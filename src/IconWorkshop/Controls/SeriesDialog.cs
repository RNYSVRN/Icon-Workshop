using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using IconWorkshop.Imaging;
using IconWorkshop.Programs;

namespace IconWorkshop.Controls;

/// <summary>
/// Series: many icons in the style of one generated variant (e.g. all project folders, all games).
/// One name per line or programs from the list; preview updates while typing.
/// </summary>
public sealed class SeriesDialog : Window
{
    public sealed record Result(string Name, MakerDesign Design);

    private readonly GenParts _template;
    private readonly GenInput _baseInput;
    private readonly List<ProgramEntry> _programs;
    private readonly Dictionary<string, byte[]?> _programIcons = new(StringComparer.CurrentCultureIgnoreCase);
    private readonly TextBox _names = new() { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 150 };
    private readonly CheckBox _vary = new() { Content = Loc.T("Farben abwechseln (aus derselben Farbfamilie)", "Alternate colors (from the same color family)"), Margin = new Thickness(0, 10, 0, 0) };
    private readonly WrapPanel _preview = new();
    private readonly TextBlock _count = new() { Margin = new Thickness(0, 10, 0, 6) };
    private readonly Button _ok = new() { MinWidth = 150, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
    private readonly DispatcherTimer _delay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private List<Result> _results = new();

    private SeriesDialog(GenParts template, GenInput baseInput, List<ProgramEntry> programs)
    {
        _template = template;
        _baseInput = baseInput;
        _programs = programs;
        Title = Loc.T("Serie im selben Stil", "Series in the same style");
        Width = 640; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");

        var intro = new TextBlock
        {
            Text = Loc.T("Ein Name pro Zeile, z. B. deine Projektordner oder Spiele. Form, Look und Farben der gewählten Variante bleiben, das Symbol passt sich dem Namen an.",
                         "One name per line, e.g. your project folders or games. Shape, look and colors of the chosen variant stay, the symbol follows the name."),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10),
        };
        intro.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        var addProgram = new Button { Content = Loc.T("Programm hinzufügen …", "Add program …"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        addProgram.Click += (_, _) => AddProgram();
        _names.TextChanged += (_, _) => { _delay.Stop(); _delay.Start(); };
        _vary.Click += (_, _) => Update();
        _delay.Tick += (_, _) => { _delay.Stop(); Update(); };
        _ok.Click += (_, _) => { if (_results.Count > 0) DialogResult = true; };
        var cancel = new Button { Content = Loc.Cancel, MinWidth = 110, IsCancel = true };
        _ok.SetResourceReference(StyleProperty, "AccentButtonStyle");

        var top = new StackPanel();
        top.Children.Add(intro);
        top.Children.Add(_names);
        top.Children.Add(addProgram);
        top.Children.Add(_vary);
        top.Children.Add(_count);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        buttons.Children.Add(_ok);
        buttons.Children.Add(cancel);
        var scroll = new ScrollViewer { Content = _preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var root = new DockPanel { Margin = new Thickness(20) };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(top);
        root.Children.Add(buttons);
        root.Children.Add(scroll);
        Content = root;
        Update();
        Loaded += (_, _) => _names.Focus();
    }

    private void AddProgram()
    {
        var picked = ProgramPickerDialog.Pick(this, _programs);
        if (picked is null) return;
        _programIcons[picked.Name] = Views.IconMakerPage.ProgramPng(picked);
        _names.Text = (_names.Text.TrimEnd() + Environment.NewLine + picked.Name).Trim();
    }

    private void Update()
    {
        var names = _names.Text.Split('\n').Select(n => n.Trim()).Where(n => n.Length > 0).Distinct(StringComparer.CurrentCultureIgnoreCase).Take(60).ToList();
        var palettes = _baseInput.Category.Palettes.ToList();
        int start = Math.Max(0, palettes.IndexOf(_template.Palette));
        _results = new();
        _preview.Children.Clear();
        for (int i = 0; i < names.Count; i++)
        {
            var input = new GenInput
            {
                Name = names[i], ProgramPng = _programIcons.GetValueOrDefault(names[i]),
                Style = _baseInput.Style, StylePackId = _baseInput.StylePackId, Category = _baseInput.Category,
            };
            var palette = _vary.IsChecked == true && palettes.Count > 0 ? palettes[(start + i) % palettes.Count] : null;
            try
            {
                var design = IconGenerator.Compose(IconGenerator.ForName(_template, input, palette), input);
                _results.Add(new Result(names[i], design));
                var cell = new StackPanel { Width = 92, Margin = new Thickness(0, 0, 6, 10) };
                var img = new Image { Width = 64, Height = 64, Source = IcoWriter.Render(DesignRenderer.Render(design, _baseInput.Style), 96) };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                cell.Children.Add(img);
                cell.Children.Add(new TextBlock { Text = names[i], FontSize = 12, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
                _preview.Children.Add(cell);
            }
            catch (Exception ex) { App.LogError(ex); }
        }
        _count.Text = _results.Count == 0 ? Loc.T("Noch keine Namen.", "No names yet.") : Loc.T($"{_results.Count} Icon(s)", $"{_results.Count} icon(s)");
        _ok.Content = Loc.T($"{_results.Count} Icon(s) speichern", $"Save {_results.Count} icon(s)");
        _ok.IsEnabled = _results.Count > 0;
    }

    public static List<Result>? Show(Window owner, GenParts template, GenInput baseInput, List<ProgramEntry> programs)
    {
        var dlg = new SeriesDialog(template, baseInput, programs) { Owner = owner };
        (owner as FluentDialog.IDimmable)?.SetSmoke(true);
        try { return dlg.ShowDialog() == true ? dlg._results : null; }
        finally { (owner as FluentDialog.IDimmable)?.SetSmoke(false); }
    }
}
