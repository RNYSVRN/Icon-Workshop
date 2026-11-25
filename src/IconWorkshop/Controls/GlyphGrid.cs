using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IconWorkshop.Controls;

/// <summary>
/// Grid of font symbols. Only visible rows exist, and each row draws its symbols itself, so even
/// thousands of symbols open instantly (a WrapPanel would create one control per symbol).
/// </summary>
public sealed class GlyphGrid : UserControl
{
    private const double Cell = 36;
    private readonly ItemsControl _rows;
    private IReadOnlyList<string> _glyphs = [];
    private int _columns = 1;

    /// <summary>The font of the symbols (Segoe Fluent Icons by default).</summary>
    public FontFamily GlyphFont { get; set; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public FontWeight GlyphWeight { get; set; } = FontWeights.Normal;
    /// <summary>Name of a symbol for the tooltip.</summary>
    public Func<string, string?>? NameOf { get; set; }

    public string? Selected { get; private set; }

    public event Action<string>? GlyphChosen;

    public GlyphGrid()
    {
        var factory = new FrameworkElementFactory(typeof(GlyphRow));
        factory.SetBinding(GlyphRow.ItemsProperty, new System.Windows.Data.Binding());
        factory.SetBinding(GlyphRow.OwnerProperty, new System.Windows.Data.Binding { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(GlyphGrid), 1) });
        _rows = new ItemsControl
        {
            ItemTemplate = new DataTemplate { VisualTree = factory },
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel))),
            Template = BuildTemplate(),
        };
        VirtualizingPanel.SetIsVirtualizing(_rows, true);
        VirtualizingPanel.SetVirtualizationMode(_rows, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(_rows, ScrollUnit.Pixel);
        Content = _rows;
        SizeChanged += (_, e) => { if (e.WidthChanged) Rebuild(); };
    }

    private static ControlTemplate BuildTemplate()
    {
        var sv = new FrameworkElementFactory(typeof(ScrollViewer));
        sv.SetValue(ScrollViewer.CanContentScrollProperty, true);
        sv.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        sv.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        sv.SetValue(FocusableProperty, false);
        sv.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        return new ControlTemplate(typeof(ItemsControl)) { VisualTree = sv };
    }

    public void SetGlyphs(IReadOnlyList<string> glyphs)
    {
        _glyphs = glyphs;
        Rebuild();
    }

    public void Select(string? glyph)
    {
        Selected = glyph;
        foreach (var row in Rows()) row.InvalidateVisual();
    }

    internal void Choose(string glyph)
    {
        Select(glyph);
        GlyphChosen?.Invoke(glyph);
    }

    private void Rebuild()
    {
        double width = Math.Max(Cell, ActualWidth - 20);
        _columns = Math.Max(1, (int)(width / Cell));
        var rows = new List<string[]>();
        for (int i = 0; i < _glyphs.Count; i += _columns)
            rows.Add(_glyphs.Skip(i).Take(_columns).ToArray());
        _rows.ItemsSource = rows;
    }

    private IEnumerable<GlyphRow> Rows()
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(_rows);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is GlyphRow r) { yield return r; continue; }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
    }

    /// <summary>One drawn row of symbols with hover and selection.</summary>
    private sealed class GlyphRow : FrameworkElement
    {
        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(string[]), typeof(GlyphRow),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(GlyphGrid), typeof(GlyphRow),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public string[]? Items { get => (string[]?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
        public GlyphGrid? Owner { get => (GlyphGrid?)GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }

        private int _hover = -1;

        protected override Size MeasureOverride(Size availableSize) => new(0, Cell + 2);

        private int Hit(Point p) => Items is { } items && p.Y <= Cell && (int)(p.X / Cell) is int i && i < items.Length ? i : -1;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Hit(e.GetPosition(this));
            if (i == _hover) return;
            _hover = i;
            Cursor = i >= 0 ? Cursors.Hand : null;
            ToolTip = i >= 0 && Items is { } items ? (Owner?.NameOf?.Invoke(items[i]) ?? $"U+{char.ConvertToUtf32(items[i], 0):X4}") : null;
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e) { _hover = -1; ToolTip = null; InvalidateVisual(); }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            int i = Hit(e.GetPosition(this));
            if (i >= 0 && Items is { } items) { Owner?.Choose(items[i]); e.Handled = true; }
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, Cell + 2)); // hit-testable background
            if (Items is not { } items || Owner is not { } owner) return;
            var hover = TryFindResource("SubtleFillColorSecondaryBrush") as Brush;
            var accent = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;
            var text = TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.Black;
            var tf = new Typeface(owner.GlyphFont, FontStyles.Normal, owner.GlyphWeight, FontStretches.Normal);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            for (int i = 0; i < items.Length; i++)
            {
                var r = new Rect(i * Cell + 1, 1, Cell - 2, Cell - 2);
                bool sel = items[i] == owner.Selected;
                if (sel) dc.DrawRoundedRectangle(hover, new Pen(accent, 2), r, 5, 5);
                else if (i == _hover) dc.DrawRoundedRectangle(hover, null, r, 5, 5);
                // Outline from the font file: also works for symbols outside the basic plane (text layout draws boxes there)
                if (IconWorkshop.Imaging.DesignRenderer.GlyphOutline(items[i], tf, 20) is { Bounds.IsEmpty: false } geo)
                {
                    var b = geo.Bounds;
                    double scale = Math.Min(1, 20 / Math.Max(b.Width, b.Height));
                    dc.PushTransform(new TranslateTransform(r.X + r.Width / 2, r.Y + r.Height / 2));
                    dc.PushTransform(new ScaleTransform(scale, scale));
                    dc.PushTransform(new TranslateTransform(-(b.X + b.Width / 2), -(b.Y + b.Height / 2)));
                    dc.DrawGeometry(text, null, geo);
                    dc.Pop(); dc.Pop(); dc.Pop();
                    continue;
                }
                var ft = new FormattedText(items[i], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, 20, text, dpi);
                dc.DrawText(ft, new Point(r.X + (r.Width - ft.Width) / 2, r.Y + (r.Height - ft.Height) / 2));
            }
        }
    }
}
