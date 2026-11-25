using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IconWorkshop.Assignments;
using IconWorkshop.Controls;
using IconWorkshop.Imaging;
using IconWorkshop.Programs;
using IconWorkshop.Services;
using Microsoft.Win32;

namespace IconWorkshop.Views;

/// <summary>One line in the overview: an assignment, or all shortcuts of a program that got the same icon (e.g. Steam on the desktop and in the Start menu).</summary>
public sealed class AssignmentRow
{
    public IReadOnlyList<Assignment> Assignments { get; }
    public string Name { get; }
    public string Details { get; }
    public string Target { get; }
    public string Area { get; }
    public AssignmentStatus Status { get; }
    public bool HasProblem => Status != AssignmentStatus.Ok;
    public string StatusText => Status switch
    {
        AssignmentStatus.Changed => Loc.T("Überschrieben", "Overwritten"),
        AssignmentStatus.TargetMissing => Loc.T("Nicht mehr vorhanden", "No longer exists"),
        AssignmentStatus.IcoMissing => Loc.T("Icon-Datei fehlt", "Icon file missing"),
        _ => "",
    };
    private readonly string? _preview;

    public AssignmentRow(IReadOnlyList<(Assignment A, AssignmentStatus Status)> items, string? preview)
    {
        var a = items[0].A;
        Assignments = items.Select(i => i.A).ToList();
        Status = items.Select(i => i.Status).FirstOrDefault(s => s != AssignmentStatus.Ok, AssignmentStatus.Ok);
        _preview = preview;
        Area = AreaOf(a);
        Name = a.DisplayName;
        Target = string.Join("\n", Assignments.Select(x => x.FolderPath ?? (x.Kind == AssignmentKind.Registry ? "HKCU\\" + x.RegistryKey : x.Target)));
        string where = a.Kind switch
        {
            AssignmentKind.Shortcut or AssignmentKind.CreatedShortcut =>
                string.Join(" · ", Assignments.Select(x => (x.Kind == AssignmentKind.CreatedShortcut ? Loc.T("Eigene Verknüpfung ", "Own shortcut ") : "")
                    + (ProgramScanner.LocationLabelOf(x.Target) ?? System.IO.Path.GetDirectoryName(x.Target))).Distinct()),
            AssignmentKind.Folder => a.FolderPath ?? "",
            _ => Area,
        };
        Details = $"{where}  ·  {a.IconName.Replace('_', ' ')} ({a.PackName})  ·  {Assignments.Max(x => x.AppliedUtc).ToLocalTime():g}";
    }

    public static string AreaOf(Assignment a) => a.Kind switch
    {
        AssignmentKind.Shortcut or AssignmentKind.CreatedShortcut => Loc.NavPrograms,
        AssignmentKind.Folder => Loc.NavFolders,
        _ when a.Target.StartsWith("drive:") => Loc.NavDrives,
        _ when a.Target.StartsWith("type:") => Loc.NavFileTypes,
        _ => Loc.NavSystemIcons,
    };

    private ImageSource? _icon;
    private bool _loaded;
    public ImageSource? Icon
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                if (_preview is not null) _icon = ImageTools.LoadThumbnail(_preview, 64);
            }
            return _icon;
        }
    }

}

/// <summary>All assignments at a glance, re-apply, sign-in check, cache, export/import and restore.</summary>
public partial class AssignmentsPage : UserControl, IPage
{
    // ---------- Texts for XAML ----------
    public static string ReapplyTitle => Loc.T("Nach Programm-Updates", "After program updates");
    public static string ReapplyDesc => Loc.T("Manche Programme legen ihre Verknüpfung bei einem Update neu an, dann ist das Icon wieder das alte. „Erneut anwenden“ setzt alle überschriebenen Icons wieder.",
                                              "Some programs re-create their shortcut when updating, which brings back the old icon. “Re-apply” sets all overwritten icons again.");
    public static string ReapplyText => Loc.T("Erneut anwenden", "Re-apply");
    public static string SignInTitle => Loc.T("Bei der Anmeldung automatisch prüfen", "Check automatically at sign-in");
    public static string CacheTitle => Loc.T("Icons in Windows aktualisieren", "Refresh icons in Windows");
    public static string CacheDesc => Loc.T("Zeigt Windows noch alte Icons, zuerst „Aktualisieren“ versuchen. Hilft das nicht, den Cache leeren: Dafür startet der Explorer neu, Taskleiste und Fenster verschwinden kurz.",
                                            "If Windows still shows old icons, try “Refresh” first. If that doesn't help, clear the cache: Explorer restarts, the taskbar and windows disappear briefly.");
    public static string RefreshText => Loc.T("Aktualisieren", "Refresh");
    public static string ClearCacheText => Loc.T("Icon- und Miniaturansichten-Cache leeren", "Clear icon and thumbnail cache");
    public static string ProfilesTitle => Loc.T("Profile", "Profiles");
    public static string ProfilesDesc => Loc.T("Einen kompletten Look speichern und später mit einem Klick zurückholen, z. B. „Dunkel“ und „Bunt“. Beim Anwenden werden die aktuellen Zuweisungen wiederhergestellt und die des Profils gesetzt.",
                                               "Save a complete look and bring it back later with one click, e.g. “Dark” and “Colorful”. Applying restores the current assignments and sets those of the profile.");
    public static string ProfileApplyText => Loc.T("Anwenden", "Apply");
    public static string ProfileSaveText => Loc.T("Aktuellen Stand als Profil speichern …", "Save current state as profile …");
    public static string ListTitle => Loc.T("Zuweisungen", "Assignments");
    public static string ExportText => Loc.T("Exportieren", "Export");
    public static string ImportText => Loc.T("Importieren", "Import");
    public static string RestoreAllText => Loc.T("Alle wiederherstellen", "Restore all");
    public static string EmptyTitle => Loc.T("Noch keine Zuweisungen", "No assignments yet");
    public static string EmptyText => Loc.T("Sobald du einem Programm, Systemsymbol, Laufwerk, Ordner oder Dateityp ein Icon zuweist, erscheint es hier. Über „Importieren“ kannst du einen Export übernehmen.",
                                            "As soon as you assign an icon to a program, system icon, drive, folder or file type, it shows up here. Use “Import” to take over an export.");

    private sealed record FilterOption(string Label, Func<AssignmentRow, bool> Match);

    private readonly MainWindow _main;
    private List<AssignmentRow> _rows = new();

    public AssignmentsPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        FilterBox.ItemsSource = new[]
        {
            new FilterOption(Loc.T("Alle Bereiche", "All areas"), _ => true),
            new FilterOption(Loc.NavPrograms, r => r.Area == Loc.NavPrograms),
            new FilterOption(Loc.NavSystemIcons, r => r.Area == Loc.NavSystemIcons),
            new FilterOption(Loc.NavDrives, r => r.Area == Loc.NavDrives),
            new FilterOption(Loc.NavFolders, r => r.Area == Loc.NavFolders),
            new FilterOption(Loc.NavFileTypes, r => r.Area == Loc.NavFileTypes),
            new FilterOption(Loc.T("Nur Probleme", "Problems only"), r => r.HasProblem),
        };
        FilterBox.SelectedIndex = 0;
        main.Assignments.Changed += () => Dispatcher.BeginInvoke(Reload);
    }

    public void OnShown()
    {
        FillProfiles();
        SignInSwitch.IsChecked = SignInCheck.IsEnabled;
        UpdateSignInDesc();
        Reload();
    }

    private void Reload()
    {
        var store = _main.Assignments;
        _rows = store.Items
            .GroupBy(a => a.Kind is AssignmentKind.Shortcut or AssignmentKind.CreatedShortcut
                ? $"p|{a.DisplayName}|{a.PackId}|{a.IconName}" : "t|" + a.Target, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AssignmentRow(g.Select(a => (a, store.GetStatus(a))).ToList(), PreviewOf(g.First())))
            .OrderBy(r => r.Area).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        ApplyFilter();
        UpdateProfileButtons();
    }

    private string? PreviewOf(Assignment a)
    {
        var pack = _main.Packs.Find(a.PackId);
        var icon = pack?.Icons.FirstOrDefault(i => i.Name == a.IconName);
        return pack is not null && icon is not null ? pack.PngPath(icon) : File.Exists(a.IcoPath) ? a.IcoPath : null;
    }

    private void ApplyFilter()
    {
        var filter = (FilterBox.SelectedItem as FilterOption)?.Match ?? (_ => true);
        var shown = _rows.Where(filter).ToList();
        var view = new System.Windows.Data.ListCollectionView(shown);
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(AssignmentRow.Area)));
        List.ItemsSource = view;
        int problems = _rows.Count(r => r.HasProblem), total = _main.Assignments.Items.Count;
        CountText.Text = "  ·  " + Loc.T($"{total} Zuweisung(en)", $"{total} assignment(s)")
                         + (problems > 0 ? Loc.T($" · {problems} mit Problem", $" · {problems} with a problem") : "");
        EmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        List.Visibility = _rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RestoreAllButton.IsEnabled = ExportButton.IsEnabled = _rows.Count > 0;
    }

    private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    // ---------- Actions ----------

    private async Task RunAsync(Func<Task> work)
    {
        Busy.Visibility = Visibility.Visible;
        IsEnabled = false;
        try { await work(); }
        catch (Exception ex) { App.LogError(ex); ShowInfo(false, Loc.T("Fehler", "Error"), ex.Message); }
        finally
        {
            Busy.Visibility = Visibility.Collapsed;
            IsEnabled = true;
            Reload();
        }
    }

    private async void Reapply_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var r = await _main.Assignments.ReapplyAsync(_main.Packs, allowElevation: true);
        if (r.Failed.Count == 0)
            ShowInfo(true, r.Repaired == 0 ? Loc.T("Alles in Ordnung", "Everything is fine") : Loc.T($"{r.Repaired} Icon(s) erneut angewendet", $"{r.Repaired} icon(s) re-applied"),
                     Loc.T($"{r.Checked} Zuweisung(en) geprüft.", $"{r.Checked} assignment(s) checked."));
        else
            ShowInfo(false, Loc.T($"{r.Repaired} erneut angewendet, {r.Failed.Count} nicht möglich", $"{r.Repaired} re-applied, {r.Failed.Count} not possible"),
                     string.Join("\n", r.Failed.Select(f => $"{f.Name}: {f.Error}")), warning: true);
    });

    private void SignInSwitch_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SignInSwitch.IsChecked == true) SignInCheck.Enable(); else SignInCheck.Disable();
        }
        catch (Exception ex) { ShowInfo(false, Loc.T("Nicht möglich", "Not possible"), ex.Message); }
        SignInSwitch.IsChecked = SignInCheck.IsEnabled;
        UpdateSignInDesc();
    }

    private void UpdateSignInDesc()
    {
        string desc = Loc.T("Prüft beim Anmelden einmal still alle Zuweisungen und setzt überschriebene Icons wieder. Verknüpfungen aller Benutzer brauchen dafür Adminrechte und werden nur gemeldet.",
                            "Silently checks all assignments once at sign-in and sets overwritten icons again. Shortcuts of all users need admin rights for this and are only reported.");
        try
        {
            if (File.Exists(SignInCheck.LogFile))
                desc += "\n" + Loc.T("Letzte Prüfung: ", "Last check: ") + File.ReadLines(SignInCheck.LogFile).First();
        }
        catch { }
        SignInDesc.Text = desc;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        IconCache.Refresh();
        ShowInfo(true, Loc.T("Windows wurde benachrichtigt", "Windows was notified"),
                 Loc.T("Falls ein Icon weiterhin alt aussieht, auf dem Desktop F5 drücken oder den Cache leeren.", "If an icon still looks old, press F5 on the desktop or clear the cache."));
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var answer = FluentDialog.Show(_main, Loc.T("Icon-Cache leeren?", "Clear icon cache?"),
            Loc.T("Der Explorer wird kurz beendet und neu gestartet. Taskleiste, Desktop und geöffnete Explorer-Fenster verschwinden dabei für ein paar Sekunden; Explorer-Fenster werden nicht wieder geöffnet. Andere Programme laufen weiter.",
                  "Explorer is ended briefly and restarted. The taskbar, desktop and open Explorer windows disappear for a few seconds; Explorer windows are not reopened. Other programs keep running."),
            Loc.T("Cache leeren", "Clear cache"), close: Loc.Cancel, glyph: FluentDialog.GlyphWarning);
        if (answer != DialogResultKind.Primary) return;
        await RunAsync(async () =>
        {
            int deleted = await IconCache.ClearAsync();
            ShowInfo(true, Loc.T("Cache geleert", "Cache cleared"), Loc.T($"{deleted} Cache-Datei(en) gelöscht, der Explorer läuft wieder.", $"{deleted} cache file(s) deleted, Explorer is running again."));
        });
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = ExportText,
            FileName = $"Icon-Workshop-Export-{DateTime.Now:yyyy-MM-dd}.zip",
            Filter = Loc.T("ZIP-Archiv (*.zip)|*.zip", "ZIP archive (*.zip)|*.zip"),
        };
        if (dlg.ShowDialog(_main) != true) return;
        await RunAsync(async () =>
        {
            int n = await _main.Assignments.ExportAsync(dlg.FileName);
            ShowInfo(true, Loc.T($"{n} Zuweisung(en) exportiert", $"{n} assignment(s) exported"), dlg.FileName);
        });
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = ImportText, Filter = Loc.T("ZIP-Archiv (*.zip)|*.zip", "ZIP archive (*.zip)|*.zip") };
        if (dlg.ShowDialog(_main) != true) return;
        await RunAsync(async () =>
        {
            var r = await _main.Assignments.ImportAsync(dlg.FileName, _main.Packs);
            if (r.Failed.Count == 0) ShowInfo(true, Loc.T($"{r.Succeeded.Count} Zuweisung(en) übernommen", $"{r.Succeeded.Count} assignment(s) imported"), "");
            else ShowInfo(false, Loc.T($"{r.Succeeded.Count} übernommen, {r.Failed.Count} übersprungen", $"{r.Succeeded.Count} imported, {r.Failed.Count} skipped"),
                          string.Join("\n", r.Failed.Select(f => $"{f.Path}: {f.Error}")), warning: true);
        });
    }

    private async void RestoreOne_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AssignmentRow row) return;
        await RunAsync(async () =>
        {
            var r = await _main.Assignments.RestoreAsync(row.Assignments.ToList(), allowElevation: true);
            if (r.Failed.Count == 0) ShowInfo(true, Loc.T("Original wiederhergestellt", "Original restored"), row.Name);
            else ShowInfo(false, Loc.T("Nicht wiederhergestellt", "Not restored"), string.Join("\n", r.Failed.Select(f => $"{f.Path}: {f.Error}")));
        });
    }

    private async void RestoreAll_Click(object sender, RoutedEventArgs e)
    {
        var all = _main.Assignments.Items.ToList();
        if (all.Count == 0) return;
        var answer = FluentDialog.Show(_main, Loc.T("Alles wiederherstellen?", "Restore everything?"),
            Loc.T($"Alle {all.Count} Zuweisungen werden rückgängig gemacht: Originale zurück, eigene Verknüpfungen entfernt.",
                  $"All {all.Count} assignments are undone: originals back, own shortcuts removed."),
            RestoreAllText, close: Loc.Cancel, glyph: FluentDialog.GlyphWarning);
        if (answer != DialogResultKind.Primary) return;
        await RunAsync(async () =>
        {
            var r = await _main.Assignments.RestoreAsync(all, allowElevation: true);
            if (r.Failed.Count == 0) ShowInfo(true, Loc.T($"{r.Succeeded.Count} wiederhergestellt", $"{r.Succeeded.Count} restored"), "");
            else ShowInfo(false, Loc.T($"{r.Succeeded.Count} wiederhergestellt, {r.Failed.Count} fehlgeschlagen", $"{r.Succeeded.Count} restored, {r.Failed.Count} failed"),
                          string.Join("\n", r.Failed.Select(f => $"{f.Path}: {f.Error}")), warning: true);
        });
    }

    // ---------- Profiles ----------

    private void FillProfiles(string? select = null)
    {
        var all = Profiles.All();
        ProfileBox.ItemsSource = all;
        ProfileBox.SelectedItem = all.FirstOrDefault(p => p.Name == select) ?? all.FirstOrDefault();
        UpdateProfileButtons();
    }

    private void UpdateProfileButtons()
    {
        ProfileApplyButton.IsEnabled = ProfileDeleteButton.IsEnabled = ProfileBox.SelectedItem is Profiles.Profile;
        ProfileBox.IsEnabled = ProfileBox.Items.Count > 0;
        ProfileSaveButton.IsEnabled = _main.Assignments.Items.Count > 0;
    }

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateProfileButtons();

    private async void ProfileSave_Click(object sender, RoutedEventArgs e)
    {
        string? name = FluentDialog.Prompt(_main, Loc.T("Profil speichern", "Save profile"),
            Loc.T("Name des Profils, z. B. „Dunkel“ oder „Gaming“. Ein Profil mit gleichem Namen wird ersetzt.", "Name of the profile, e.g. “Dark” or “Gaming”. A profile with the same name is replaced."),
            (ProfileBox.SelectedItem as Profiles.Profile)?.Name ?? "", Loc.T("Speichern", "Save"));
        if (string.IsNullOrWhiteSpace(name)) return;
        string file = Profiles.FileFor(name);
        await RunAsync(async () =>
        {
            int n = await _main.Assignments.ExportAsync(file);
            ShowInfo(true, Loc.T($"Profil „{name.Trim()}“ gespeichert", $"Profile “{name.Trim()}” saved"), Loc.T($"{n} Zuweisung(en).", $"{n} assignment(s)."));
        });
        FillProfiles(System.IO.Path.GetFileNameWithoutExtension(file));
    }

    private async void ProfileApply_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not Profiles.Profile profile) return;
        var current = _main.Assignments.Items.ToList();
        var answer = FluentDialog.Show(_main, Loc.T($"Profil „{profile.Name}“ anwenden?", $"Apply profile “{profile.Name}”?"),
            Loc.T($"Die {current.Count} aktuellen Zuweisungen werden wiederhergestellt (Originale zurück), danach werden die Icons des Profils gesetzt. Speichere den aktuellen Stand vorher als Profil, wenn du ihn behalten willst.",
                  $"The {current.Count} current assignments are restored (originals back), then the profile's icons are set. Save the current state as a profile first if you want to keep it."),
            ProfileApplyText, close: Loc.Cancel, glyph: FluentDialog.GlyphWarning);
        if (answer != DialogResultKind.Primary) return;
        await RunAsync(async () =>
        {
            var restored = current.Count > 0 ? await _main.Assignments.RestoreAsync(current, allowElevation: true) : null;
            var r = await _main.Assignments.ImportAsync(profile.File, _main.Packs);
            var failed = (restored?.Failed.Select(f => $"{f.Path}: {f.Error}") ?? []).Concat(r.Failed.Select(f => $"{f.Path}: {f.Error}")).ToList();
            if (failed.Count == 0) ShowInfo(true, Loc.T($"Profil „{profile.Name}“ angewendet", $"Profile “{profile.Name}” applied"), Loc.T($"{r.Succeeded.Count} Zuweisung(en) gesetzt.", $"{r.Succeeded.Count} assignment(s) set."));
            else ShowInfo(false, Loc.T($"Profil angewendet, {failed.Count} nicht möglich", $"Profile applied, {failed.Count} not possible"), string.Join("\n", failed.Take(8)), warning: true);
        });
    }

    private void ProfileDelete_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not Profiles.Profile profile) return;
        var answer = FluentDialog.Show(_main, Loc.T($"Profil „{profile.Name}“ löschen?", $"Delete profile “{profile.Name}”?"),
            Loc.T("Nur das gespeicherte Profil wird gelöscht; die aktuellen Icons bleiben, wie sie sind.", "Only the saved profile is deleted; the current icons stay as they are."),
            Loc.Delete, close: Loc.Cancel, glyph: FluentDialog.GlyphWarning);
        if (answer != DialogResultKind.Primary) return;
        try { Profiles.Delete(profile); } catch (Exception ex) { ShowInfo(false, Loc.T("Nicht möglich", "Not possible"), ex.Message); }
        FillProfiles();
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
}
