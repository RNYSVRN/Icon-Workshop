using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IconWorkshop.Assignments;
using IconWorkshop.Imaging;
using IconWorkshop.Packs;
using IconWorkshop.Programs;

namespace IconWorkshop.Views;

/// <summary>
/// A program with its suggested icon and a checkbox: either an icon of the pack (name match) or, for programs
/// the pack has no icon for, the program's own icon generated in the pack's style ("fill gaps").
/// </summary>
public sealed class SuggestionRow : INotifyPropertyChanged
{
    public ProgramRow Program { get; }
    public IconSuggestion? Suggestion { get; }
    public IconPack Pack { get; }
    public bool AlreadyChanged { get; }
    public bool IsGenerated => Suggestion is null;
    public string Name => Program.Name;
    public string Details => IsGenerated
        ? Loc.T($"Eigenes Icon im Stil von {Pack.Name}", $"Own icon in the style of {Pack.Name}") + $"  ·  {Program.Subtitle}"
        : $"{Suggestion!.Icon.Title}  ·  {Program.Subtitle}";
    public string ConfidenceText => IsGenerated ? Loc.T("Erzeugt", "Generated")
        : Suggestion!.IsConfident ? Loc.T("Sicherer Treffer", "Confident match") : Loc.T("Vermutlich", "Likely");
    public string Group => IsGenerated
        ? Loc.T("Ohne Icon im Pack: im Stil des Packs erzeugt", "No icon in the pack: generated in the pack's style")
        : Loc.T("Aus dem Pack", "From the pack");
    public Visibility GeneratedVisibility => IsGenerated ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Generated rows: the design, ready once the program icon is loaded.</summary>
    public MakerDesign? Design { get; private set; }

    public SuggestionRow(ProgramRow program, IconSuggestion? suggestion, IconPack pack, bool alreadyChanged)
    {
        Program = program;
        Suggestion = suggestion;
        Pack = pack;
        AlreadyChanged = alreadyChanged;
        _isChecked = suggestion is { IsConfident: true } && !alreadyChanged;
    }

    private ImageSource? _newIcon;
    public ImageSource? NewIcon => _newIcon ??= Suggestion is null ? null : ImageTools.LoadThumbnail(Suggestion.Pack.PngPath(Suggestion.Icon), 64);

    public void SetGenerated(MakerDesign design, ImageSource preview)
    {
        Design = design;
        _newIcon = preview;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NewIcon)));
    }

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Suggestion wizard: finds an icon for every program with shortcuts by name, shows the list with
/// checkboxes (confident matches pre-selected) and applies the selection in one step.
/// </summary>
public partial class SuggestionsView : UserControl
{
    public static string TitleText => Loc.T("Automatische Vorschläge", "Automatic suggestions");
    public static string SubtitleText => Loc.T("Passende Icons für deine Programme aus einem Icon-Pack. Sichere Treffer sind vorausgewählt; prüfe die Liste und übernimm sie in einem Schritt.",
                                               "Matching icons for your programs from an icon pack. Confident matches are pre-selected; check the list and apply it in one step.");
    public static string HideChangedText => Loc.T("Programme mit neuem Icon ausblenden", "Hide programs that already have a new icon");
    public static string SelectAllText => Loc.T("Alle auswählen", "Select all");
    public static string SelectNoneText => Loc.T("Keine", "None");
    public static string ApplyText => Loc.T("Ausgewählte übernehmen", "Apply selected");
    public static string OtherIconText => Loc.T("Anderes Icon wählen", "Choose another icon");
    public static string FillGapsText => Loc.T("Lücken füllen", "Fill gaps");
    public static string FillGapsTip => Loc.T("Programme ohne Icon im Pack bekommen ihr eigenes Icon im Stil des Packs (nicht vorausgewählt)", "Programs without an icon in the pack get their own icon in the pack's style (not pre-selected)");
    public static string TuneText => Loc.T("Im 1-Klick-Generator anpassen", "Adjust in the one-click generator");

    private MainWindow? _main;
    private Func<IReadOnlyList<ProgramRow>>? _programs;
    private List<SuggestionRow> _all = new();

    public event Action? BackRequested;
    public event Action<ProgramRow>? OpenProgram;
    /// <summary>Icons were applied (the program list should refresh its flags).</summary>
    public event Action? Applied;

    public SuggestionsView() => InitializeComponent();

    public void Initialize(MainWindow main, Func<IReadOnlyList<ProgramRow>> programs)
    {
        _main = main;
        _programs = programs;
    }

    /// <summary>Called when the view is opened: fills the pack list and builds the suggestions.</summary>
    public void Open()
    {
        if (_main is null) return;
        var packs = _main.Packs.Packs.ToList();
        var previous = (PackBox.SelectedItem as IconPack)?.Id ?? App.Settings.LibraryPack;
        PackBox.ItemsSource = packs;
        PackBox.SelectedItem = packs.FirstOrDefault(p => p.Id == previous) ?? packs.FirstOrDefault();
        InfoBar.Visibility = Visibility.Collapsed;
        Build();
    }

    private void Build()
    {
        if (_main is null || _programs is null) return;
        if (PackBox.SelectedItem is not IconPack pack)
        {
            _all = new();
            ShowList(Loc.T("Importiere zuerst ein Icon-Pack.", "Import an icon pack first."));
            return;
        }
        var matcher = new IconMatcher(pack);
        var withShortcuts = _programs().Where(r => r.Entry.Shortcuts.Count > 0).Select(r => (Row: r, S: matcher.Suggest(r.Entry))).ToList();
        _all = withShortcuts
            .Where(x => x.S is not null)
            .Select(x => new SuggestionRow(x.Row, x.S, pack, x.Row.IsChanged))
            .OrderByDescending(r => r.Suggestion!.IsConfident).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (FillGaps.IsChecked == true)
        {
            var gaps = withShortcuts.Where(x => x.S is null)
                .Select(x => new SuggestionRow(x.Row, null, pack, x.Row.IsChanged))
                .OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            _all.AddRange(gaps);
            GenerateGaps(gaps, pack);
        }
        ShowList(Loc.T("Für deine Programme wurden in diesem Pack keine passenden Icons gefunden. In der Programmliste kannst du Icons auch von Hand wählen.",
                       "No matching icons were found in this pack for your programs. You can still choose icons by hand on the program list."));
    }

    /// <summary>Loads the program icons in the background (shell thread) and puts each one into the pack's style.</summary>
    private static void GenerateGaps(List<SuggestionRow> rows, IconPack pack)
    {
        var style = PackStyle.For(pack);
        foreach (var row in rows)
        {
            byte[]? png = null;
            var entry = row.Program.Entry;
            ShellIconLoader.Request(() => { if (!entry.IconMissing) png = IconMakerPage.ProgramPng(entry); return null; }, _ =>
            {
                if (png is null && !entry.IconMissing) return;
                try
                {
                    // Blank Steam icons: a symbol for the name (games category), colored like Steam's small game icon
                    var design = IconGenerator.InPackStyle(new GenInput
                    {
                        Name = row.Name, ProgramPng = png, ColorPng = IconMakerPage.ColorHint(entry), Style = style, StylePackId = pack.Id,
                        Category = entry.Game is not null ? IconCategories.Games : IconCategories.Free,
                    });
                    row.SetGenerated(design, IcoWriter.Render(DesignRenderer.Render(design, style), 64));
                }
                catch (Exception ex) { App.LogError(ex); }
            });
        }
    }

    private List<SuggestionRow> _shown = new();

    private void ShowList(string emptyMessage)
    {
        _shown = _all.Where(r => HideChanged.IsChecked != true || !r.AlreadyChanged).ToList();
        var view = new System.Windows.Data.ListCollectionView(_shown);
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(SuggestionRow.Group)));
        List.ItemsSource = view;
        EmptyText.Text = _all.Count > 0 && _shown.Count == 0
            ? Loc.T("Alle Programme mit Vorschlag haben schon ein neues Icon.", "All programs with a suggestion already have a new icon.")
            : emptyMessage;
        EmptyText.Visibility = _shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSummary();
    }

    private IEnumerable<SuggestionRow> Shown => _shown;

    private void UpdateSummary()
    {
        var selected = Shown.Where(r => r.IsChecked).ToList();
        int shortcuts = selected.Sum(r => r.Program.Entry.Shortcuts.Count);
        int admin = selected.Sum(r => r.Program.Entry.Shortcuts.Count(s => s.NeedsAdmin));
        SummaryTitle.Text = Loc.T($"{selected.Count} von {Shown.Count()} Programmen ausgewählt", $"{selected.Count} of {Shown.Count()} programs selected");
        SummaryText.Text = Loc.T($"{shortcuts} Verknüpfung(en) werden geändert, die Originale gesichert.", $"{shortcuts} shortcut(s) will be changed, the originals backed up.")
                           + (admin > 0 ? Loc.T($" {admin} davon brauchen Adminrechte (eine Abfrage).", $" {admin} of them need admin rights (one prompt).") : "");
        ApplyButton.IsEnabled = selected.Count > 0;
    }

    private void PackBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => Build();
    private void HideChanged_Click(object sender, RoutedEventArgs e) => ShowList(EmptyText.Text);
    private void Check_Click(object sender, RoutedEventArgs e) => UpdateSummary();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Shown) r.IsChecked = true;
        UpdateSummary();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var r in Shown) r.IsChecked = false;
        UpdateSummary();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    private void Other_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SuggestionRow row) OpenProgram?.Invoke(row.Program);
    }

    private void Tune_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is SuggestionRow row) _main?.GenerateInMaker(row.Program.Entry);
    }

    private void FillGaps_Click(object sender, RoutedEventArgs e) => Build();

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_main is null) return;
        var selected = Shown.Where(r => r.IsChecked).ToList();
        if (selected.Count == 0) return;
        Busy.Visibility = Visibility.Visible;
        IsEnabled = false;
        try
        {
            var items = new List<AssignmentStore.BatchItem>();
            foreach (var r in selected)
            {
                if (r.Suggestion is { } s) { items.Add(new AssignmentStore.BatchItem(r.Program.Entry.Shortcuts, r.Name, s.Pack, s.Icon)); continue; }
                if (r.Design is null) continue; // program icon could not be read
                // Generated: save as own icon (re-editable in the icon maker), then assign like any pack icon
                byte[] png = DesignRenderer.RenderPng(r.Design, PackStyle.For(r.Pack));
                var (customPack, customIcon) = _main.Packs.SaveCustomIcon(r.Name, png, r.Pack.Name, r.Design.ToJson());
                items.Add(new AssignmentStore.BatchItem(r.Program.Entry.Shortcuts, r.Name, customPack, customIcon));
            }
            var result = await _main.Assignments.ApplyManyAsync(items, null);
            Applied?.Invoke();
            if (result.Failed.Count == 0)
                ShowInfo(true, Loc.T($"{items.Count} Programm(e) haben ein neues Icon", $"{items.Count} program(s) have a new icon"),
                         Loc.T($"{result.Succeeded.Count} Verknüpfung(en) geändert. Rückgängig machen geht jederzeit unter „Zuweisungen & Sicherung“.",
                               $"{result.Succeeded.Count} shortcut(s) changed. You can undo this at any time on “Assignments & backup”."));
            else
                ShowInfo(false, Loc.T($"{result.Succeeded.Count} geändert, {result.Failed.Count} fehlgeschlagen", $"{result.Succeeded.Count} changed, {result.Failed.Count} failed"),
                         string.Join("\n", result.Failed.Take(8).Select(f => $"{System.IO.Path.GetFileName(f.Path)}: {f.Error}")), warning: result.Succeeded.Count > 0);
            Build();
        }
        catch (Exception ex) { App.LogError(ex); ShowInfo(false, Loc.T("Übernehmen fehlgeschlagen", "Applying failed"), ex.Message); }
        finally
        {
            Busy.Visibility = Visibility.Collapsed;
            IsEnabled = true;
        }
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
