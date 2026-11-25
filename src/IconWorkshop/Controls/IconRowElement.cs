using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IconWorkshop.Controls;

/// <summary>
/// One row of the icon grid, drawn directly (images, labels, hover and selection) instead of one control
/// per icon. Scrolling through thousands of icons only creates one light element per visible row.
/// </summary>
public sealed class IconRowElement : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IReadOnlyList<IconItem>), typeof(IconRowElement),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => ((IconRowElement)d).OnItemsChanged(e)));

    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(
        nameof(Owner), typeof(IconBrowser), typeof(IconRowElement),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<IconItem>? Items
    {
        get => (IReadOnlyList<IconItem>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public IconBrowser? Owner
    {
        get => (IconBrowser?)GetValue(OwnerProperty);
        set => SetValue(OwnerProperty, value);
    }

    private int _hover = -1;
    private const double Gap = 4;

    public IconRowElement()
    {
        Focusable = false;
        Loaded += (_, _) => Owner?.RegisterRow(this);
        Unloaded += (_, _) => { Owner?.UnregisterRow(this); Detach(Items); };
        ToolTipService.SetInitialShowDelay(this, 600);
    }

    private void OnItemsChanged(DependencyPropertyChangedEventArgs e)
    {
        Detach(e.OldValue as IReadOnlyList<IconItem>);
        if (e.NewValue is IReadOnlyList<IconItem> items)
            foreach (var i in items) i.PropertyChanged += Item_PropertyChanged;
        _hover = -1;
        ToolTip = null;
    }

    private void Detach(IReadOnlyList<IconItem>? items)
    {
        if (items is null) return;
        foreach (var i in items) i.PropertyChanged -= Item_PropertyChanged;
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IconItem.Thumb)) InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Never report 0: the virtualizing panel would then create every row at once.
        // Before the owner binding is resolved, the current tile height of any browser is a good estimate.
        double height = (Owner?.TileHeight ?? IconBrowser.LastTileHeight) + Gap;
        return new Size(0, height);
    }

    private int HitIndex(Point p)
    {
        var o = Owner;
        if (o is null || Items is null) return -1;
        if (p.Y < 0 || p.Y > o.TileHeight) return -1;
        int i = (int)(p.X / (o.TileWidth + Gap));
        double inTile = p.X - i * (o.TileWidth + Gap);
        return i >= 0 && i < Items.Count && inTile <= o.TileWidth ? i : -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = HitIndex(e.GetPosition(this));
        if (i == _hover) return;
        _hover = i;
        Cursor = i >= 0 ? Cursors.Hand : null;
        ToolTip = i >= 0 && Items is not null ? Items[i].Title : null;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover < 0) return;
        _hover = -1;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        int i = HitIndex(e.GetPosition(this));
        if (i >= 0 && Items is not null) { Owner?.SelectFromRow(Items[i]); e.Handled = true; }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2 && HitIndex(e.GetPosition(this)) is int i and >= 0 && Items is not null)
        {
            Owner?.ActivateFromRow(Items[i]);
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var o = Owner;
        var items = Items;
        if (o is null || items is null) return;

        var hoverBrush = TryFindResource("SubtleFillColorSecondaryBrush") as Brush;
        var accent = TryFindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;
        var onAccent = TryFindResource("TextOnAccentFillColorPrimaryBrush") as Brush ?? Brushes.White;
        var textBrush = TryFindResource("TextFillColorSecondaryBrush") as Brush ?? Brushes.Gray;
        var accentPen = new Pen(accent, 2);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(SystemFonts.MessageFontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        double tw = o.TileWidth, th = o.TileHeight, img = o.ImageSize;
        bool labels = o.ShowLabels;
        var selected = o.Selected;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            double x = i * (tw + Gap);
            var tile = new Rect(x, 0, tw, th);
            bool isSelected = ReferenceEquals(item, selected) || (selected is not null && selected.Path == item.Path);

            if (isSelected) dc.DrawRoundedRectangle(hoverBrush, accentPen, new Rect(x + 1, 1, tw - 2, th - 2), 6, 6);
            else if (i == _hover) dc.DrawRoundedRectangle(hoverBrush, null, tile, 6, 6);

            double labelHeight = labels ? 18 : 0;
            double imgY = (th - labelHeight - img) / 2 + (labels ? 2 : 0);
            if (item.Thumb is { } thumb)
                dc.DrawImage(thumb, new Rect(x + (tw - img) / 2, imgY, img, img));

            if (labels)
            {
                var ft = new FormattedText(item.Title, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 11, textBrush, dpi)
                {
                    MaxTextWidth = tw - 6,
                    MaxLineCount = 1,
                    Trimming = TextTrimming.CharacterEllipsis,
                    TextAlignment = TextAlignment.Center,
                };
                dc.DrawText(ft, new Point(x + 3, th - labelHeight - 2));
            }

            if (isSelected)
            {
                var c = new Point(x + tw - 14, 14);
                dc.DrawEllipse(accent, null, c, 9, 9);
                var check = new StreamGeometry();
                using (var g = check.Open())
                {
                    g.BeginFigure(new Point(c.X - 4, c.Y), false, false);
                    g.LineTo(new Point(c.X - 1.2, c.Y + 2.8), true, true);
                    g.LineTo(new Point(c.X + 4.2, c.Y - 3), true, true);
                }
                dc.DrawGeometry(null, new Pen(onAccent, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, check);
            }
        }
    }
}
