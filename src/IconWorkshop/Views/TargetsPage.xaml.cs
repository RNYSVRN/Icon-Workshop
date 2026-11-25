using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IconWorkshop.Assignments;
using IconWorkshop.Imaging;
using IconWorkshop.Native;
using IconWorkshop.Targets;

namespace IconWorkshop.Views;

/// <summary>A system icon or drive in the list.</summary>
public sealed class TargetRow(IconTarget target) : INotifyPropertyChanged
{
    public IconTarget Target { get; } = target;
    public string Name => Target.Name;
    public string Subtitle => IconWorkshop.Services.DemoMode.Mask(Target.Subtitle);
    public string ChangedLabel => Loc.T("Neues Icon", "New icon");
    public string? AssignedPreview { get; set; }

    private bool _isChanged;
    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; Raise(nameof(IsChanged)); }
    }

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
                else ShellIconLoader.Request(() => TargetsPage.LoadCurrentIcon(Target, 64), img => { _icon = img; Raise(nameof(Icon)); });
            }
            return _icon;
        }
    }

    public void ResetIcon() { _icon = null; _requested = false; Raise(nameof(Icon)); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Per-user icon targets from the registry: system icons and drives. List plus detail with icon picker.</summary>
public partial class TargetsPage : UserControl, IPage
{
    private readonly MainWindow _main;
    private readonly Func<List<IconTarget>> _load;
    private List<TargetRow> _rows = new();
    private TargetRow? _current;
    private bool _loaded;

    private TargetsPage(MainWindow main, string title, string subtitle, string? note, Func<List<IconTarget>> load)
    {
        InitializeComponent();
        _main = main;
        _load = load;
        PageTitleText.Text = title;
        PageSubtitleText.Text = subtitle;
        BreadcrumbBack.Content = title;
        if (note is not null) { NoteText.Text = note; NoteBar.Visibility = Visibility.Visible; }

        Browser.Attach(main.Packs, App.Settings.LibraryPack, App.Settings.LibraryTileSize);
        Browser.SelectionChanged += _ => UpdateActions();
        Browser.IconActivated += _ => Apply_Click(this, new RoutedEventArgs());
        Browser.TileSizeChanged += size => { App.Settings.LibraryTileSize = size; App.Settings.Save(); };
        main.Assignments.Changed += UpdateChangedFlags;
    }

    // ---------- Pages ----------

    public static TargetsPage SystemIconsPage(MainWindow main) => new(main, Loc.NavSystemIcons, Loc.SystemIconsSubtitle,
        Loc.T("Die Änderungen gelten nur für dein Benutzerkonto und brauchen keine Administratorrechte. Ob ein Symbol auf dem Desktop angezeigt wird, stellst du unter Windows-Einstellungen > Personalisierung > Designs > Desktopsymboleinstellungen ein.",
              "Changes apply only to your user account and need no admin rights. Whether an icon is shown on the desktop is set in Windows Settings > Personalization > Themes > Desktop icon settings."),
        SystemIcons.All);

    public static TargetsPage DrivesPage(MainWindow main)
    {
        var extra = new List<char>();
        var page = new TargetsPage(main, Loc.NavDrives, Loc.DrivesSubtitle,
            Loc.T("Das Icon gilt für den Laufwerksbuchstaben, nur für dein Benutzerkonto und ohne Administratorrechte. Für USB-Sticks: einfach den Buchstaben wählen, unter dem der Stick erscheint.",
                  "The icon applies to the drive letter, only for your user account and without admin rights. For USB sticks: simply pick the letter the stick shows up as."),
            () => DriveIcons.All(extra));

        // Toolbar: add a letter that is not connected right now
        var letters = new ComboBox { Width = 90, Margin = new Thickness(0, 0, 8, 0) };
        var add = new Button { Content = Loc.T("Laufwerksbuchstaben hinzufügen", "Add drive letter") };
        var refresh = new Button { Margin = new Thickness(8, 0, 0, 0), Content = ProgramsPage.RescanText };
        void FillLetters()
        {
            var used = page._rows.Select(r => char.ToUpperInvariant(r.Target.Id[^1])).ToHashSet();
            letters.ItemsSource = Enumerable.Range('D', 23).Select(c => (char)c).Where(c => !used.Contains(c)).Select(c => $"{c}:").ToList();
            letters.SelectedIndex = 0;
        }
        add.Click += (_, _) =>
        {
            if (letters.SelectedItem is string s) { extra.Add(s[0]); page.Reload(); FillLetters(); }
        };
        refresh.Click += (_, _) => { page.Reload(); FillLetters(); };
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
        bar.Children.Add(letters);
        bar.Children.Add(add);
        bar.Children.Add(refresh);
        page.ToolbarHost.Content = bar;
        page.Loaded += (_, _) => FillLetters();
        return page;
    }

    public static TargetsPage FoldersPage(MainWindow main)
    {
        var page = new TargetsPage(main, Loc.NavFolders, Loc.FoldersSubtitle,
            Loc.T("Das Icon wird in einer versteckten Datei desktop.ini im Ordner gespeichert, wie es Windows selbst macht. Eine vorhandene desktop.ini wird vorher gesichert und beim Wiederherstellen zurückgeschrieben.",
                  "The icon is stored in a hidden desktop.ini file in the folder, the same way Windows does it. An existing desktop.ini is backed up first and written back when restoring."),
            () => FolderIcons.All(App.Settings.CustomFolders, main.Assignments.Items.Select(a => a.Target)));
        var add = new Button();
        add.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new TextBlock { Text = "", FontFamily = (FontFamily)Application.Current.FindResource("IconFont"), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = Loc.T("Ordner hinzufügen", "Add folder") },
            },
        };
        add.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = Loc.T("Ordner wählen", "Choose folder") };
            if (dlg.ShowDialog(main) != true) return;
            page.AddFolderAndShow(dlg.FolderName);
        };
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
        bar.Children.Add(add);
        page.ToolbarHost.Content = bar;
        return page;
    }

    public static TargetsPage FileTypesPage(MainWindow main)
    {
        var page = new TargetsPage(main, Loc.NavFileTypes, Loc.FileTypesSubtitle,
            Loc.T("Das Icon wird für das Programm gesetzt, das den Dateityp derzeit öffnet, nur für dein Benutzerkonto. Wählst du später ein anderes Standardprogramm, zeigt Windows wieder dessen Icon; dann einfach erneut zuweisen.",
                  "The icon is set for the program that currently opens the file type, only for your user account. If you later choose another default app, Windows shows its icon again; then simply assign again."),
            () => FileTypeIcons.All(App.Settings.CustomFileTypes, main.Assignments.Items.Select(a => a.Target)));
        var box = new TextBox { Width = 140, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
        var hint = new TextBlock { Text = Loc.T("z. B. .epub", "e.g. .epub"), IsHitTestVisible = false, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
        box.TextChanged += (_, _) => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var add = new Button { Content = Loc.T("Weiteren Dateityp hinzufügen", "Add more file types") };
        void Add()
        {
            string ext = box.Text.Trim().ToLowerInvariant();
            if (ext.Length > 0 && ext[0] != '.') ext = "." + ext;
            if (!FileTypeIcons.IsValidExtension(ext)) { box.Focus(); box.SelectAll(); return; }
            if (!App.Settings.CustomFileTypes.Contains(ext) && !FileTypeIcons.Curated.Contains(ext))
            {
                App.Settings.CustomFileTypes.Add(ext);
                App.Settings.Save();
            }
            box.Text = "";
            page.Reload();
            page.ShowById("type:" + ext);
        }
        add.Click += (_, _) => Add();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Add(); };
        var boxHost = new Grid();
        boxHost.Children.Add(box);
        boxHost.Children.Add(hint);
        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 12), LastChildFill = false };
        bar.Children.Add(boxHost);
        bar.Children.Add(add);
        page.ToolbarHost.Content = bar;
        return page;
    }

    /// <summary>Folders page: adds a folder to the list (remembered) and opens its detail view.</summary>
    public void AddFolderAndShow(string folder)
    {
        if (!App.Settings.CustomFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            App.Settings.CustomFolders.Add(folder);
            App.Settings.Save();
        }
        _loaded = true;
        Reload();
        ShowById(FolderIcons.IdOf(folder));
    }

    /// <summary>Opens the detail view of a target by its ID (e.g. a drive from the Explorer context menu).</summary>
    public void OpenById(string id)
    {
        if (!_loaded) { _loaded = true; Reload(); }
        ShowById(id);
    }

    /// <summary>Opens the detail view of a target by its ID (after adding it).</summary>
    private void ShowById(string id)
    {
        var row = _rows.FirstOrDefault(r => r.Target.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (row is not null) ShowDetail(row, keepBrowser: false);
    }

    // ---------- List ----------

    public void OnShown()
    {
        if (!_loaded) { _loaded = true; Reload(); }
    }

    public void FocusSearch()
    {
        if (DetailView.IsVisible) Browser.FocusSearch();
    }

    private void Reload()
    {
        _rows = _load().Select(t => new TargetRow(t)).ToList();
        UpdateChangedFlags();
        TargetList.ItemsSource = _rows;
    }

    private void UpdateChangedFlags()
    {
        foreach (var row in _rows)
        {
            var a = _main.Assignments.Find(row.Target.Id);
            string? preview = a is null ? null : PreviewOf(a);
            if (row.IsChanged != (a is not null) || row.AssignedPreview != preview)
            {
                row.IsChanged = a is not null;
                row.AssignedPreview = preview;
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

    /// <summary>The icon Windows currently shows for a target: a custom value of this user, Windows' default location or the shell icon.</summary>
    internal static ImageSource? LoadCurrentIcon(IconTarget t, int size)
    {
        if (t.CurrentValue is { Length: > 0 } custom && ShellNative.IconFromLocation(custom, size) is { } c) return c;
        if (t.Extension is { } ext) return ShellNative.IconForExtension(ext);
        if (t.DefaultLocation is { Length: > 0 } def && ShellNative.IconFromLocation(def, size) is { } d) return d;
        return t.ShellSource is not null ? ShellNative.GetItemImage(t.ShellSource, size) : null;
    }

    private void TargetList_Click(object sender, MouseButtonEventArgs e)
    {
        if ((e.OriginalSource as DependencyObject) is { } d && ItemsControl.ContainerFromElement(TargetList, d) is ListBoxItem item
            && item.DataContext is TargetRow row)
            ShowDetail(row, keepBrowser: false);
    }

    private void TargetList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && TargetList.SelectedItem is TargetRow row) ShowDetail(row, keepBrowser: false);
    }

    // ---------- Detail ----------

    private void ShowList()
    {
        _current = null;
        DetailView.Visibility = Visibility.Collapsed;
        ListView.Visibility = Visibility.Visible;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ShowList();

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape && DetailView.IsVisible) { ShowList(); e.Handled = true; }
    }

    private void ShowDetail(TargetRow row, bool keepBrowser)
    {
        _current = row;
        ListView.Visibility = Visibility.Collapsed;
        DetailView.Visibility = Visibility.Visible;
        if (!keepBrowser) InfoBar.Visibility = Visibility.Collapsed;
        DetailTitle.Text = row.Name;
        DetailSubtitle.Text = row.Subtitle;
        var a = _main.Assignments.Find(row.Target.Id);
        DetailNote.Text = row.Target.Unsupported is { } why ? why
            : a is null
            ? Loc.T("Das bisherige Icon wird vor der Änderung gemerkt.", "The previous icon is remembered before the change.")
            : Loc.T($"Aktuell: „{a.IconName.Replace('_', ' ')}“ aus {a.PackName}", $"Current: “{a.IconName.Replace('_', ' ')}” from {a.PackName}");

        CurrentIcon.Source = a is not null && PreviewOf(a) is { } p ? ImageTools.LoadThumbnail(p, 96) : null;
        if (CurrentIcon.Source is null)
            ShellIconLoader.Request(() => LoadCurrentIcon(row.Target, 96), img => { if (_current == row) CurrentIcon.Source = img; });

        if (!keepBrowser)
        {
            if (_main.TakePendingIcon() is { } pending) Browser.Preselect(pending);
            else Browser.Suggest(row.Target.SearchTerms);
        }
        UpdateActions();
    }

    private void UpdateActions()
    {
        if (_current is null) return;
        var icon = Browser.Selected;
        NewIcon.Source = icon is null ? null : ImageTools.LoadThumbnail(icon.Path, 96);
        NewIconPlaceholder.Visibility = icon is null ? Visibility.Visible : Visibility.Collapsed;
        bool unsupported = _current.Target.Unsupported is not null;
        ApplyButton.IsEnabled = icon is not null && !unsupported;
        RestoreButton.IsEnabled = _main.Assignments.Find(_current.Target.Id) is not null;
        ActionTitle.Text = icon?.Title ?? Loc.T("Kein Icon ausgewählt", "No icon selected");
        ActionText.Text = unsupported ? _current.Target.Unsupported!
            : icon is null ? Loc.T("Wähle unten ein Icon aus.", "Choose an icon below.")
            : _current.Target.FolderPath is not null
                ? Loc.T($"Aus {icon.Pack.Name} · die bisherige desktop.ini wird gesichert.", $"From {icon.Pack.Name} · the existing desktop.ini is backed up.")
                : Loc.T($"Aus {icon.Pack.Name} · gilt nur für dein Benutzerkonto.", $"From {icon.Pack.Name} · applies only to your user account.");
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not { } row || Browser.Selected is not { } icon || !ApplyButton.IsEnabled) return;
        ApplyButton.IsEnabled = false;
        try
        {
            var result = await _main.Assignments.ApplyTargetsAsync([row.Target], icon.Pack, icon.Icon);
            ShowResult(result, Loc.T("Icon zugewiesen", "Icon assigned"));
            ShowDetail(row, keepBrowser: true);
        }
        catch (Exception ex) { App.LogError(ex); ShowInfo(false, Loc.T("Zuweisen fehlgeschlagen", "Assigning failed"), ex.Message); }
        finally { UpdateActions(); }
    }

    /// <summary>One-click generator with the matching category: drive letter on a drive, extension on a page, a folder ...</summary>
    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not { } row) return;
        var t = row.Target;
        if (t.Id.StartsWith("drive:")) _main.GenerateInMaker($"{t.Id[6..].ToUpperInvariant()}: {t.Name}", IconWorkshop.Imaging.IconCategories.Drive);
        else if (t.Extension is { } ext) _main.GenerateInMaker(ext, IconWorkshop.Imaging.IconCategories.FileType);
        else if (t.FolderPath is not null) _main.GenerateInMaker(t.Name, IconWorkshop.Imaging.IconCategories.Folder);
        else _main.GenerateInMaker(t.Name, IconWorkshop.Imaging.IconCategories.System);
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not { } row || _main.Assignments.Find(row.Target.Id) is not { } a) return;
        RestoreButton.IsEnabled = false;
        try
        {
            var result = await _main.Assignments.RestoreAsync([a], allowElevation: false);
            ShowResult(result, Loc.T("Original wiederhergestellt", "Original restored"));
            ShowDetail(row, keepBrowser: true);
        }
        catch (Exception ex) { App.LogError(ex); ShowInfo(false, Loc.T("Wiederherstellen fehlgeschlagen", "Restoring failed"), ex.Message); }
        finally { UpdateActions(); }
    }

    private void ShowResult(BatchResult result, string successTitle)
    {
        if (result.Failed.Count == 0)
            ShowInfo(true, successTitle, Loc.T("Falls das Icon noch nicht aktualisiert ist: Auf dem Desktop bzw. im Explorer F5 drücken. Manchmal hilft erst „Icon-Cache leeren“ (folgt in Etappe 5) oder eine neue Anmeldung.",
                                               "If the icon hasn't updated yet: press F5 on the desktop or in Explorer. Sometimes only “Clear icon cache” (coming in stage 5) or signing in again helps."));
        else
            ShowInfo(false, Loc.T("Nicht geändert", "Not changed"), string.Join("\n", result.Failed.Select(f => $"{f.Path}: {f.Error}")));
    }

    private void ShowInfo(bool success, string title, string text)
    {
        string kind = success ? "Success" : "Critical";
        InfoIcon.Text = success ? "" : "";
        InfoIcon.SetResourceReference(TextBlock.ForegroundProperty, $"SystemFillColor{kind}Brush");
        InfoBar.SetResourceReference(Border.BackgroundProperty, $"SystemFillColor{kind}BackgroundBrush");
        InfoTitle.Text = title;
        InfoText.Text = text;
        InfoText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        InfoBar.Visibility = Visibility.Visible;
    }

    private void CloseInfo_Click(object sender, RoutedEventArgs e) => InfoBar.Visibility = Visibility.Collapsed;

    /// <summary>Screenshot mode: open the detail of the first target.</summary>
    internal void ShowFirstDetail(bool show)
    {
        if (!show) { ShowList(); return; }
        if (_rows.Count > 0) ShowDetail(_rows[0], keepBrowser: false);
    }
}
