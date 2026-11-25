using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using IconWorkshop.Imaging;
using IconWorkshop.Packs;

namespace IconWorkshop.Controls;

/// <summary>An icon in a browser grid. The thumbnail is loaded lazily in the background when first shown.</summary>
public sealed class IconItem : INotifyPropertyChanged
{
    public IconPack Pack { get; }
    public PackIcon Icon { get; }
    public string Path { get; }
    public string Title => Icon.Title;
    public string SearchText { get; }

    // One shared list per pack: all pages (library, pickers) use the same items and thus the same loaded thumbnails
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IconPack, List<IconItem>> Shared = new();

    /// <summary>The icons of a pack as browser items, created once and shared.</summary>
    public static List<IconItem> For(IconPack pack)
    {
        var list = Shared.GetValue(pack, p => p.Icons.Select(i => new IconItem(p, i)).ToList());
        if (list.Count == pack.Icons.Count) return list;
        // Icons were added (icon maker): keep the existing items, append the new ones
        var known = list.Select(i => i.Icon).ToHashSet();
        list.AddRange(pack.Icons.Where(i => !known.Contains(i)).Select(i => new IconItem(pack, i)));
        return list;
    }

    public IconItem(IconPack pack, PackIcon icon)
    {
        Pack = pack;
        Icon = icon;
        Path = pack.PngPath(icon);
        SearchText = icon.Name.Replace('_', ' ').ToLowerInvariant();
    }

    private ImageSource? _thumb;
    private bool _requested;

    public ImageSource? Thumb
    {
        get
        {
            if (_thumb is null && !_requested)
            {
                _requested = true;
                ThumbLoader.Request(this);
            }
            return _thumb;
        }
    }

    internal void SetThumb(ImageSource? thumb)
    {
        _thumb = thumb;
        if (thumb is null) _requested = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumb)));
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Loads thumbnails on background threads, newest requests first (what's on screen right now),
/// and keeps only a limited number in memory.
/// </summary>
public static class ThumbLoader
{
    private const int MaxLoaded = 2500;
    private static readonly ConcurrentStack<IconItem> Pending = new();
    private static readonly Queue<IconItem> Loaded = new();
    private static readonly SemaphoreSlim Signal = new(0);
    private static int _decodeSize = 96;
    private static int _generation;
    private static bool _started;

    /// <summary>Pixel size thumbnails are decoded at. Changing it reloads visible thumbnails.</summary>
    public static int DecodeSize
    {
        get => _decodeSize;
        set
        {
            if (value == _decodeSize) return;
            _decodeSize = value;
            Reset();
        }
    }

    /// <summary>Number of thumbnails delivered so far (performance measurements).</summary>
    public static int Completed;

    public static void Request(IconItem item)
    {
        EnsureStarted();
        Pending.Push(item);
        Signal.Release();
    }

    /// <summary>Forgets all thumbnails (e.g. tile size changed); they are reloaded when shown again.</summary>
    public static void Reset()
    {
        Interlocked.Increment(ref _generation);
        Pending.Clear();
        while (Loaded.Count > 0) Loaded.Dequeue().SetThumb(null);
    }

    private static void EnsureStarted()
    {
        if (_started) return;
        _started = true;
        int workers = Math.Clamp(Environment.ProcessorCount - 1, 2, 6);
        for (int i = 0; i < workers; i++)
            new Thread(Work) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "ThumbLoader" }.Start();
    }

    private static void Work()
    {
        var dispatcher = Application.Current.Dispatcher;
        while (true)
        {
            Signal.Wait();
            if (!Pending.TryPop(out var item)) continue;
            int gen = _generation;
            // Preview cache of the pack (one open file) first; the single PNG only as a fallback
            var cached = item.Pack.Thumbs?.Get(item.Icon.Name);
            var thumb = cached is not null ? ImageTools.DecodeThumbnail(cached, _decodeSize) : ImageTools.LoadThumbnail(item.Path, _decodeSize);
            Done.Enqueue((item, thumb, gen));
            // Results are handed to the UI in batches (one dispatcher call per batch, not per icon),
            // with normal priority so previews also appear while scrolling.
            if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
                dispatcher.BeginInvoke(Flush, System.Windows.Threading.DispatcherPriority.Normal);
        }
    }

    private static readonly ConcurrentQueue<(IconItem Item, ImageSource? Thumb, int Gen)> Done = new();
    private static int _flushScheduled;

    private static void Flush()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        while (Done.TryDequeue(out var r))
        {
            if (r.Gen != _generation) { r.Item.SetThumb(null); continue; }
            r.Item.SetThumb(r.Thumb);
            Completed++;
            Loaded.Enqueue(r.Item);
        }
        while (Loaded.Count > MaxLoaded) Loaded.Dequeue().SetThumb(null);
    }
}
