using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IconWorkshop.Controls;
using IconWorkshop.Imaging;
using IconWorkshop.Packs;
using IconWorkshop.Services;
using Microsoft.Win32;

namespace IconWorkshop.Views;

/// <summary>A pack card: name, statistics, source and a few sample icons.</summary>
public sealed class PackRow
{
    public IconPack Pack { get; }
    public string Name => Pack.Name;
    public string Meta { get; }
    public string Source { get; }
    public string SourceFull => Pack.SourceFile;
    public IReadOnlyList<ImageSource> Samples { get; }

    public PackRow(IconPack pack)
    {
        Pack = pack;
        var parts = new List<string> { Loc.IconCount(pack.Icons.Count) };
        if (pack.Categories.Count > 0) parts.Add(Loc.PackCategories(pack.Categories.Count));
        if (!string.IsNullOrWhiteSpace(pack.Version)) parts.Add($"{Loc.PackVersion} {pack.Version}");
        parts.Add(FormatSize(pack.SizeOnDisk()));
        Meta = string.Join("  ·  ", parts);
        Source = $"{Loc.PackImported} {pack.ImportedUtc.ToLocalTime():d}  ·  {Path.GetFileName(pack.SourceFile.TrimEnd('\\'))}";
        Samples = PickSamples(pack).Select(i => ImageTools.LoadThumbnail(pack.PngPath(i), 80)).OfType<ImageSource>().ToList();
    }

    private const int SampleCount = 14;

    private static IEnumerable<PackIcon> PickSamples(IconPack pack)
    {
        // Well-known apps look nicer than the first entries ("a_better_camera" ...)
        string[] preferred = ["chrome", "spotify", "whatsapp", "telegram", "youtube", "steam", "discord", "firefox", "netflix", "instagram", "gmail", "vlc", "camera", "calculator", "settings", "maps"];
        var byName = pack.Icons.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        var picks = preferred.Select(n => byName.GetValueOrDefault(n)).OfType<PackIcon>().Take(SampleCount).ToList();
        foreach (var icon in pack.Icons)
        {
            if (picks.Count >= SampleCount) break;
            if (!picks.Contains(icon)) picks.Add(icon);
        }
        return picks;
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024.0 / 1024.0:F0} MB" : $"{Math.Max(1, bytes / 1024)} KB";
}

/// <summary>Import and manage icon packs.</summary>
public partial class PacksPage : UserControl, IPage
{
    private readonly MainWindow _main;
    private CancellationTokenSource? _importCts;

    public PacksPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        main.Packs.Changed += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var packs = _main.Packs.Packs;
        PackList.ItemsSource = packs.Select(p => new PackRow(p)).ToList();
        EmptyState.Visibility = packs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SummaryText.Text = packs.Count == 0 ? "" : "  ·  " + Loc.T($"{packs.Count} Icon-Pack(s) · {packs.Sum(p => p.Icons.Count):N0} Icons",
                                                         $"{packs.Count} icon pack(s) · {packs.Sum(p => p.Icons.Count):N0} icons");
    }

    // ---------- Import ----------

    private async void ImportApk_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = Loc.ImportApk,
            Filter = Loc.T("Icon-Packs (*.apk;*.apks;*.xapk;*.zip)|*.apk;*.apks;*.xapk;*.zip|Alle Dateien (*.*)|*.*",
                           "Icon packs (*.apk;*.apks;*.xapk;*.zip)|*.apk;*.apks;*.xapk;*.zip|All files (*.*)|*.*"),
            Multiselect = true,
        };
        if (dlg.ShowDialog(_main) != true) return;
        foreach (var file in dlg.FileNames) await ImportAsync(file);
    }

    private async void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.ImportFolder };
        if (dlg.ShowDialog(_main) != true) return;
        await ImportAsync(dlg.FolderName);
    }

    public async Task ImportAsync(string source)
    {
        if (_importCts is not null) return; // one import at a time
        _importCts = new CancellationTokenSource();
        SetBusy(true, Path.GetFileName(source.TrimEnd('\\')));
        var progress = new Progress<ImportProgress>(p =>
        {
            ProgressText.Text = p.Total > 0 ? $"{p.Phase}  {p.Done:N0} / {p.Total:N0}" : p.Phase;
            bool determinate = p.Total > 0;
            ProgressBar.Visibility = determinate ? Visibility.Visible : Visibility.Collapsed;
            ProgressBusy.Visibility = determinate ? Visibility.Collapsed : Visibility.Visible;
            if (determinate) ProgressBar.Value = (double)p.Done / p.Total;
        });
        try
        {
            var result = await _main.Packs.ImportAsync(source, progress, _importCts.Token);
            ShowInfo(true, Loc.ImportDone(result.Pack.Name, result.Pack.Icons.Count),
                     Loc.ImportSkipped(result.Missing, result.Unsupported, result.Failed));
        }
        catch (OperationCanceledException) { InfoBar.Visibility = Visibility.Collapsed; }
        catch (Exception ex)
        {
            ShowInfo(false, Loc.ImportFailed, $"{Path.GetFileName(source.TrimEnd('\\'))}: {ex.Message}");
        }
        finally
        {
            _importCts.Dispose();
            _importCts = null;
            SetBusy(false, null);
        }
    }

    private void SetBusy(bool busy, string? name)
    {
        ProgressCard.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ImportApkButton.IsEnabled = ImportFolderButton.IsEnabled = !busy;
        if (busy)
        {
            ProgressTitle.Text = $"{Loc.Importing} {name}";
            ProgressText.Text = "";
            ProgressBar.Visibility = Visibility.Collapsed;
            ProgressBusy.Visibility = Visibility.Visible;
            InfoBar.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelImport_Click(object sender, RoutedEventArgs e) => _importCts?.Cancel();

    private void ShowInfo(bool success, string title, string text)
    {
        InfoIcon.Text = success ? "" : "";
        InfoIcon.SetResourceReference(TextBlock.ForegroundProperty, success ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
        InfoBar.SetResourceReference(Border.BackgroundProperty, success ? "SystemFillColorSuccessBackgroundBrush" : "SystemFillColorCriticalBackgroundBrush");
        InfoTitle.Text = title;
        InfoText.Text = text;
        InfoText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        InfoBar.Visibility = Visibility.Visible;
    }

    private void CloseInfo_Click(object sender, RoutedEventArgs e) => InfoBar.Visibility = Visibility.Collapsed;

    // ---------- Pack actions ----------

    private static IconPack? PackOf(object sender) => ((sender as FrameworkElement)?.Tag as PackRow)?.Pack;

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (PackOf(sender) is { } pack) _main.ShowLibrary(pack.Id);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (PackOf(sender) is { } pack) ShellHelper.OpenFolder(pack.Folder);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (PackOf(sender) is not { } pack) return;
        string? name = FluentDialog.Prompt(_main, Loc.RenamePackTitle, "", pack.Name, Loc.Rename);
        if (!string.IsNullOrWhiteSpace(name)) _main.Packs.Rename(pack, name);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (PackOf(sender) is not { } pack) return;
        int usage = _main.Packs.UsageCount(pack);
        var answer = FluentDialog.Show(_main, Loc.DeletePackTitle, Loc.DeletePackText(pack.Name), Loc.Delete, close: Loc.Cancel,
                                       note: usage > 0 ? Loc.DeletePackInUse(usage) : null, glyph: FluentDialog.GlyphWarning);
        if (answer != DialogResultKind.Primary) return;
        _main.Packs.Delete(pack, _main.Assignments.IcoFilesInUse(pack)); // ICO files still in use are kept
    }
}
