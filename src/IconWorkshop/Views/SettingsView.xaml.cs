using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IconWorkshop.Controls;
using IconWorkshop.Packs;
using IconWorkshop.Services;
using Microsoft.Win32;

namespace IconWorkshop.Views;

/// <summary>Settings in the main window: general, help, licenses &amp; privacy, support.</summary>
public partial class SettingsView : UserControl
{
    private sealed record NavEntry(string Glyph, string Label, FrameworkElement Page, bool Animated = false)
    {
        public bool ShowGlyph => !Animated;
    }

    private sealed record LanguageOption(string Code, string Label);

    private MainWindow? _main;
    private string _startupLanguage = "auto";
    private bool _initializing;

    public event Action? BackRequested;
    public event Action? RestartRequested;

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(SettingsView), new PropertyMetadata(false));

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public SettingsView()
    {
        InitializeComponent();
        Nav.ItemsSource = new[]
        {
            new NavEntry("\uE713", Loc.General, GeneralPage),
            new NavEntry("\uE897", Loc.Help, HelpPage),
            new NavEntry("\uE8A5", Loc.LicensesNav, LicensePage),
            new NavEntry("", Loc.Support, SupportPage, Animated: true),
        };
        HelpList.ItemsSource = SettingsContent.HelpTopics();
        LicenseList.ItemsSource = SettingsContent.Licenses();
        PrivacyBox.Content = SettingsContent.Privacy();
        Nav.SelectedIndex = 0;

        // Accordion: when one section opens, all others close.
        AddHandler(Expander.ExpandedEvent, new RoutedEventHandler(OnExpanderExpanded));
    }

    public int PageCount => Nav.Items.Count;
    public void SelectPage(int index) => Nav.SelectedIndex = index;

    public void Initialize(MainWindow main)
    {
        _main = main;
        _initializing = true;
        _startupLanguage = App.Settings.Language;
        var options = new[] { new LanguageOption("auto", Loc.LangAuto), new LanguageOption("de", "Deutsch"), new LanguageOption("en", "English") };
        LanguageBox.ItemsSource = options;
        LanguageBox.SelectedItem = options.FirstOrDefault(o => o.Code == App.Settings.Language) ?? options[0];
        _initializing = false;
        UpdatePacksPath();
        ThumbFrameSwitch.IsChecked = ThumbnailFrame.IsHidden;
        ExplorerMenuSwitch.IsChecked = ExplorerMenu.IsEnabled;
    }

    public void Reset()
    {
        Nav.SelectedIndex = 0;
        UpdatePacksPath();
        ThumbFrameSwitch.IsChecked = ThumbnailFrame.IsHidden;
        ExplorerMenuSwitch.IsChecked = ExplorerMenu.IsEnabled;
    }

    private void ThumbFrameSwitch_Click(object sender, RoutedEventArgs e)
    {
        bool hide = ThumbFrameSwitch.IsChecked == true;
        if (AppSettings.ReadOnly) return; // screenshots / preview: change nothing in Windows
        try { ThumbnailFrame.SetHidden(hide); }
        catch (Exception ex)
        {
            App.LogError(ex);
            ThumbFrameSwitch.IsChecked = ThumbnailFrame.IsHidden;
        ExplorerMenuSwitch.IsChecked = ExplorerMenu.IsEnabled;
            FluentDialog.Show(_main, Loc.T("Nicht möglich", "Not possible"), ex.Message, Loc.Ok, glyph: FluentDialog.GlyphError);
        }
    }

    private void ExplorerMenuSwitch_Click(object sender, RoutedEventArgs e)
    {
        bool on = ExplorerMenuSwitch.IsChecked == true;
        if (AppSettings.ReadOnly) return; // screenshots / preview: change nothing in Windows
        try { ExplorerMenu.SetEnabled(on, Environment.ProcessPath ?? ""); }
        catch (Exception ex)
        {
            App.LogError(ex);
            ExplorerMenuSwitch.IsChecked = ExplorerMenu.IsEnabled;
            FluentDialog.Show(_main, Loc.T("Nicht möglich", "Not possible"), ex.Message, Loc.Ok, glyph: FluentDialog.GlyphError);
        }
    }

    /// <summary>Follows the main navigation (menu button / narrow window).</summary>
    public void SetCompact(bool compact)
    {
        IsCompact = compact;
        NavColumn.Width = new GridLength(compact ? MainWindow.NavCompactWidth : MainWindow.NavWidth);
        SettingsCaption.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Nav_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Nav.SelectedItem is not NavEntry selected) return;
        foreach (NavEntry entry in Nav.Items)
            entry.Page.Visibility = entry == selected ? Visibility.Visible : Visibility.Collapsed;
        ContentScroll.ScrollToTop();
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    private void OnExpanderExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not Expander opened) return;
        foreach (var other in FindChildren<Expander>(this))
            if (!ReferenceEquals(other, opened)) other.IsExpanded = false;
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int n = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var deeper in FindChildren<T>(child)) yield return deeper;
        }
    }

    // ---------- Options ----------

    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || LanguageBox.SelectedItem is not LanguageOption opt) return;
        App.Settings.Language = opt.Code;
        App.Settings.Save();
        RestartHint.Visibility = opt.Code == _startupLanguage ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Restart_Click(object sender, RoutedEventArgs e) => RestartRequested?.Invoke();

    private void WindowsColors_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:colors") { UseShellExecute = true }); } catch { }
    }

    private void Kofi_Click(object sender, RoutedEventArgs e) => ShellHelper.OpenUrl(AppInfo.KofiUrl);
    private void GitHub_Click(object sender, RoutedEventArgs e) => ShellHelper.OpenUrl(AppInfo.GitHubUrl);
    private void Issue_Click(object sender, RoutedEventArgs e) => ShellHelper.OpenUrl(AppInfo.GitHubIssuesUrl);

    // ---------- Locations ----------

    private void UpdatePacksPath()
    {
        if (_main is null) return;
        PacksPathText.Text = DemoMode.DisplayPath(_main.Packs.Root);
        PacksPathBox.ToolTip = PacksPathText.Text;
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e) => ShellHelper.OpenFolder(AppInfo.DataFolder);
    private void CopyDataFolder_Click(object sender, RoutedEventArgs e) => ShellHelper.Copy(AppInfo.DataFolder);
    private void OpenProgramFolder_Click(object sender, RoutedEventArgs e) => ShellHelper.OpenFolder(AppInfo.ProgramFolder);
    private void CopyProgramFolder_Click(object sender, RoutedEventArgs e) => ShellHelper.Copy(AppInfo.ProgramFolder);
    private void OpenPacksFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_main is not null) ShellHelper.OpenFolder(_main.Packs.Root);
    }

    private async void ChangePacksFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_main is null) return;
        var dlg = new OpenFolderDialog { Title = Loc.ChoosePacksFolder, InitialDirectory = _main.Packs.Root };
        if (dlg.ShowDialog(_main) != true) return;
        string target = dlg.FolderName;
        // A folder named after the app inside the chosen folder keeps things tidy (e.g. D:\Icon-Packs)
        if (_main.Packs.Packs.Count > 0 &&
            FluentDialog.Show(_main, Loc.MovePacksTitle, Loc.MovePacksText(target), Loc.Move, close: Loc.Cancel) != DialogResultKind.Primary)
            return;

        ChangePacksButton.IsEnabled = false;
        MoveProgress.Visibility = Visibility.Visible;
        MoveText.Text = Loc.T("Icon-Packs werden verschoben …", "Moving icon packs …");
        try
        {
            var progress = new Progress<ImportProgress>(p => MoveText.Text = p.Total > 0 ? $"{p.Phase}  {p.Done:N0} / {p.Total:N0}" : p.Phase);
            var moved = await _main.Packs.MoveRootAsync(target, progress);
            await _main.Assignments.RepointAsync(moved, _main.Packs);
            App.Settings.PacksFolder = string.Equals(target.TrimEnd('\\'), AppInfo.DefaultPacksFolder, StringComparison.OrdinalIgnoreCase) ? null : target;
            App.Settings.Save();
        }
        catch (Exception ex)
        {
            FluentDialog.Show(_main, Loc.MoveFailed, ex.Message, Loc.Ok, glyph: FluentDialog.GlyphError);
        }
        finally
        {
            ChangePacksButton.IsEnabled = true;
            MoveProgress.Visibility = Visibility.Collapsed;
            UpdatePacksPath();
        }
    }
}
