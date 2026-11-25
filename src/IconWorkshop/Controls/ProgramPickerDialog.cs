using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using IconWorkshop.Programs;

namespace IconWorkshop.Controls;

/// <summary>Small dialog to choose a program (search + list), e.g. to use its icon in the icon maker.</summary>
public sealed class ProgramPickerDialog : Window
{
    private readonly ListBox _list = new() { DisplayMemberPath = nameof(ProgramEntry.Name), Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBox _search = new();
    private readonly List<ProgramEntry> _all;

    private ProgramPickerDialog(List<ProgramEntry> programs)
    {
        _all = programs;
        Title = Loc.T("Programm wählen", "Choose program");
        Width = 460; Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");

        var ok = new Button { Content = Loc.T("Übernehmen", "Use"), MinWidth = 110, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.SetResourceReference(StyleProperty, "AccentButtonStyle");
        var cancel = new Button { Content = Loc.Cancel, MinWidth = 110, IsCancel = true };
        ok.Click += (_, _) => { if (_list.SelectedItem is not null) DialogResult = true; };
        _list.MouseDoubleClick += (_, _) => { if (_list.SelectedItem is not null) DialogResult = true; };
        _search.TextChanged += (_, _) => Filter();
        _search.KeyDown += (_, e) => { if (e.Key == Key.Down && _list.Items.Count > 0) { _list.SelectedIndex = 0; _list.Focus(); } };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var root = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(_search, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(_search);
        root.Children.Add(buttons);
        root.Children.Add(_list);
        Content = root;
        Filter();
        Loaded += (_, _) => _search.Focus();
    }

    private void Filter()
    {
        string q = _search.Text.Trim().ToLowerInvariant();
        _list.ItemsSource = q.Length == 0 ? _all : _all.Where(p => p.Name.ToLowerInvariant().Contains(q)).ToList();
    }

    public static ProgramEntry? Pick(Window owner, List<ProgramEntry> programs)
    {
        var dlg = new ProgramPickerDialog(programs) { Owner = owner };
        (owner as FluentDialog.IDimmable)?.SetSmoke(true);
        try { return dlg.ShowDialog() == true ? dlg._list.SelectedItem as ProgramEntry : null; }
        finally { (owner as FluentDialog.IDimmable)?.SetSmoke(false); }
    }
}
