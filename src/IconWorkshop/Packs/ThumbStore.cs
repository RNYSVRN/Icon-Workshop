using System.Collections.Concurrent;
using System.Text;
using System.Windows.Media.Imaging;

namespace IconWorkshop.Packs;

/// <summary>
/// All preview images of a pack in one file (thumbs.bin): small 128 px PNGs plus an index.
/// Opening thousands of single files is slow on Windows (file system and virus scanner per file);
/// reading slices from one open file is not. Built during import, or once in the background for
/// packs imported without it.
/// </summary>
public sealed class ThumbStore : IDisposable
{
    public const string FileName = "thumbs.bin";
    public const int Size = 128;
    private const uint Magic = 0x31545749; // "IWT1"

    private readonly FileStream _stream;
    private readonly Dictionary<string, (long Offset, int Length)> _index;
    private readonly object _lock = new();

    private ThumbStore(FileStream stream, Dictionary<string, (long, int)> index)
    {
        _stream = stream;
        _index = index;
    }

    public int Count => _index.Count;

    /// <summary>Opens thumbs.bin of a pack folder; null if missing or damaged.</summary>
    public static ThumbStore? TryOpen(string packFolder)
    {
        string file = Path.Combine(packFolder, FileName);
        if (!File.Exists(file)) return null;
        FileStream? fs = null;
        try
        {
            fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1, FileOptions.RandomAccess);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
            if (br.ReadUInt32() != Magic || br.ReadInt32() != Size) throw new InvalidDataException();
            int count = br.ReadInt32();
            var index = new Dictionary<string, (long, int)>(count, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < count; i++)
            {
                string name = br.ReadString();
                long offset = br.ReadInt64();
                int length = br.ReadInt32();
                index[name] = (offset, length);
            }
            return new ThumbStore(fs, index);
        }
        catch
        {
            fs?.Dispose();
            return null;
        }
    }

    /// <summary>The encoded preview (PNG) of an icon, or null.</summary>
    public byte[]? Get(string name)
    {
        if (!_index.TryGetValue(name, out var e)) return null;
        var buffer = new byte[e.Length];
        lock (_lock)
        {
            _stream.Position = e.Offset;
            _stream.ReadExactly(buffer);
        }
        return buffer;
    }

    public void Dispose() => _stream.Dispose();

    // ---------- Building ----------

    /// <summary>Scales an icon image down to the preview size and encodes it as PNG.</summary>
    public static byte[] MakeThumb(byte[] image)
    {
        // Read the dimensions first, then let the decoder scale (high quality, aspect ratio kept)
        var header = BitmapDecoder.Create(new MemoryStream(image), BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None).Frames[0];
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        if (header.PixelWidth > Size || header.PixelHeight > Size)
        {
            if (header.PixelWidth >= header.PixelHeight) img.DecodePixelWidth = Size;
            else img.DecodePixelHeight = Size;
        }
        img.StreamSource = new MemoryStream(image);
        img.EndInit();
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(img));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>Writes thumbs.bin from already computed previews (name -> PNG).</summary>
    public static void Write(string packFolder, IReadOnlyList<(string Name, byte[] Png)> thumbs)
    {
        string file = Path.Combine(packFolder, FileName);
        string tmp = file + ".tmp";
        using (var fs = File.Create(tmp))
        using (var bw = new BinaryWriter(fs, Encoding.UTF8))
        {
            bw.Write(Magic);
            bw.Write(Size);
            bw.Write(thumbs.Count);
            // Index size first, so the data offsets are known
            long headerSize = 12 + thumbs.Sum(t => StringSize(t.Name) + 12L);
            long offset = headerSize;
            foreach (var (name, png) in thumbs)
            {
                bw.Write(name);
                bw.Write(offset);
                bw.Write(png.Length);
                offset += png.Length;
            }
            foreach (var (_, png) in thumbs) bw.Write(png);
        }
        File.Move(tmp, file, overwrite: true);
    }

    /// <summary>Builds thumbs.bin for an existing pack from its icons folder (in parallel).</summary>
    public static void Build(IconPack pack, CancellationToken ct = default)
    {
        var result = new ConcurrentBag<(string, byte[])>();
        Parallel.ForEach(pack.Icons, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, icon =>
        {
            try { result.Add((icon.Name, MakeThumb(File.ReadAllBytes(pack.PngPath(icon))))); }
            catch { /* unreadable icon: shown without preview cache */ }
        });
        ct.ThrowIfCancellationRequested();
        Write(pack.Folder, result.ToList());
    }

    private static long StringSize(string s)
    {
        int bytes = Encoding.UTF8.GetByteCount(s);
        int prefix = 1;
        for (uint v = (uint)bytes; v >= 0x80; v >>= 7) prefix++;
        return prefix + bytes;
    }
}
