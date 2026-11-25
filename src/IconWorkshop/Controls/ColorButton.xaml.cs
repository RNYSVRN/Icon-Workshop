using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace IconWorkshop.Controls;

/// <summary>
/// A color swatch that opens a full color picker (saturation/brightness field, hue, opacity, suggestions,
/// recently used colors and optional hex input). Works with HSV internally so dragging stays smooth.
/// </summary>
public partial class ColorButton : UserControl
{
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color), typeof(Color), typeof(ColorButton),
        new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, e) => ((ColorButton)d).OnColorChanged()));

    public Color Color
    {
        get => (Color)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Raised whenever the user changes the color (also while dragging).</summary>
    public event Action<Color>? ColorChanged;

    private static readonly string[] Presets =
    [
        "#FFFFFF", "#F3F3F3", "#A0A0A0", "#5A5A5A", "#202024", "#000000",
        "#E81123", "#FF4F4F", "#FF8C00", "#FFB900", "#FFE14D", "#107C10", "#3CCB5A", "#00B294",
        "#00B7C3", "#0078D4", "#4F8DF7", "#0063B1", "#5C2D91", "#8764B8", "#B4009E", "#E3008C", "#FF5C8A", "#8E562E",
    ];
    private static readonly List<Color> Recent = new();

    private double _h, _s = 0, _v = 1;
    private byte _a = 255;
    private bool _internal;
    private bool _dragSv, _dragHue;

    public ColorButton()
    {
        InitializeComponent();

        OnColorChanged();
    }

    private Border MakeSwatch(Color c)
    {
        var b = new Border
        {
            Width = 20, Height = 20, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 4, 4),
            Background = new SolidColorBrush(c), BorderThickness = new Thickness(1), Cursor = Cursors.Hand, ToolTip = Hex(c),
        };
        b.SetResourceReference(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        b.MouseLeftButtonUp += (_, _) => SetFromUser(Color.FromArgb(_a == 0 ? (byte)255 : _a, c.R, c.G, c.B), updateHsv: true);
        return b;
    }

    private void OnColorChanged()
    {
        var c = Color;
        Swatch.Fill = new SolidColorBrush(c);
        if (_internal) return;
        (_h, _s, _v) = ToHsv(c);
        _a = c.A;
        UpdateVisuals();
    }

    private void SetFromUser(Color c, bool updateHsv)
    {
        if (updateHsv) { (_h, _s, _v) = ToHsv(c); _a = c.A; }
        _internal = true;
        Color = c;
        _internal = false;
        UpdateVisuals();
        ColorChanged?.Invoke(c);
    }

    private void UpdateFromHsv() => SetFromUser(FromHsv(_h, _s, _v, _a), updateHsv: false);

    private void UpdateVisuals()
    {
        SvHue.Fill = new SolidColorBrush(FromHsv(_h, 1, 1, 255));
        Canvas.SetLeft(SvThumb, _s * SvArea.Width - 7);
        Canvas.SetTop(SvThumb, (1 - _v) * SvArea.Height - 7);
        Canvas.SetTop(HueThumb, _h / 360 * HueArea.Height - 4);
        _internal = true;
        AlphaSlider.Value = _a / 255.0 * 100;
        _internal = false;
        AlphaText.Text = $"{Math.Round(_a / 255.0 * 100)} %";
        if (!HexBox.IsKeyboardFocused) HexBox.Text = Hex(Color);
    }

    // ---------- Saturation / brightness field ----------

    private void Sv_MouseDown(object sender, MouseButtonEventArgs e) { _dragSv = true; SvArea.CaptureMouse(); PickSv(e.GetPosition(SvArea)); }
    private void Sv_MouseMove(object sender, MouseEventArgs e) { if (_dragSv) PickSv(e.GetPosition(SvArea)); }
    private void Sv_MouseUp(object sender, MouseButtonEventArgs e) { _dragSv = false; SvArea.ReleaseMouseCapture(); }

    private void PickSv(Point p)
    {
        _s = Math.Clamp(p.X / SvArea.Width, 0, 1);
        _v = Math.Clamp(1 - p.Y / SvArea.Height, 0, 1);
        UpdateFromHsv();
    }

    // ---------- Hue ----------

    private void Hue_MouseDown(object sender, MouseButtonEventArgs e) { _dragHue = true; HueArea.CaptureMouse(); PickHue(e.GetPosition(HueArea)); }
    private void Hue_MouseMove(object sender, MouseEventArgs e) { if (_dragHue) PickHue(e.GetPosition(HueArea)); }
    private void Hue_MouseUp(object sender, MouseButtonEventArgs e) { _dragHue = false; HueArea.ReleaseMouseCapture(); }

    private void PickHue(Point p)
    {
        _h = Math.Clamp(p.Y / HueArea.Height, 0, 0.9999) * 360;
        // A grey would stay grey when changing the hue: give it some color so the change is visible
        if (_s < 0.05) _s = 1;
        if (_v < 0.05) _v = 1;
        UpdateFromHsv();
    }

    private void Alpha_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_internal) return;
        _a = (byte)Math.Round(AlphaSlider.Value / 100 * 255);
        UpdateFromHsv();
    }

    // ---------- Hex and recent colors ----------

    private void HexBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyHex();
    }

    private void HexBox_LostFocus(object sender, RoutedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        if (ParseHex(HexBox.Text) is { } c) SetFromUser(c, updateHsv: true);
        else HexBox.Text = Hex(Color);
    }

    /// <summary>When the picker closes, the chosen color becomes a recent color (shared by all pickers).</summary>
    private void Popup_Closed(object? sender, EventArgs e)
    {
        var c = Color;
        Recent.RemoveAll(r => r == c);
        Recent.Insert(0, c);
        if (Recent.Count > 12) Recent.RemoveAt(Recent.Count - 1);
    }

    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        // Refresh the recent colors right before the popup opens
        if (!Popup.IsOpen) RefreshRecent();
    }

    private void RefreshRecent()
    {
        // Suggestions are created on first opening only (a page has many color buttons; most are never opened)
        if (PresetPanel.Children.Count == 0)
            foreach (var hex in Presets) PresetPanel.Children.Add(MakeSwatch((Color)ColorConverter.ConvertFromString(hex)));
        RecentPanel.Children.Clear();
        foreach (var c in Recent) RecentPanel.Children.Add(MakeSwatch(c));
        RecentTitle.Visibility = RecentPanel.Visibility = Recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- Color math ----------

    public static string Hex(Color c) => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color? ParseHex(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string t = text.Trim();
        if (!t.StartsWith('#')) t = "#" + t;
        try { return t.Length is 7 or 9 ? (Color)ColorConverter.ConvertFromString(t) : null; }
        catch { return null; }
    }

    public static (double H, double S, double V) ToHsv(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        return (h, max == 0 ? 0 : d / max, max);
    }

    public static Color FromHsv(double h, double s, double v, byte a)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return Color.FromArgb(a, (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
