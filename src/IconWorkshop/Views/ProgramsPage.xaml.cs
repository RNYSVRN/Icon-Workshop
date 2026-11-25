using System.Collections.Concurrent;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using IconWorkshop.Assignments;
using IconWorkshop.Controls;
using IconWorkshop.Imaging;
using IconWorkshop.Native;
using IconWorkshop.Packs;
using IconWorkshop.Programs;
using IconWorkshop.Services;

namespace IconWorkshop.Views;

/// <summary>A program in the list. The current icon is loaded lazily from the shell.</summary>
public sealed class ProgramRow : INotifyPropertyChanged
{
    public ProgramEntry Entry { get; }
    public string Name => Entry.Name;
    public string Subtitle => Entry.Subtitle;
    public string SearchText { get; }

    /// <summary>Letter group like "All apps" in the Start menu: A-Z, "#" for digits, "&" for others.</summary>
    public string Group
    {
        get
        {
            string n = Entry.Name.Trim();
            if (n.Length == 0) return "&";
            char c = n.Normalize(System.Text.NormalizationForm.FormD)[0];
            return char.IsDigit(c) ? "#" : char.IsLetter(c) ? char.ToUpperInvariant(c).ToString() : "&";
        }
    }

    public ProgramRow(ProgramEntry entry)
    {
        Entry = entry;
        SearchText = (entry.Name + " " + System.IO.Path.GetFileNameWithoutExtension(entry.ExePath ?? "")).ToLowerInvariant();
    }

    private bool _isChanged;
    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; Raise(nameof(IsChanged)); Raise(nameof(ChangedLabel)); }
    }

    public string ChangedLabel => Loc.T("Neues Icon", "New icon");

    /// <summary>Preview of the assigned icon (from the pack), shown instead of the shell icon when changed.</summary>
    public string? AssignedPreview { get; set; }

    private ImageSource? _icon;
    private bool _requested;
    public ImageSource? Icon
    {
        get
        {
            if (_icon is null && !_requested)
            {
                _requested = true;
                if (AssignedPreview is not null) _icon = ImageTools.LoadThumbnail(AssignedPreview, 64);
                // Steam shortcut with a missing icon file: Steam's own small game icon instead of the blank page
                if (_icon is null && Entry.IconMissing && Entry.Game is { Launcher: GameLauncher.Steam } g && GameLibrary.SteamClientIcon(g.Id) is { } steamIcon)
                    _icon = ImageTools.LoadThumbnail(steamIcon, 64);
                if (_icon is null) ShellIconLoader.Request(Entry.IconSource, 64, img => { _icon = img; Raise(nameof(Icon)); });
            }
            return _icon;
        }
    }

    public void ResetIcon()
    {
        _icon = null;
        _requested = false;
        Raise(nameof(Icon));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A shortcut of the selected program with its checkbox.</summary>
public sealed class ShortcutRow(ShortcutRef shortcut, bool isChanged)
{
    public ShortcutRef Shortcut { get; } = shortcut;
    public string Label => Shortcut.LocationLabel;
    public string Path => DemoMode.Mask(Shortcut.Path);
    public bool NeedsAdmin => Shortcut.NeedsAdmin;
    public bool IsChanged { get; } = isChanged;
    public bool IsChecked { get; set; } = true;
}

/// <summary>Loads shell icons (current icons of shortcuts and apps) on one STA thread, newest requests first.</summary>
public static class ShellIconLoader
{
    private static readonly ConcurrentStack<(Func<ImageSource?> Load, Action<ImageSource?> Done)> Pending = new();
    private static readonly SemaphoreSlim Signal = new(0);
    private static bool _started;

    public static void Request(string source, int size, Action<ImageSource?> done)
    {
        if (string.IsNullOrEmpty(source)) return;
        Request(() => ShellNative.GetItemImage(source, size), done);
    }

    /// <summary>Runs any icon loader on the shell thread.</summary>
    public static void Request(Func<ImageSource?> load, Action<ImageSource?> done)
    {
        if (!_started)
        {
            _started = true;
            var t = new Thread(Work) { IsBackground = true, Name = "ShellIcons", Priority = ThreadPriority.BelowNormal };
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
        Pending.Push((load, done));
        Signal.Release();
    }

    private static void Work()
    {
        var dispatcher = Application.Current.Dispatcher;
        while (true)
        {
            Signal.Wait();
            if (!Pending.TryPop(out var r)) continue;
            ImageSource? img = null;
            try { img = r.Load(); } catch { }
            dispatcher.BeginInvoke(() => r.Done(img));
        }
    }
}

/// <summary>Programs: list of all programs and the detail view to give their shortcuts a new icon.</summary>
public partial class ProgramsPage : UserControl, IPage
{
    // ---------- Texts for XAML ----------
    public static string SearchText => Loc.T("Programme suchen", "Search programs");
    public static string RescanText => Loc.T("Neu einlesen", "Rescan");
    public static string NoProgramsText => Loc.T("Keine passenden Programme gefunden.", "No matching programs found.");
    public static string ChooseIconText => Loc.T("Icon auswählen", "Choose icon");
    public static string CurrentText => Loc.T("Aktuell", "Current");
    public static string NewText => Loc.T("Neu", "New");
    public static string ChangedText => Loc.T("geändert", "changed");
    public static string AdminTip => Loc.T("Verknüpfung aller Benutzer: Zum Ändern fragt Windows nach Administratorrechten.",
                                           "Shortcut of all users: Windows asks for admin rights to change it.");
    public static string CreateHeader => Loc.T("Noch keine Verknüpfung vorhanden", "No shortcut yet");
    public static string CreateDesc => Loc.T("Die Icon Werkstatt legt eine eigene Verknüpfung mit dem neuen Icon an. Über sie kannst du das Programm auch an die Taskleiste anheften.",
                                             "Icon Workshop creates its own shortcut with the new icon. You can also pin the program to the taskbar from it.");
    public static string OnDesktop => Loc.T("Auf dem Desktop", "On the desktop");
    public static string InStartMenu => Loc.T("Im Startmenü", "In the Start menu");
    public static string RestoreText => Loc.T("Original wiederherstellen", "Restore original");
    public static string ApplyText => Loc.T("Icon zuweisen", "Assign icon");
    public static string SuggestText => Loc.T("Vorschläge", "Suggestions");
    public static string GenerateText => Loc.T("Icon erzeugen", "Generate icon");
    public static string GenerateTip => Loc.T("Im Icon-Maker Varianten für dieses Programm erzeugen (1-Klick-Generator)", "Generate variants for this program in the icon maker (one-click generator)");
    public static string AsListText => Loc.T("Als Liste", "As list");
    public static string AsTilesText => Loc.T("Als Kacheln", "As tiles");

    private sealed record FilterOption(string Label, Func<ProgramRow, bool> Match);

    private readonly MainWindow _main;
    private readonly DispatcherTimer _searchDelay;
    private List<ProgramRow> _rows = new();
    private ProgramRow? _current;
    private bool _scanning;
    private bool _loaded;

    public ProgramsPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        _searchDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); ApplyFilter(); };

        var filters = new[]
        {
            new FilterOption(Loc.T("Alle Programme", "All programs"), _ => true),
            new FilterOption(Loc.T("Mit Verknüpfung", "With shortcut"), r => r.Entry.Shortcuts.Count > 0),
            new FilterOption(Loc.T("Desktop", "Desktop"), r => r.Entry.Shortcuts.Any(s => s.Location == LinkLocation.Desktop)),
            new FilterOption(Loc.T("Startmenü", "Start menu"), r => r.Entry.Shortcuts.Any(s => s.Location == LinkLocation.StartMenu)),
            new FilterOption(Loc.T("Taskleiste", "Taskbar"), r => r.Entry.Shortcuts.Any(s => s.Location == LinkLocation.Taskbar)),
            new FilterOption(Loc.T("Windows-Apps", "Windows apps"), r => r.Entry.Kind == ProgramKind.StoreApp),
            new FilterOption(Loc.T("Ohne Verknüpfung", "Without shortcut"), r => r.Entry.Shortcuts.Count == 0),
            new FilterOption(Loc.T("Mit neuem Icon", "With new icon"), r => r.IsChanged),
            new FilterOption(Loc.T("Spiele (Steam, Epic)", "Games (Steam, Epic)"), r => r.Entry.Game is not null),
            new FilterOption(Loc.T("Icon fehlt", "Icon missing"), r => r.Entry.IconMissing && !r.IsChanged),
        };
        FilterBox.ItemsSource = filters;
        FilterBox.SelectedIndex = 0;
        SetViewMode(App.Settings.ProgramsAsTiles, save: false);

        Browser.Attach(main.Packs, App.Settings.LibraryPack, App.Settings.LibraryTileSize);
        Browser.SelectionChanged += _ => UpdateActions();
        Browser.IconActivated += _ => Apply_Click(this, new RoutedEventArgs());
        Browser.TileSizeChanged += size => { App.Settings.LibraryTileSize = size; App.Settings.Save(); };
        main.Assignments.Changed += UpdateChangedFlags;

        Suggestions.Initialize(main, () => _rows);
        Suggestions.BackRequested += ShowList;
        Suggestions.OpenProgram += row => ShowDetail(row, keepBrowser: false);
    }

    public async void OnShown()
    {
        if (!_loaded) { _loaded = true; await ScanAsync(); }
    }

    public void FocusSearch()
    {
        if (DetailView.IsVisible) Browser.FocusSearch();
        else { SearchBox.Focus(); SearchBox.SelectAll(); }
    }

    // ===================================================================== List

    private async Task ScanAsync()
    {
        if (_scanning) return;
        _scanning = true;
        ScanBusy.Visibility = Visibility.Visible;
        RescanButton.IsEnabled = false;
        string? currentKey = _current?.Entry.Key;
        try
        {
            var entries = await ProgramScanner.ScanAsync();
            _rows = entries.Where(e => !DemoMode.UsesRealIcons(e)).Select(e => new ProgramRow(e)).ToList();
            UpdateChangedFlags();
            ApplyFilter();
            // Keep the detail view on the same program after a rescan
            if (currentKey is not null && DetailView.IsVisible)
            {
                var row = _rows.FirstOrDefault(r => r.Entry.Key == currentKey)
                          ?? _rows.FirstOrDefault(r => r.Name.Equals(_current?.Name, StringComparison.CurrentCultureIgnoreCase));
                if (row is not null) ShowDetail(row, keepBrowser: true); else ShowList();
            }
        }
        catch (Exception ex) { App.LogError(ex); }
        finally
        {
            _scanning = false;
            ScanBusy.Visibility = Visibility.Collapsed;
            RescanButton.IsEnabled = true;
        }
    }

    private void UpdateChangedFlags()
    {
        var byTarget = _main.Assignments.Items.ToDictionary(a => a.Target, StringComparer.OrdinalIgnoreCase);
        foreach (var row in _rows)
        {
            var a = row.Entry.Shortcuts.Select(s => byTarget.GetValueOrDefault(s.Path)).FirstOrDefault(x => x is not null);
            bool changed = a is not null;
            string? preview = a is null ? null : PreviewOf(a);
            if (changed != row.IsChanged || preview != row.AssignedPreview)
            {
                row.AssignedPreview = preview;
                row.IsChanged = changed;
                row.ResetIcon();
            }
        }
    }

    private string? PreviewOf(Assignment a)
    {
        var pack = _main.Packs.Find(a.PackId);
        var icon = pack?.Icons.FirstOrDefault(i => i.Name == a.IconName);
        return pack is not null && icon is not null ? pack.PngPath(icon) : File.Exists(a.IcoPath) ? a.IcoPath : null;
    }

    private void ApplyFilter()
    {
        string[] terms = SearchBox.Text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filter = (FilterBox.SelectedItem as FilterOption)?.Match ?? (_ => true);
        var shown = _rows.Where(r => filter(r) && terms.All(t => r.SearchText.Contains(t))).ToList();
        var view = new ListCollectionView(shown);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ProgramRow.Group)));
        ProgramList.ItemsSource = view;
        ProgramTiles.ItemsSource = view;
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListEmpty.Visibility = shown.Count == 0 && !_scanning ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = "  ·  " + (shown.Count == _rows.Count
            ? Loc.T($"{_rows.Count:N0} Einträge", $"{_rows.Count:N0} entries")
            : Loc.T($"{shown.Count:N0} von {_rows.Count:N0} Einträgen", $"{shown.Count:N0} of {_rows.Count:N0} entries"));
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _searchDelay.Stop();
        _searchDelay.Start();
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e) => SetViewMode(TileModeButton.IsChecked == true, save: true);

    internal void ShowTilesForScreenshot(bool on) { ShowList(); SetViewMode(on, save: false); }

    private void SetViewMode(bool tiles, bool save)
    {
        ListModeButton.IsChecked = !tiles;
        TileModeButton.IsChecked = tiles;
        ProgramList.Visibility = tiles ? Visibility.Collapsed : Visibility.Visible;
        ProgramTiles.Visibility = tiles ? Visibility.Visible : Visibility.Collapsed;
        if (save && App.Settings.ProgramsAsTiles != tiles) { App.Settings.ProgramsAsTiles = tiles; App.Settings.Save(); }
    }

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private async void Rescan_Click(object sender, RoutedEventArgs e) => await ScanAsync();

    private void ProgramList_Click(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject) is { } d && ItemsControl.ContainerFromElement((ItemsControl)sender, d) is ListBoxItem item
            && item.DataContext is ProgramRow row)
            ShowDetail(row, keepBrowser: false);
    }

    private void ProgramList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ((ListBox)sender).SelectedItem is ProgramRow row) ShowDetail(row, keepBrowser: false);
    }

    // ===================================================================== Detail

    private void ShowList()
    {
        _current = null;
        DetailView.Visibility = Visibility.Collapsed;
        Suggestions.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
        (ProgramTiles.IsVisible ? ProgramTiles : ProgramList).Focus();
    }

    private void Suggest_Click(object sender, RoutedEventArgs e) => ShowSuggestions();

    /// <summary>Opens the suggestion wizard (after the scan has finished).</summary>
    internal async void ShowSuggestions()
    {
        while (_scanning) await Task.Delay(100);
        ListView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Collapsed;
        Suggestions.Visibility = Visibility.Visible;
        Suggestions.Open();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowList();

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not null) _main.GenerateInMaker(_current.Entry);
    }

    /// <summary>Esc goes back from the detail view to the list.</summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape && (DetailView.IsVisible || Suggestions.IsVisible)) { ShowList(); e.Handled = true; }
    }

    private void ShowDetail(ProgramRow row, bool keepBrowser)
    {
        _current = row;
        ListView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        Suggestions.Visibility = Visibility.Collapsed;
        DetailTitle.Text = row.Name;
        InfoBar.Visibility = keepBrowser ? InfoBar.Visibility : Visibility.Collapsed;

        var assigned = _main.Assignments.Items.Select(a => a.Target).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var shortcuts = row.Entry.Shortcuts.Select(s => new ShortcutRow(s, assigned.Contains(s.Path))).ToList();
        ShortcutList.ItemsSource = shortcuts;
        bool hasShortcuts = shortcuts.Count > 0;
        ShortcutPanel.Visibility = hasShortcuts ? Visibility.Visible : Visibility.Collapsed;
        CreatePanel.Visibility = hasShortcuts ? Visibility.Collapsed : Visibility.Visible;
        ShortcutHeader.Text = shortcuts.Count == 1
            ? Loc.T("1 Verknüpfung", "1 shortcut")
            : Loc.T($"{shortcuts.Count} Verknüpfungen, die gemeinsam geändert werden", $"{shortcuts.Count} shortcuts changed together");

        CurrentIcon.Source = null;
        ShellIconLoader.Request(row.Entry.IconSource, 96, img => { if (_current == row) CurrentIcon.Source = img; });

        if (!keepBrowser)
        {
            if (_main.TakePendingIcon() is { } pending) Browser.Preselect(pending);
            else SuggestIcons(row.Entry);
        }
        UpdateActions();
    }

    /// <summary>Pre-fills the icon search with the program (EXE name first, then the name, then single words).</summary>
    private void SuggestIcons(ProgramEntry entry)
    {
        var candidates = new List<string>();
        if (entry.ExePath is { } exe) candidates.Add(System.IO.Path.GetFileNameWithoutExtension(exe));
        candidates.Add(entry.Name);
        candidates.AddRange(entry.Name.Split([' ', '-', '_', '.', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                                      .Where(w => w.Length >= 3).OrderByDescending(w => w.Length));
        Browser.Suggest(candidates);
    }

    private IReadOnlyList<ShortcutRef> CheckedShortcuts() =>
        (ShortcutList.ItemsSource as IEnumerable<ShortcutRow>)?.Where(r => r.IsChecked).Select(r => r.Shortcut).ToList() ?? new();

    private List<Assignment> AssignmentsOfCurrent()
    {
        if (_current is null) return new();
        var paths = _current.Entry.Shortcuts.Select(s => s.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _main.Assignments.Items.Where(a => paths.Contains(a.Target)).ToList();
    }

    private void UpdateActions()
    {
        if (_current is null) return;
        var icon = Browser.Selected;
        NewIcon.Source = icon is null ? null : ImageTools.LoadThumbnail(icon.Path, 96);
        NewIconPlaceholder.Visibility = icon is null ? Visibility.Visible : Visibility.Collapsed;

        bool hasShortcuts = _current.Entry.Shortcuts.Count > 0;
        int count = hasShortcuts ? CheckedShortcuts().Count : (CreateDesktop.IsChecked == true ? 1 : 0) + (CreateStart.IsChecked == true ? 1 : 0);
        ApplyButton.IsEnabled = icon is not null && count > 0;
        RestoreButton.IsEnabled = AssignmentsOfCurrent().Count > 0;

        ActionTitle.Text = icon?.Title ?? Loc.T("Kein Icon ausgewählt", "No icon selected");
        if (icon is null) ActionText.Text = Loc.T("Wähle unten ein Icon aus.", "Choose an icon below.");
        else if (count == 0) ActionText.Text = Loc.T("Keine Verknüpfung ausgewählt.", "No shortcut selected.");
        else if (hasShortcuts)
            ActionText.Text = Loc.T(count == 1 ? $"Aus {icon.Pack.Name} · 1 Verknüpfung wird geändert, das Original wird gesichert."
                                               : $"Aus {icon.Pack.Name} · {count} Verknüpfungen werden geändert, die Originale werden gesichert.",
                                    count == 1 ? $"From {icon.Pack.Name} · 1 shortcut will be changed, the original is backed up."
                                               : $"From {icon.Pack.Name} · {count} shortcuts will be changed, the originals are backed up.");
        else
            ActionText.Text = Loc.T($"Aus {icon.Pack.Name} · {count} neue Verknüpfung(en) werden angelegt.",
                                    $"From {icon.Pack.Name} · {count} new shortcut(s) will be created.");
    }

    private void Shortcut_Toggled(object sender, RoutedEventArgs e) => UpdateActions();
    private void CreateTarget_Click(object sender, RoutedEventArgs e) => UpdateActions();

    private void RevealShortcut_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is string path) ShellHelper.RevealFile(path);
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not { } row || Browser.Selected is not { } icon || !ApplyButton.IsEnabled) return;
        SetBusy(true);
        try
        {
            BatchResult result;
            bool created = row.Entry.Shortcuts.Count == 0;
            if (created)
                result = await _main.Assignments.CreateShortcutsAsync(row.Entry, CreateDesktop.IsChecked == true, CreateStart.IsChecked == true, icon.Pack, icon.Icon);
            else
                result = await _main.Assignments.ApplyAsync(CheckedShortcuts(), row.Name, icon.Pack, icon.Icon, allowElevation: true);

            ShowResult(result, created
                ? Loc.T("Verknüpfung angelegt", "Shortcut created")
                : Loc.T("Icon zugewiesen", "Icon assigned"));
            if (created && result.Succeeded.Count > 0) await ScanAsync(); // the new shortcuts now belong to the program
            else if (_current is not null) ShowDetail(_current, keepBrowser: true);
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            ShowInfo(false, Loc.T("Zuweisen fehlgeschlagen", "Assigning failed"), ex.Message);
        }
        finally { SetBusy(false); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var assignments = AssignmentsOfCurrent();
        if (assignments.Count == 0) return;
        SetBusy(true);
        try
        {
            bool removedOwn = assignments.Any(a => a.Kind == AssignmentKind.CreatedShortcut);
            var result = await _main.Assignments.RestoreAsync(assignments, allowElevation: true);
            ShowResult(result, Loc.T("Original wiederhergestellt", "Original restored"));
            if (removedOwn) await ScanAsync();
            else if (_current is not null) ShowDetail(_current, keepBrowser: true);
        }
        catch (Exception ex)
        {
            App.LogError(ex);
            ShowInfo(false, Loc.T("Wiederherstellen fehlgeschlagen", "Restoring failed"), ex.Message);
        }
        finally { SetBusy(false); }
    }

    private void SetBusy(bool busy)
    {
        ApplyButton.IsEnabled = RestoreButton.IsEnabled = !busy;
        if (!busy) UpdateActions();
    }

    private void ShowResult(BatchResult result, string successTitle)
    {
        if (result.Failed.Count == 0)
        {
            ShowInfo(true, successTitle, Loc.T(
                "Falls ein Icon noch nicht aktualisiert ist: Auf dem Desktop F5 drücken. Angeheftete Taskleisten-Icons zeigen das neue Icon eventuell erst nach dem Neuanheften.",
                "If an icon hasn't updated yet: press F5 on the desktop. Pinned taskbar icons may only show the new icon after re-pinning."));
            return;
        }
        string details = string.Join("\n", result.Failed.Select(f => $"{System.IO.Path.GetFileName(f.Path)}: {f.Error}"));
        ShowInfo(result.Succeeded.Count > 0,
                 result.Succeeded.Count > 0
                     ? Loc.T($"{result.Succeeded.Count} erfolgreich, {result.Failed.Count} fehlgeschlagen", $"{result.Succeeded.Count} succeeded, {result.Failed.Count} failed")
                     : Loc.T("Nicht geändert", "Not changed"),
                 details, warning: result.Succeeded.Count > 0);
    }

    private void ShowInfo(bool success, string title, string text, bool warning = false)
    {
        string kind = warning ? "Caution" : success ? "Success" : "Critical";
        InfoIcon.Text = warning ? "" : success ? "" : "";
        InfoIcon.SetResourceReference(TextBlock.ForegroundProperty, $"SystemFillColor{kind}Brush");
        InfoBar.SetResourceReference(Border.BackgroundProperty, $"SystemFillColor{kind}BackgroundBrush");
        InfoTitle.Text = title;
        InfoText.Text = text;
        InfoText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        InfoBar.Visibility = Visibility.Visible;
    }

    private void CloseInfo_Click(object sender, RoutedEventArgs e) => InfoBar.Visibility = Visibility.Collapsed;

    internal async Task WaitForScanAsync()
    {
        while (!_loaded || _scanning) await Task.Delay(100);
    }

    /// <summary>Explorer context menu: opens the program that owns this shortcut file. False if the shortcut isn't one the app manages.</summary>
    public async Task<bool> OpenShortcutAsync(string path)
    {
        if (!_loaded) OnShown();
        await WaitForScanAsync();
        var row = _rows.FirstOrDefault(r => r.Entry.Shortcuts.Any(s => string.Equals(s.Path, path, StringComparison.OrdinalIgnoreCase)));
        if (row is null) return false;
        ShowDetail(row, keepBrowser: false);
        return true;
    }

    /// <summary>Screenshot mode: opens the detail view of a program with several shortcuts.</summary>
    internal void ShowDetailForScreenshot(bool show)
    {
        if (!show) { ShowList(); return; }
        // Prefer a program the packs have an icon for, so the preview shows "current -> new"
        bool HasIcon(ProgramRow r) => _main.Packs.Packs.Any(p => p.Icons.Any(i => i.Name.Equals(r.Name.Replace(' ', '_'), StringComparison.OrdinalIgnoreCase)));
        var row = _rows.FirstOrDefault(r => r.Entry.Shortcuts.Count > 1 && r.Name.Contains("Steam", StringComparison.OrdinalIgnoreCase))
                  ?? _rows.Where(r => r.Entry.Shortcuts.Count > 1).OrderByDescending(HasIcon).FirstOrDefault();
        if (row is not null) ShowDetail(row, keepBrowser: false);
    }
}
