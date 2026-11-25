using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IconWorkshop.Assignments;
using IconWorkshop.Controls;
using IconWorkshop.Packs;

namespace IconWorkshop.Views;

/// <summary>Pages that react to being shown or to Ctrl+F.</summary>
public interface IPage
{
    void OnShown() { }
    void FocusSearch() { }
}

public partial class MainWindow : Window, FluentDialog.IDimmable
{
    /// <summary>
    /// Navigation entry. Deliberately a class, not a record: the page is created lazily, and a record's
    /// hash code would change with it, which breaks the ListBox selection.
    /// </summary>
    public sealed class NavEntry(string glyph, string label, Func<FrameworkElement> create, bool separatorBefore = false)
    {
        private FrameworkElement? _page;
        public string Glyph { get; } = glyph;
        public string Label { get; } = label;
        public bool SeparatorBefore { get; } = separatorBefore;
        public FrameworkElement Page => _page ??= create();
    }

    public PackStore Packs { get; }
    public AssignmentStore Assignments { get; }

    private readonly NavEntry _libraryNav, _packsNav;
    private NavEntry? _makerNav;

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    /// <summary>Icon-only navigation in narrow windows (like Windows 11 Settings).</summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public MainWindow()
    {
        InitializeComponent();
        Packs = new PackStore(App.Settings.EffectivePacksFolder);
        Packs.Load();
        Assignments = new AssignmentStore(AppInfo.DataFolder);
        Packs.UsageCount = Assignments.UsageCount;
        // Self-healing: ICO files of existing assignments that went missing are re-created in the background
        Task.Run(() => { try { Assignments.RepairMissingIcos(Packs); } catch (Exception ex) { App.LogError(ex); } });

        _libraryNav = new("", Loc.NavLibrary, () => new LibraryPage(this), separatorBefore: true);
        _packsNav = new("", Loc.NavPacks, () => new PacksPage(this));
        Nav.ItemsSource = new[]
        {
            new NavEntry("", Loc.NavPrograms, () => new ProgramsPage(this)),
            new NavEntry("", Loc.NavSystemIcons, () => TargetsPage.SystemIconsPage(this)),
            new NavEntry("", Loc.NavDrives, () => TargetsPage.DrivesPage(this)),
            new NavEntry("", Loc.NavFolders, () => TargetsPage.FoldersPage(this)),
            new NavEntry("", Loc.NavFileTypes, () => TargetsPage.FileTypesPage(this)),
            _libraryNav,
            _packsNav,
            _makerNav = new NavEntry("", Loc.T("Icon-Maker", "Icon maker"), () => new IconMakerPage(this)),
            new NavEntry("", Loc.NavAssignments, () => new AssignmentsPage(this), separatorBefore: true),
        };
        // Without packs, start where you import them; otherwise with the programs
        Nav.SelectedItem = Packs.Packs.Count == 0 ? _packsNav : Nav.Items[0];

        SettingsPanel.BackRequested += () => ShowSettings(false);
        SettingsPanel.RestartRequested += () => ((App)Application.Current).Restart();
        SettingsPanel.Initialize(this);

        SizeChanged += (_, _) => UpdateCompact();
        SizeChanged += (_, _) => UpdateFrameMargin();
        StateChanged += (_, _) => Dispatcher.BeginInvoke(UpdateFrameMargin, System.Windows.Threading.DispatcherPriority.Background);
        ContentRendered += (_, _) => UpdateFrameMargin();
        UpdateCompact();
        PreviewKeyDown += OnPreviewKeyDown;
        DragEnter += OnDragOver;
        DragOver += OnDragOver;
        DragLeave += (_, _) => DropOverlay.Visibility = Visibility.Collapsed;
        Drop += OnDrop;
    }

    // ---------- "Assign to" from the library ----------

    /// <summary>Icon chosen in the library that waits for a target; the next detail view preselects it.</summary>
    public IconItem? PendingIcon { get; private set; }

    /// <summary>Areas that can receive an icon: nav index and label.</summary>
    public IEnumerable<(int Index, string Label)> AssignAreas()
    {
        var entries = Nav.ItemsSource.Cast<NavEntry>().ToList();
        for (int i = 0; i < 5 && i < entries.Count; i++) yield return (i, entries[i].Label);
    }

    public void StartAssign(IconItem icon, int areaIndex)
    {
        PendingIcon = icon;
        PendingImage.Source = Imaging.ImageTools.LoadThumbnail(icon.Path, 64);
        PendingText.Text = Loc.T($"Wähle das Ziel für „{icon.Title}“ aus {icon.Pack.Name}. Das Icon ist dort schon ausgewählt.",
                                 $"Choose the target for “{icon.Title}” from {icon.Pack.Name}. The icon will already be selected there.");
        PendingBar.Visibility = Visibility.Visible;
        Nav.SelectedItem = Nav.Items[areaIndex];
    }

    /// <summary>Called by a detail view: returns the waiting icon once and ends the "assign to" mode.</summary>
    public IconItem? TakePendingIcon()
    {
        var icon = PendingIcon;
        CancelPending();
        return icon;
    }

    private void CancelPending()
    {
        PendingIcon = null;
        PendingBar.Visibility = Visibility.Collapsed;
        PendingImage.Source = null;
    }

    private void CancelPending_Click(object sender, RoutedEventArgs e) => CancelPending();

    // ---------- Window placement ----------

    /// <summary>Restores position and size of the last session, if that place is still on a screen.</summary>
    public void RestorePlacement(Services.AppSettings s)
    {
        if (s.WindowWidth is double w && s.WindowHeight is double h && s.WindowLeft is double l && s.WindowTop is double t)
        {
            var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                                  SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            // At least the title bar area must be visible, otherwise use the default (e.g. a monitor was removed)
            if (screen.IntersectsWith(new Rect(l + 40, t, Math.Max(100, w - 80), 40)))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = l; Top = t;
                Width = Math.Max(MinWidth, w); Height = Math.Max(MinHeight, h);
            }
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
        Closing += (_, _) => SavePlacement(s);
    }

    private void SavePlacement(Services.AppSettings s)
    {
        var r = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (r.IsEmpty || r.Width <= 0) return;
        s.WindowLeft = r.Left; s.WindowTop = r.Top; s.WindowWidth = r.Width; s.WindowHeight = r.Height;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        s.Save();
    }

    /// <summary>Shows the window in front (when the app is started a second time).</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;   // reliably in front of other windows, then back to normal
        Topmost = false;
        Focus();
    }

    // ---------- Navigation ----------

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not NavEntry entry) return;
        PageHost.Content = entry.Page;
        (entry.Page as IPage)?.OnShown();
    }

    public void ShowLibrary(string? packId)
    {
        Nav.SelectedItem = _libraryNav;
        (_libraryNav.Page as LibraryPage)?.ShowPack(packId);
    }

    public void ShowPacks() => Nav.SelectedItem = _packsNav;

    /// <summary>
    /// Explorer context menu: opens the right page for a shortcut (.lnk/.url), a folder or a drive root,
    /// with the detail view to choose the icon.
    /// </summary>
    public async void OpenPath(string path)
    {
        try
        {
            ShowSettings(false);
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".lnk" or ".url" && Nav.Items[0] is NavEntry programsNav)
            {
                Nav.SelectedItem = programsNav;
                if (programsNav.Page is ProgramsPage programs && !await programs.OpenShortcutAsync(path))
                    FluentDialog.Show(this, Loc.T("Verknüpfung nicht gefunden", "Shortcut not found"),
                        Loc.T("Die Icon Werkstatt kümmert sich um Verknüpfungen auf dem Desktop, im Startmenü und in der Taskleiste. Diese liegt woanders:\n",
                              "Icon Workshop handles shortcuts on the desktop, in the Start menu and on the taskbar. This one is somewhere else:\n") + path, Loc.Ok);
                return;
            }
            if (!System.IO.Directory.Exists(path)) return;
            string full = System.IO.Path.GetFullPath(path);
            string root = System.IO.Path.GetPathRoot(full) ?? "";
            bool isDrive = string.Equals(full.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) && root.Length >= 2 && root[1] == ':';
            var nav = Nav.Items[isDrive ? 2 : 3] as NavEntry;
            if (nav is null) return;
            Nav.SelectedItem = nav;
            if (nav.Page is not TargetsPage page) return;
            if (isDrive) page.OpenById("drive:" + char.ToLowerInvariant(root[0]));
            else page.AddFolderAndShow(full);
        }
        catch (Exception ex) { App.LogError(ex); }
    }

    /// <summary>Opens the one-click generator of the icon maker for a program.</summary>
    public void GenerateInMaker(IconWorkshop.Programs.ProgramEntry program)
    {
        if (_makerNav is null) return;
        Nav.SelectedItem = _makerNav;
        (_makerNav.Page as IconMakerPage)?.GenerateFor(program);
    }

    /// <summary>Opens the one-click generator for a drive, folder, file type or system icon.</summary>
    public void GenerateInMaker(string name, IconWorkshop.Imaging.IconCategory category)
    {
        if (_makerNav is null) return;
        Nav.SelectedItem = _makerNav;
        (_makerNav.Page as IconMakerPage)?.GenerateFor(name, category);
    }

    /// <summary>Opens an icon in the icon maker (own icons for editing, others as a layer of a new design).</summary>
    public void OpenInMaker(IconItem item)
    {
        if (_makerNav is null) return;
        Nav.SelectedItem = _makerNav;
        (_makerNav.Page as IconMakerPage)?.Open(item);
    }

    private bool _wasNarrow;
    /// <summary>Menu button pressed while the window is narrow: applies until the width class changes.</summary>
    private bool? _narrowOverride;

    /// <summary>Symbols only when collapsed via the menu button, or automatically in narrow windows.</summary>
    private void UpdateCompact()
    {
        bool narrow = ActualWidth > 0 && ActualWidth < 960;
        if (narrow != _wasNarrow) { _wasNarrow = narrow; _narrowOverride = null; }
        bool compact = _narrowOverride ?? (App.Settings.NavCollapsed || narrow);
        IsCompact = compact;
        NavColumn.Width = new GridLength(compact ? NavCompactWidth : NavWidth);
        SettingsLabel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SettingsPanel.SetCompact(compact);
    }

    public const double NavWidth = 272, NavCompactWidth = 60;

    private void NavToggle_Click(object sender, RoutedEventArgs e) => ToggleNav();

    internal void ToggleNav()
    {
        if (_wasNarrow) _narrowOverride = !IsCompact;
        else { App.Settings.NavCollapsed = !IsCompact; App.Settings.Save(); }
        UpdateCompact();
    }

    /// <summary>
    /// The window keeps its left, right and bottom frame (so Windows draws the caption buttons flush with the
    /// corner like in other apps), but WPF still lays the content out over the whole window rectangle. The
    /// content therefore moves in by the part of the window that is not visible: the invisible resize border
    /// in normal state, the overhang beyond the screen edge when maximized.
    /// </summary>
    private void UpdateFrameMargin()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var win)
            || DwmGetWindowAttribute(hwnd, 9 /*DWMWA_EXTENDED_FRAME_BOUNDS*/, out var vis, 16) != 0) return;
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var margin = new Thickness(Math.Max(0, vis.Left - win.Left) / scale, Math.Max(0, vis.Top - win.Top) / scale,
                                   Math.Max(0, win.Right - vis.Right) / scale, Math.Max(0, win.Bottom - vis.Bottom) / scale);
        if (Root.Margin != margin) Root.Margin = margin;
    }

    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out NativeRect rect, int size);

    // ---------- Settings (in the same window) ----------

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings(true);

    private void ShowSettings(bool show)
    {
        if (show) SettingsPanel.Reset();
        SettingsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        MainView.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool back = e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt);
        if (back && SettingsPanel.IsVisible) { ShowSettings(false); e.Handled = true; return; }
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && !SettingsPanel.IsVisible)
        {
            (PageHost.Content as IPage)?.FocusSearch();
            e.Handled = true;
        }
    }

    public void SetSmoke(bool on) => Smoke.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

    // ---------- Drag & drop import ----------

    private static string[]? DroppedPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) ? e.Data.GetData(DataFormats.FileDrop) as string[] : null;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        bool ok = DroppedPaths(e) is { Length: > 0 } && !SettingsPanel.IsVisible;
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DropOverlay.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        if (DroppedPaths(e) is not { Length: > 0 } paths || SettingsPanel.IsVisible) return;
        ShowPacks();
        if (_packsNav.Page is PacksPage page)
            foreach (var path in paths) await page.ImportAsync(path);
    }

    // ---------- Screenshots (for documentation and design review) ----------

    /// <summary>Renders every page into PNG files (--screenshots folder). Nothing is changed.</summary>
    /// <summary>Developer measurement (--perf file): library start, thumbnails, scrolling. Nothing is changed.</summary>
    public async Task MeasurePerformanceAsync(string file)
    {
        var log = new System.Text.StringBuilder();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await Task.Delay(500);
        sw.Restart();
        Nav.SelectedItem = _libraryNav;
        UpdateLayout();
        log.AppendLine($"Bibliothek öffnen (Seite + Layout): {sw.ElapsedMilliseconds} ms");
        var lib = (LibraryPage)_libraryNav.Page;

        async Task<(long Ms, int Count)> WaitForThumbs()
        {
            var t = System.Diagnostics.Stopwatch.StartNew();
            int last = -1, start = ThumbLoader.Completed;
            long lastChange = 0;
            while (t.ElapsedMilliseconds < 10000)
            {
                await Task.Delay(20);
                if (ThumbLoader.Completed != last) { last = ThumbLoader.Completed; lastChange = t.ElapsedMilliseconds; }
                else if (t.ElapsedMilliseconds - lastChange > 400) break;
            }
            return (lastChange, last - start);
        }

        var first = await WaitForThumbs();
        log.AppendLine($"Erste Vorschaubilder: {first.Count} in {first.Ms} ms");

        var sv = lib.BrowserControl.Scroller;
        if (sv is not null)
        {
            long layoutTotal = 0, layoutMax = 0;
            for (int i = 1; i <= 30; i++)
            {
                var step = System.Diagnostics.Stopwatch.StartNew();
                sv.ScrollToVerticalOffset(i * 400);
                UpdateLayout();
                long ms = step.ElapsedMilliseconds;
                layoutTotal += ms;
                layoutMax = Math.Max(layoutMax, ms);
                await Task.Delay(16);
            }
            log.AppendLine($"Scrollen 30 Schritte: Layout gesamt {layoutTotal} ms, max. Schritt {layoutMax} ms");
            var after = await WaitForThumbs();
            log.AppendLine($"Vorschaubilder nach dem Scrollen: {after.Count} in {after.Ms} ms nach dem letzten Schritt");
        }
        foreach (var p in Packs.Packs) log.AppendLine($"Pack {p.Name}: Vorschau-Cache {(p.Thumbs is null ? "FEHLT" : p.Thumbs.Count + " Einträge")}");
        if (_makerNav is not null)
        {
            var mk = System.Diagnostics.Stopwatch.StartNew();
            Nav.SelectedItem = _makerNav;
            UpdateLayout();
            log.AppendLine($"Icon-Maker öffnen (Seite + Layout): {mk.ElapsedMilliseconds} ms");
        }
        await File.WriteAllTextAsync(file, log.ToString());
        Application.Current.Shutdown();
    }

    public async Task CaptureScreenshotsAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        string suffix = (Loc.German ? "de" : "en") + "-" + (IsDark() ? "dark" : "light");
        await Task.Delay(800);
        foreach (NavEntry entry in Nav.Items)
        {
            Nav.SelectedItem = entry;
            if (entry == _libraryNav && entry.Page is LibraryPage lib) lib.SelectFirstForScreenshot();
            if (entry.Page is IconMakerPage maker) maker.ShowExample();
            if (entry.Page is ProgramsPage programs) await programs.WaitForScanAsync();
            await Task.Delay(entry == _libraryNav ? 2500 : 600);
            Save(folder, $"{Array.IndexOf(Nav.ItemsSource.Cast<NavEntry>().ToArray(), entry) + 1:00}-{Slug(entry.Label)}-{suffix}.png");
        }
        if (Nav.Items[0] is NavEntry first && first.Page is ProgramsPage p)
        {
            Nav.SelectedItem = first;
            p.ShowDetailForScreenshot(true);
            await Task.Delay(1500);
            Save(folder, $"01b-programmdetail-{suffix}.png");
            p.ShowDetailForScreenshot(false);
            p.ShowSuggestions();
            await Task.Delay(2000);
            Save(folder, $"01c-vorschlaege-{suffix}.png");
            p.ShowDetailForScreenshot(false);
            p.ShowTilesForScreenshot(true);
            await Task.Delay(1500);
            Save(folder, $"01d-programme-kacheln-{suffix}.png");
            p.ShowTilesForScreenshot(false);
        }
        foreach (var (index, name) in new[] { (1, "systemsymbole"), (2, "laufwerke"), (3, "ordner"), (4, "dateitypen") })
        {
            if (Nav.Items[index] is not NavEntry e || e.Page is not TargetsPage tp) continue;
            Nav.SelectedItem = e;
            tp.ShowFirstDetail(true);
            await Task.Delay(1500);
            Save(folder, $"0{index + 1}b-{name}-detail-{suffix}.png");
            tp.ShowFirstDetail(false);
        }
        // "Assign to" from the library: banner on the target page, icon preselected in the detail view
        if (_libraryNav.Page is LibraryPage library && library.BrowserControl.Selected is { } chosen
            && Nav.Items[0] is NavEntry programsEntry && programsEntry.Page is ProgramsPage programsPage)
        {
            StartAssign(chosen, 0);
            await Task.Delay(800);
            Save(folder, $"06b-zuweisen-an-{suffix}.png");
            programsPage.ShowDetailForScreenshot(true);
            await Task.Delay(1500);
            Save(folder, $"06c-zuweisen-an-detail-{suffix}.png");
            programsPage.ShowDetailForScreenshot(false);
            CancelPending();
        }
        // Explorer context menu: a drive and a folder open their detail views
        foreach (var (target, file) in new[] { (System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "10-kontextmenue-laufwerk"), (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "10b-kontextmenue-ordner") })
        {
            OpenPath(target);
            await Task.Delay(1500);
            Save(folder, $"{file}-{suffix}.png");
        }
        // Generator with the Windows shapes (folder, file type, drive)
        if (_makerNav?.Page is IconMakerPage generator)
        {
            Nav.SelectedItem = _makerNav;
            foreach (var (name, category, file) in new[] { (Loc.T("Projekte", "Work"), IconWorkshop.Imaging.IconCategories.Folder, "08b-generator-ordner"), (".psd", IconWorkshop.Imaging.IconCategories.FileType, "08c-generator-dateityp"), ("D: Daten", IconWorkshop.Imaging.IconCategories.Drive, "08d-generator-laufwerk") })
            {
                generator.GenerateFor(name, category);
                await Task.Delay(1200);
                Save(folder, $"{file}-{suffix}.png");
            }
        }
        ShowSettings(true);
        for (int i = 0; i < SettingsPanel.PageCount; i++)
        {
            SettingsPanel.SelectPage(i);
            await Task.Delay(500);
            Save(folder, $"20-settings-{i + 1}-{suffix}.png");
        }
        Application.Current.Shutdown();
    }

    private void Save(string folder, string name)
    {
        UpdateLayout();
        var dpi = VisualTreeHelper.GetDpi(this);
        int w = (int)(Root.ActualWidth * dpi.DpiScaleX), h = (int)(Root.ActualHeight * dpi.DpiScaleY);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
        {
            var bg = TryFindResource("SolidBackgroundFillColorBaseBrush") as Brush ?? Brushes.White;
            dc.DrawRectangle(bg, null, new Rect(0, 0, Root.ActualWidth, Root.ActualHeight));
        }
        var rtb = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        rtb.Render(background);
        rtb.Render(Root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(Path.Combine(folder, name));
        enc.Save(fs);
    }

    private bool IsDark() => TryFindResource("SolidBackgroundFillColorBaseBrush") is SolidColorBrush b && b.Color.R < 128;

    private static string Slug(string s) => new(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());

    internal static SolidColorBrush Hex(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
