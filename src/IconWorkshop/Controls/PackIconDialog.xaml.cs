using System.Windows;
using IconWorkshop.Packs;

namespace IconWorkshop.Controls;

/// <summary>Dialog to pick one icon from the imported packs (double click or "Use").</summary>
public partial class PackIconDialog : Window
{
    public IconItem? Result { get; private set; }

    private PackIconDialog(PackStore packs, string title)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        OkButton.Content = Loc.T("Übernehmen", "Use");
        Browser.Attach(packs, App.Settings.LibraryPack, App.Settings.LibraryTileSize);
        Browser.SelectionChanged += item =>
        {
            OkButton.IsEnabled = item is not null;
            SelectedText.Text = item is null ? "" : $"{item.Title} · {item.Pack.Name}";
        };
        Browser.IconActivated += item => { Result = item; DialogResult = true; };
        Loaded += (_, _) => Browser.FocusSearch();
    }

    public static IconItem? Pick(Window owner, PackStore packs, string title)
    {
        var dlg = new PackIconDialog(packs, title) { Owner = owner };
        (owner as FluentDialog.IDimmable)?.SetSmoke(true);
        try { return dlg.ShowDialog() == true ? dlg.Result : null; }
        finally { (owner as FluentDialog.IDimmable)?.SetSmoke(false); }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = Browser.Selected;
        DialogResult = Result is not null;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
