using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using IconWorkshop.Packs;

namespace IconWorkshop.Controls;

/// <summary>
/// Searchable icon grid over one or all packs. Rows are virtualized (only visible rows are created),
/// thumbnails load in the background, so packs with thousands of icons stay smooth.
/// </summary>
public partial class IconBrowser : UserControl
{
    private sealed record PackOption(IconPack? Pack, string Label);
    private sealed record CategoryOption(string? Name, string Label, bool Uncategorized = false);

    private static readonly (double Image, double Tile, bool Labels, int Decode)[] Sizes =
    [
        (32, 52, false, 0),
        (48, 96, true, 0),
        (72, 124, true, 0),
    ];

    private PackStore? _store;
    private List<IconItem> _all = new();
    private List<IconItem> _filtered = new();
    private readonly DispatcherTimer _searchDelay;
    private int _columns = 1;
    private bool _updating;

    /// <summary>The selected icon changed (null = none).</summary>
    public event Action<IconItem?>? SelectionChanged;

    /// <summary>Double click on an icon.</summary>
    public event Action<IconItem>? IconActivated;

    /// <summary>Number of shown icons / all icons changed.</summary>
    public event Action<int, int>? CountChanged;

    /// <summary>Tile size index (0 small, 1 medium, 2 large) changed by the user.</summary>
    public event Action<int>? TileSizeChanged;

    public IconItem? Selected { get; private set; }

    /// <summary>Number of icons currently shown (after search and filters).</summary>
    public int ShownCount => _filtered.Count;

    public static readonly DependencyProperty TileWidthProperty = DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(IconBrowser), new(96.0));
    public static readonly DependencyProperty TileHeightProperty = DependencyProperty.Register(nameof(TileHeight), typeof(double), typeof(IconBrowser), new(96.0));
    public static readonly DependencyProperty ImageSizeProperty = DependencyProperty.Register(nameof(ImageSize), typeof(double), typeof(IconBrowser), new(48.0));
    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(IconBrowser), new(true));

    /// <summary>Tile height last used by any browser (estimate for rows before their owner is known).</summary>
    internal static double LastTileHeight { get; private set; } = 96;

    public double TileWidth { get => (double)GetValue(TileWidthProperty); set => SetValue(TileWidthProperty, value); }
    public double TileHeight { get => (double)GetValue(TileHeightProperty); set => SetValue(TileHeightProperty, value); }
    public double ImageSize { get => (double)GetValue(ImageSizeProperty); set => SetValue(ImageSizeProperty, value); }
    public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }

    public IconBrowser()
    {
        InitializeComponent();
        _searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); ApplyFilter(); };
        SizeBox.ItemsSource = new[] { Loc.TileSmall, Loc.TileMedium, Loc.TileLarge };
        SetTileSize(1, notify: false);
        // The DPI is only known once the control is shown
        Loaded += (_, _) => SetTileSize(SizeBox.SelectedIndex, notify: false);
    }

    public void Attach(PackStore store, string? packId, int tileSize)
    {
        _store = store;
        store.Changed += () => Reload(SelectedPackId);
        SetTileSize(tileSize, notify: false);
        Reload(packId);
    }

    public string? SelectedPackId => (PackBox.SelectedItem as PackOption)?.Pack?.Id;

    /// <summary>Shows a specific pack (null = all packs).</summary>
    public void ShowPack(string? packId)
    {
        if (PackBox.ItemsSource is IEnumerable<PackOption> options)
            PackBox.SelectedItem = options.FirstOrDefault(o => o.Pack?.Id == packId) ?? options.First();
    }

    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>Sets the search text and filters right away (without the typing delay).</summary>
    public void SetSearch(string text)
    {
        SearchBox.Text = text;
        _searchDelay.Stop();
        ApplyFilter();
    }

    /// <summary>Tries search terms in order and keeps the first one that finds icons (selecting the first icon).</summary>
    /// <summary>Shows and selects a specific icon (e.g. one chosen in the library).</summary>
    public void Preselect(IconItem icon)
    {
        ShowPack(null);
        SetSearch(icon.Title);
        var match = _filtered.FirstOrDefault(i => i.Path == icon.Path);
        if (match is not null) Select(match);
    }

    public void Suggest(IEnumerable<string> candidates)
    {
        foreach (var c in candidates.Select(c => c.Trim().ToLowerInvariant()).Where(c => c.Length > 0).Distinct())
        {
            SetSearch(c);
            if (ShownCount > 0) { SelectFirst(); return; }
        }
        SetSearch("");
    }

    internal ScrollViewer? Scroller => Rows.Template.FindName("Scroller", Rows) as ScrollViewer;

    /// <summary>Selects the first shown icon (if any).</summary>
    public void SelectFirst()
    {
        if (_filtered.Count > 0) Select(_filtered[0]);
    }

    private void FilterBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool narrow = FilterBar.ActualWidth < 700;
        Grid.SetRow(FilterArea, narrow ? 1 : 0);
        Grid.SetColumn(FilterArea, narrow ? 0 : 1);
        Grid.SetColumnSpan(SearchArea, narrow ? 2 : 1);
        FilterArea.Margin = narrow ? new Thickness(-8, 8, 0, 0) : new Thickness(0);
    }

    private void Reload(string? packId)
    {
        if (_store is null) return;
        _updating = true;
        var options = new List<PackOption> { new(null, Loc.AllPacks) };
        options.AddRange(_store.Packs.Select(p => new PackOption(p, p.Name)));
        PackBox.ItemsSource = options;
        PackBox.SelectedItem = options.FirstOrDefault(o => o.Pack is not null && o.Pack.Id == packId) ?? options[0];
        _updating = false;
        RebuildItems();
    }

    private void RebuildItems()
    {
        var pack = (PackBox.SelectedItem as PackOption)?.Pack;
        var packs = pack is null ? _store?.Packs.ToList() ?? new() : new List<IconPack> { pack };
        _all = packs.SelectMany(IconItem.For).ToList();

        // Categories: of the selected pack, or merged by name over all packs
        _updating = true;
        var cats = new List<CategoryOption> { new(null, Loc.AllCategories) };
        cats.AddRange(packs.SelectMany(p => p.Categories).Distinct(StringComparer.CurrentCultureIgnoreCase)
                           .Select(c => new CategoryOption(c, c)));
        if (_all.Any(i => i.Icon.Categories is null) && cats.Count > 1) cats.Add(new(null, Loc.NoCategory, Uncategorized: true));
        var previous = CategoryBox.SelectedItem as CategoryOption;
        CategoryBox.ItemsSource = cats;
        CategoryBox.SelectedItem = cats.FirstOrDefault(c => previous is not null && c.Label == previous.Label) ?? cats[0];
        CategoryBox.IsEnabled = cats.Count > 1;
        _updating = false;

        if (Selected is not null && !_all.Any(i => i.Path == Selected.Path)) Select(null);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string[] terms = SearchBox.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cat = CategoryBox.SelectedItem as CategoryOption;
        IEnumerable<IconItem> q = _all;
        if (cat is { Uncategorized: true }) q = q.Where(i => i.Icon.Categories is null);
        else if (cat?.Name is { } name)
            q = q.Where(i => i.Icon.Categories is { } cs && cs.Any(c => c < i.Pack.Categories.Count
                                                                     && i.Pack.Categories[c].Equals(name, StringComparison.CurrentCultureIgnoreCase)));
        if (terms.Length > 0) q = q.Where(i => terms.All(t => i.SearchText.Contains(t)));

        _filtered = q.ToList();
        // Exact and prefix matches first when searching
        if (terms.Length > 0)
        {
            string first = terms[0];
            _filtered = _filtered.OrderBy(i => i.SearchText == first ? 0 : i.SearchText.StartsWith(first, StringComparison.Ordinal) ? 1 : 2)
                                 .ThenBy(i => i.SearchText.Length).ToList();
        }

        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = _filtered.Count == 0 && _all.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        BuildRows(scrollToTop: true);
        CountChanged?.Invoke(_filtered.Count, _all.Count);
    }

    private void BuildRows(bool scrollToTop)
    {
        double available = Math.Max(0, Rows.ActualWidth - 20); // minus scrollbar
        int columns = Math.Max(1, (int)(available / (TileWidth + 4)));
        _columns = columns;
        var rows = new List<List<IconItem>>((_filtered.Count + columns - 1) / columns);
        for (int i = 0; i < _filtered.Count; i += columns)
            rows.Add(_filtered.GetRange(i, Math.Min(columns, _filtered.Count - i)));
        Rows.ItemsSource = rows;
        if (scrollToTop && Rows.Template.FindName("Scroller", Rows) is ScrollViewer sv) sv.ScrollToTop();
    }

    private void Rows_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged) return;
        int columns = Math.Max(1, (int)(Math.Max(0, Rows.ActualWidth - 20) / (TileWidth + 4)));
        if (columns != _columns) BuildRows(scrollToTop: false);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void PackBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating) RebuildItems();
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updating) ApplyFilter();
    }

    private void SizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || SizeBox.SelectedIndex < 0) return;
        SetTileSize(SizeBox.SelectedIndex, notify: true);
    }

    private void SetTileSize(int index, bool notify)
    {
        index = Math.Clamp(index, 0, Sizes.Length - 1);
        var s = Sizes[index];
        ImageSize = s.Image;
        TileWidth = s.Tile;
        TileHeight = s.Labels ? s.Tile : s.Tile;
        LastTileHeight = TileHeight;
        ShowLabels = s.Labels;
        // Decode exactly at the shown pixel size (DPI-aware), at most the size of the preview cache
        double dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this).DpiScaleX;
        ThumbLoader.DecodeSize = Math.Min(Packs.ThumbStore.Size, (int)Math.Ceiling(s.Image * Math.Max(1, dpi)));
        _updating = true;
        SizeBox.SelectedIndex = index;
        _updating = false;
        InvalidateRows(measure: true);
        if (IsLoaded) BuildRows(scrollToTop: false);
        if (notify) TileSizeChanged?.Invoke(index);
    }

    // ---------- Rows (drawn elements) ----------

    private readonly HashSet<IconRowElement> _rows = new();

    internal void RegisterRow(IconRowElement row) => _rows.Add(row);
    internal void UnregisterRow(IconRowElement row) => _rows.Remove(row);
    internal void SelectFromRow(IconItem item) => Select(item);
    internal void ActivateFromRow(IconItem item)
    {
        Select(item);
        IconActivated?.Invoke(item);
    }

    private void InvalidateRows(bool measure)
    {
        foreach (var r in _rows)
        {
            if (measure) r.InvalidateMeasure();
            r.InvalidateVisual();
        }
    }

    /// <summary>The selection lives in the browser (items are shared between pages); visible rows redraw.</summary>
    private void Select(IconItem? item)
    {
        Selected = item;
        InvalidateRows(measure: false);
        SelectionChanged?.Invoke(item);
    }
}
