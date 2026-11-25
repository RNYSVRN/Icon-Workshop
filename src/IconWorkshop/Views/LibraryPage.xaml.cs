using System.Windows;
using System.Windows.Controls;
using IconWorkshop.Controls;
using IconWorkshop.Imaging;
using IconWorkshop.Services;

namespace IconWorkshop.Views;

/// <summary>Library: browse the icons of all packs, with a detail column for the selected icon.</summary>
public partial class LibraryPage : UserControl, IPage
{
    private readonly MainWindow _main;

    public LibraryPage(MainWindow main)
    {
        InitializeComponent();
        _main = main;
        Browser.Attach(main.Packs, App.Settings.LibraryPack, App.Settings.LibraryTileSize);
        Browser.SelectionChanged += ShowDetail;
        Browser.CountChanged += (shown, total) => CountText.Text = "  ·  " + Loc.IconCountOf(shown, total);
        Browser.TileSizeChanged += size => { App.Settings.LibraryTileSize = size; App.Settings.Save(); };
        main.Packs.Changed += UpdateEmptyState;
        SizeChanged += (_, _) => DetailColumn.Width = new GridLength(ActualWidth < 760 ? 0 : 280);
        UpdateEmptyState();
        ShowDetail(null);
    }

    public void OnShown() => UpdateEmptyState();

    public void FocusSearch() => Browser.FocusSearch();

    public void ShowPack(string? packId) => Browser.ShowPack(packId);

    private void UpdateEmptyState()
    {
        bool empty = _main.Packs.Packs.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        BrowserArea.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        if (empty) CountText.Text = "";
    }

    private void ShowDetail(IconItem? item)
    {
        if (item is not null && Browser.SelectedPackId != App.Settings.LibraryPack)
        {
            App.Settings.LibraryPack = Browser.SelectedPackId;
            App.Settings.Save();
        }
        DetailActions.IsEnabled = item is not null;
        MakerButtonText.Text = item is not null && item.Pack.Id == Packs.PackStore.CustomPackId && Packs.PackStore.DesignPath(item.Pack, item.Icon) is not null
            ? Loc.T("Im Icon-Maker bearbeiten", "Edit in icon maker")
            : Loc.T("Im Icon-Maker verwenden", "Use in icon maker");
        DetailPlaceholder.Visibility = item is null ? Visibility.Visible : Visibility.Collapsed;
        DetailImage.Source = item is null ? null : ImageTools.LoadThumbnail(item.Path, 256);
        DetailTitle.Text = item?.Title ?? Loc.T("Klicke auf ein Icon, um es hier anzusehen.", "Click an icon to view it here.");
        DetailTitle.FontSize = item is null ? 13 : 18;
        DetailTitle.FontWeight = item is null ? FontWeights.Normal : FontWeights.SemiBold;
        DetailPack.Text = item is null ? "" : $"{Loc.FromPack} {item.Pack.Name}";
        var cats = item?.Icon.Categories?.Where(c => c < item.Pack.Categories.Count).Select(c => item.Pack.Categories[c]).ToList();
        DetailCategory.Text = cats is { Count: > 0 } ? $"{Loc.Category}: {string.Join(", ", cats)}" : "";
    }

    private void Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Selected is { } item) ShellHelper.RevealFile(item.Path);
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Selected is { } item) ShellHelper.Copy(item.Path);
    }

    private void GoToPacks_Click(object sender, RoutedEventArgs e) => _main.ShowPacks();

    private void OpenInMaker_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Selected is { } item) _main.OpenInMaker(item);
    }

    public static string AssignToText => Loc.T("Zuweisen an …", "Assign to …");

    /// <summary>"Assign to": choose the area, then pick the target there; the icon is preselected.</summary>
    private void Assign_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Selected is not { } item) return;
        AssignMenu.Items.Clear();
        foreach (var (index, label) in _main.AssignAreas())
        {
            var mi = new MenuItem { Header = label };
            mi.Click += (_, _) => _main.StartAssign(item, index);
            AssignMenu.Items.Add(mi);
        }
        AssignMenu.PlacementTarget = AssignButton;
        AssignMenu.IsOpen = true;
    }

    internal IconBrowser BrowserControl => Browser;

    internal void SelectFirstForScreenshot()
    {
        Browser.SetSearch("steam");
        Browser.SelectFirst();
    }
}
