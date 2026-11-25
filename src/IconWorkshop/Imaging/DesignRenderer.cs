using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IconWorkshop.Packs;
using Path = System.Windows.Shapes.Path;

namespace IconWorkshop.Imaging;

/// <summary>
/// Renders a <see cref="MakerDesign"/> into a 256 px icon. Builds a small WPF element tree
/// (tile, gloss, clipped layers, overlay, badges) so effects like soft shadows work, then renders it.
/// </summary>
public static class DesignRenderer
{
    public const int Size = 256;
    private const double Inset = 16;

    public static BitmapSource Render(MakerDesign d, PackStyle style)
    {
        var root = new Grid { Width = Size, Height = Size };
        RenderOptions.SetBitmapScalingMode(root, BitmapScalingMode.HighQuality);
        var tileRect = new Rect(Inset, Inset, Size - 2 * Inset, Size - 2 * Inset);
        Geometry? geo = d.Shape is TileShape.PackPlate or TileShape.None ? null : ShapeGeometry(d.Shape, tileRect);
        bool packPlate = d.Shape == TileShape.PackPlate;

        // ---------- Tile ----------
        if (d.Shape != TileShape.None)
        {
            UIElement tile;
            if (packPlate && d.UsePackPlate && style.Back is { } back)
                tile = new Image { Source = back, Stretch = Stretch.Fill };
            else if (packPlate)
                tile = new Rectangle { Fill = BrushOf(d.Background), OpacityMask = IconMask(style.Shape) };
            else
            {
                tile = new Path
                {
                    Data = geo,
                    // Folder: the silhouette is the back part (darker), the front panel follows as a detail
                    Fill = BrushOf(d.Shape == TileShape.Folder ? Shade(d.Background, 0.28) : d.Background),
                    Stroke = d.Border.On ? new SolidColorBrush(d.Border.C) : null,
                    StrokeThickness = d.Border.Width,
                    StrokeLineJoin = PenLineJoin.Round,
                };
            }
            if (d.TileShadow.Kind != ShadowKind.None) tile.Effect = SoftShadow(d.TileShadow);
            root.Children.Add(tile);
            foreach (var detail in ShapeDetails(d, tileRect)) root.Children.Add(detail);

            if (d.Gloss)
            {
                var gloss = new LinearGradientBrush(Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(0, 0.6));
                root.Children.Add(geo is not null
                    ? new Path { Data = geo, Fill = gloss }
                    : new Rectangle { Fill = gloss, OpacityMask = IconMask(style.Shape) });
            }
        }

        // ---------- Layers (clipped to the tile) ----------
        var content = new Grid { Width = Size, Height = Size };
        if (d.ClipToShape && d.Shape != TileShape.None)
        {
            if (geo is not null) content.Clip = geo;
            else if (packPlate) content.OpacityMask = IconMask(style.Keep ?? style.Shape);
        }
        var badges = new Grid { Width = Size, Height = Size };
        foreach (var layer in d.Layers.Where(l => l.Visible))
        {
            var target = layer.Kind == LayerKind.Badge ? badges : content;
            foreach (var e in LayerElements(layer)) target.Children.Add(e);
        }
        root.Children.Add(content);

        // ---------- Pack overlay, border on the pack plate, badges ----------
        if (packPlate && d.PackOverlay && style.Upon is { } upon) root.Children.Add(new Image { Source = upon, Stretch = Stretch.Fill });
        root.Children.Add(badges);

        root.Measure(new Size(Size, Size));
        root.Arrange(new Rect(0, 0, Size, Size));
        root.UpdateLayout();
        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        rtb.Freeze();
        return rtb;
    }

    public static byte[] RenderPng(MakerDesign d, PackStyle style)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(Render(d, style)));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    // ---------- Layers ----------

    private static IEnumerable<UIElement> LayerElements(MakerLayer l)
    {
        double box = Size * l.Size;
        var center = new Point(Size / 2.0 + l.X * Size, Size / 2.0 + l.Y * Size);
        var rect = new Rect(center.X - box / 2, center.Y - box / 2, box, box);
        var elements = new List<UIElement>();

        if (l.Kind == LayerKind.Badge)
        {
            var circle = new EllipseGeometry(center, box / 2, box / 2);
            var badge = new Path { Data = circle, Fill = BrushOf(l.BadgeFill) };
            if (l.Outline.On) { badge.Stroke = new SolidColorBrush(l.Outline.C); badge.StrokeThickness = l.Outline.Width; }
            if (l.Shadow.Kind == ShadowKind.Soft) badge.Effect = SoftShadow(l.Shadow);
            elements.Add(badge);
            if (l.Text.Length > 0)
            {
                var textGeo = FittedText(l.Text, new Typeface(new FontFamily(l.Font), FontStyles.Normal, l.Bold ? FontWeights.Bold : FontWeights.SemiBold, FontStretches.Normal),
                                         new Rect(center.X - box * 0.32, center.Y - box * 0.32, box * 0.64, box * 0.64), glyph: false);
                elements.Add(new Path { Data = textGeo, Fill = BrushOf(l.Fill) });
            }
            foreach (var e in elements) Transform(e, l, center);
            return elements;
        }

        // Silhouette for shadows + the visible element
        Func<Brush, UIElement> silhouette;
        UIElement main;
        if (l.Kind is LayerKind.Glyph or LayerKind.Text)
        {
            Geometry g = l.Kind == LayerKind.Glyph
                ? FittedText(l.Glyph, IconFonts.Get(l.GlyphSet).Typeface, rect, glyph: true)
                : FittedText(l.Text, new Typeface(new FontFamily(l.Font), FontStyles.Normal, l.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), rect, glyph: false);
            silhouette = b => new Path { Data = g, Fill = b };
            var path = new Path { Data = g, Fill = BrushOf(l.Fill) };
            if (l.Outline.On) { path.Stroke = new SolidColorBrush(l.Outline.C); path.StrokeThickness = l.Outline.Width; path.StrokeLineJoin = PenLineJoin.Round; }
            main = path;
        }
        else
        {
            if (l.Image is not { } img) return elements;
            var fit = Fit(img, rect);
            silhouette = b => new Rectangle { Width = fit.Width, Height = fit.Height, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                                              Margin = new Thickness(fit.X, fit.Y, 0, 0), Fill = b, OpacityMask = new ImageBrush(img) { Stretch = Stretch.Fill } };
            main = l.Tint
                ? silhouette(BrushOf(l.Fill))
                : new Image { Source = img, Width = fit.Width, Height = fit.Height, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left,
                              VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(fit.X, fit.Y, 0, 0) };
        }

        if (l.Shadow.Kind == ShadowKind.Long)
        {
            // Flat long shadow: the silhouette repeated along the direction, fading out
            double rad = l.Shadow.Angle * Math.PI / 180;
            double dx = Math.Cos(rad), dy = -Math.Sin(rad);
            int steps = (int)Math.Clamp(l.Shadow.Size, 4, 200);
            var shadowBrush = new SolidColorBrush(l.Shadow.C);
            var group = new Grid { Opacity = l.Shadow.Opacity };
            for (int i = steps; i >= 1; i -= 2)
            {
                var s = silhouette(shadowBrush);
                s.RenderTransform = new TranslateTransform(dx * i, dy * i);
                group.Children.Add(s);
            }
            elements.Add(group);
        }
        else if (l.Shadow.Kind == ShadowKind.Soft) main.Effect = SoftShadow(l.Shadow);

        elements.Add(main);
        foreach (var e in elements) Transform(e, l, center);
        return elements;
    }

    private static void Transform(UIElement e, MakerLayer l, Point center)
    {
        e.Opacity *= l.Opacity;
        if (l.Rotation != 0)
        {
            var existing = e.RenderTransform as Transform;
            var rotate = new RotateTransform(l.Rotation, center.X, center.Y);
            e.RenderTransform = existing is null || existing == System.Windows.Media.Transform.Identity
                ? rotate
                : new TransformGroup { Children = { existing, rotate } };
        }
    }

    /// <summary>Text or a symbol as geometry, centered by its ink bounds and scaled into the box (text never upscaled beyond its font size).</summary>
    public static Geometry FittedText(string text, Typeface tf, Rect box, bool glyph)
    {
        if (string.IsNullOrEmpty(text)) return Geometry.Empty;
        double fontSize = glyph ? box.Height : box.Height * 0.8;
        var geo = glyph ? GlyphOutline(text, tf, fontSize) : null;
        geo ??= new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tf, fontSize, Brushes.White, 1.0).BuildGeometry(new Point(0, 0));
        var b = geo.Bounds;
        if (b.IsEmpty || b.Width <= 0 || b.Height <= 0) return Geometry.Empty;
        double scale = Math.Min(box.Width / b.Width, box.Height / b.Height);
        if (!glyph) scale = Math.Min(1, scale);
        var t = new TransformGroup();
        t.Children.Add(new TranslateTransform(-(b.X + b.Width / 2), -(b.Y + b.Height / 2)));
        t.Children.Add(new ScaleTransform(scale, scale));
        t.Children.Add(new TranslateTransform(box.X + box.Width / 2, box.Y + box.Height / 2));
        var result = geo.Clone();
        result.Transform = t;
        return result.GetFlattenedPathGeometry();
    }

    /// <summary>
    /// A symbol straight from the font file. Text layout can't map symbols outside the basic plane
    /// (e.g. Material U+FFFD8 "music_note_2") and would draw empty boxes.
    /// </summary>
    public static Geometry? GlyphOutline(string glyph, Typeface tf, double size)
    {
        if (!tf.TryGetGlyphTypeface(out var gt)) return null;
        int cp = char.ConvertToUtf32(glyph, 0);
        return gt.CharacterToGlyphMap.TryGetValue(cp, out ushort index) ? gt.GetGlyphOutline(index, size, size) : null;
    }

    private static Rect Fit(BitmapSource img, Rect box)
    {
        double scale = Math.Min(box.Width / img.PixelWidth, box.Height / img.PixelHeight);
        double w = img.PixelWidth * scale, h = img.PixelHeight * scale;
        return new Rect(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
    }

    // ---------- Brushes, effects, shapes ----------

    /// <summary>
    /// Mask over the fixed 256 px icon area. A default ImageBrush would stretch over the content bounds,
    /// which grow with overflowing shadows, and the mask would slip.
    /// </summary>
    private static ImageBrush IconMask(ImageSource mask) => new(mask)
    {
        Stretch = Stretch.Fill,
        ViewportUnits = BrushMappingMode.Absolute,
        Viewport = new Rect(0, 0, Size, Size),
    };

    public static Brush BrushOf(FillSpec f) => f.Kind switch
    {
        FillKind.Linear => Linear(f),
        FillKind.Radial => new RadialGradientBrush(f.C1, f.C2) { GradientOrigin = new Point(0.35, 0.3), Center = new Point(0.5, 0.5), RadiusX = 0.75, RadiusY = 0.75 },
        _ => new SolidColorBrush(f.C1),
    };

    private static Brush Linear(FillSpec f)
    {
        double rad = f.Angle * Math.PI / 180;
        double dx = Math.Cos(rad) / 2, dy = Math.Sin(rad) / 2;
        return new LinearGradientBrush(f.C1, f.C2, new Point(0.5 - dx, 0.5 - dy), new Point(0.5 + dx, 0.5 + dy));
    }

    private static DropShadowEffect SoftShadow(ShadowSpec s) => new()
    {
        Color = s.C,
        Opacity = s.Opacity,
        BlurRadius = s.Size,
        ShadowDepth = s.Distance,
        Direction = s.Angle,
        RenderingBias = RenderingBias.Quality,
    };

    /// <summary>Where content sits on a shape and how large it may be (folder: on the front panel, drive: smaller).</summary>
    public static (double Y, double MaxSize) ContentArea(TileShape shape) => shape switch
    {
        TileShape.Folder => (0.09, 0.42),
        TileShape.Document => (0.05, 0.42),
        TileShape.Drive => (-0.03, 0.3),
        _ => (0, 1),
    };

    private static FillSpec Shade(FillSpec f, double t) => new()
    {
        Kind = f.Kind, Angle = f.Angle,
        C1 = Mix(f.C1, Colors.Black, t), C2 = Mix(f.C2, Colors.Black, t),
    };

    private static Color Mix(Color a, Color b, double t) =>
        Color.FromArgb(a.A, (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    // Proportions of the Windows-like shapes inside the tile rectangle
    private static (double Top, double Bottom, double Radius) FolderMetrics(Rect r) => (r.Y + r.Height * 0.14, r.Bottom - r.Height * 0.1, r.Width * 0.06);
    private static Rect DocumentRect(Rect r) => new(r.X + r.Width * 0.15, r.Y + r.Height * 0.02, r.Width * 0.7, r.Height * 0.96);
    private static Rect DriveRect(Rect r) => new(r.X, r.Y + r.Height * 0.24, r.Width, r.Height * 0.52);

    /// <summary>Second tone of the Windows shapes: front panel of the folder, folded corner of the page, front strip and light of the drive.</summary>
    private static IEnumerable<UIElement> ShapeDetails(MakerDesign d, Rect r)
    {
        switch (d.Shape)
        {
            case TileShape.Folder:
            {
                var (top, bottom, rad) = FolderMetrics(r);
                var front = new RectangleGeometry(new Rect(r.X, top + r.Height * 0.16, r.Width, bottom - top - r.Height * 0.16), rad, rad);
                yield return new Path { Data = front, Fill = BrushOf(d.Background) };
                // Thin light edge at the top of the front panel, like the Windows 11 folder
                yield return new Path { Data = new RectangleGeometry(new Rect(r.X + rad, top + r.Height * 0.16, r.Width - 2 * rad, 2.5)), Fill = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)) };
                break;
            }
            case TileShape.Document:
            {
                var p = DocumentRect(r);
                double fold = p.Width * 0.32;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(p.Right - fold, p.Y), true, true);
                    c.LineTo(new Point(p.Right - fold, p.Y + fold - 8), true, true);
                    c.ArcTo(new Point(p.Right - fold + 8, p.Y + fold), new Size(8, 8), 0, false, SweepDirection.Counterclockwise, true, true);
                    c.LineTo(new Point(p.Right, p.Y + fold), true, true);
                }
                g.Freeze();
                yield return new Path { Data = g, Fill = BrushOf(Shade(d.Background, 0.3)) };
                break;
            }
            case TileShape.Drive:
            {
                var b = DriveRect(r);
                double rad = r.Width * 0.08;
                var strip = new RectangleGeometry(new Rect(b.X, b.Bottom - b.Height * 0.26, b.Width, b.Height * 0.26), rad, rad);
                var clip = new RectangleGeometry(b, rad, rad);
                yield return new Path { Data = strip, Fill = BrushOf(Shade(d.Background, 0.3)), Clip = clip };
                yield return new Path { Data = new EllipseGeometry(new Point(b.Right - b.Width * 0.1, b.Bottom - b.Height * 0.13), 5, 5), Fill = new SolidColorBrush(Color.FromRgb(0x5B, 0xE3, 0x8C)) };
                break;
            }
        }
    }

    public static Geometry ShapeGeometry(TileShape shape, Rect r)
    {
        double cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        switch (shape)
        {
            case TileShape.Folder:
            {
                var (top, bottom, rad) = FolderMetrics(r);
                double tabRight = r.X + r.Width * 0.4, bodyTop = top + r.Height * 0.08;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(r.X, top + rad), true, true);
                    c.ArcTo(new Point(r.X + rad, top), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                    c.LineTo(new Point(tabRight - r.Width * 0.03, top), true, true);
                    c.LineTo(new Point(tabRight + r.Width * 0.05, bodyTop), true, true);
                    c.LineTo(new Point(r.Right - rad, bodyTop), true, true);
                    c.ArcTo(new Point(r.Right, bodyTop + rad), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                    c.LineTo(new Point(r.Right, bottom - rad), true, true);
                    c.ArcTo(new Point(r.Right - rad, bottom), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                    c.LineTo(new Point(r.X + rad, bottom), true, true);
                    c.ArcTo(new Point(r.X, bottom - rad), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                }
                g.Freeze();
                return g;
            }
            case TileShape.Document:
            {
                var p = DocumentRect(r);
                double fold = p.Width * 0.32, rad = p.Width * 0.08;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(p.X + rad, p.Y), true, true);
                    c.LineTo(new Point(p.Right - fold, p.Y), true, true);
                    c.LineTo(new Point(p.Right, p.Y + fold), true, true);
                    c.LineTo(new Point(p.Right, p.Bottom - rad), true, true);
                    c.ArcTo(new Point(p.Right - rad, p.Bottom), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                    c.LineTo(new Point(p.X + rad, p.Bottom), true, true);
                    c.ArcTo(new Point(p.X, p.Bottom - rad), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                    c.LineTo(new Point(p.X, p.Y + rad), true, true);
                    c.ArcTo(new Point(p.X + rad, p.Y), new Size(rad, rad), 0, false, SweepDirection.Clockwise, true, true);
                }
                g.Freeze();
                return g;
            }
            case TileShape.Drive: return new RectangleGeometry(DriveRect(r), r.Width * 0.08, r.Width * 0.08);
            case TileShape.Circle: return new EllipseGeometry(new Point(cx, cy), r.Width / 2, r.Height / 2);
            case TileShape.Square: return new RectangleGeometry(r, 10, 10);
            case TileShape.RoundedSquare: return new RectangleGeometry(r, r.Width * 0.22, r.Width * 0.22);
            case TileShape.Portrait: return new RectangleGeometry(new Rect(cx - r.Width * 0.36, r.Y, r.Width * 0.72, r.Height), r.Width * 0.12, r.Width * 0.12);
            case TileShape.Landscape: return new RectangleGeometry(new Rect(r.X, cy - r.Height * 0.36, r.Width, r.Height * 0.72), r.Width * 0.12, r.Width * 0.12);
            case TileShape.Pill: return new RectangleGeometry(new Rect(r.X, cy - r.Height * 0.32, r.Width, r.Height * 0.64), r.Height * 0.32, r.Height * 0.32);
            case TileShape.Squircle: return Superellipse(cx, cy, r.Width / 2, 5);
            case TileShape.Hexagon: return Polygon(cx, cy, r.Width / 2, 6, 90, 0.08);
            case TileShape.Octagon: return Polygon(cx, cy, r.Width / 2, 8, 22.5, 0.06);
            case TileShape.Triangle: return Polygon(cx, cy + r.Height * 0.08, r.Width * 0.58, 3, 90, 0.12);
            case TileShape.Shield:
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx, r.Y), true, true);
                    c.BezierTo(new Point(cx + r.Width * 0.25, r.Y + r.Height * 0.08), new Point(r.Right - r.Width * 0.05, r.Y + r.Height * 0.08), new Point(r.Right - r.Width * 0.05, r.Y + r.Height * 0.12), true, true);
                    c.BezierTo(new Point(r.Right - r.Width * 0.05, r.Y + r.Height * 0.62), new Point(cx + r.Width * 0.2, r.Bottom - r.Height * 0.08), new Point(cx, r.Bottom), true, true);
                    c.BezierTo(new Point(cx - r.Width * 0.2, r.Bottom - r.Height * 0.08), new Point(r.X + r.Width * 0.05, r.Y + r.Height * 0.62), new Point(r.X + r.Width * 0.05, r.Y + r.Height * 0.12), true, true);
                    c.BezierTo(new Point(r.X + r.Width * 0.05, r.Y + r.Height * 0.08), new Point(cx - r.Width * 0.25, r.Y + r.Height * 0.08), new Point(cx, r.Y), true, true);
                }
                g.Freeze();
                return g;
            }
            case TileShape.Drop:
            {
                // Rounded square with one sharp corner (top right), like a speech/leaf shape
                double rr = r.Width * 0.5;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(r.Right, r.Y), true, true);
                    c.LineTo(new Point(r.Right, cy), true, true);
                    c.ArcTo(new Point(cx, r.Bottom), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true, true);
                    c.ArcTo(new Point(r.X, cy), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true, true);
                    c.ArcTo(new Point(cx, r.Y), new Size(rr, rr), 0, false, SweepDirection.Clockwise, true, true);
                }
                g.Freeze();
                return g;
            }
            default: return new RectangleGeometry(r, r.Width * 0.22, r.Width * 0.22);
        }
    }

    private static Geometry Superellipse(double cx, double cy, double radius, double n)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (int i = 0; i <= 180; i++)
            {
                double t = i / 180.0 * 2 * Math.PI;
                double cos = Math.Cos(t), sin = Math.Sin(t);
                var p = new Point(cx + radius * Math.Sign(cos) * Math.Pow(Math.Abs(cos), 2 / n), cy + radius * Math.Sign(sin) * Math.Pow(Math.Abs(sin), 2 / n));
                if (i == 0) c.BeginFigure(p, true, true); else c.LineTo(p, true, true);
            }
        }
        g.Freeze();
        return g;
    }

    /// <summary>Regular polygon with slightly rounded corners (corner = fraction of the edge length).</summary>
    private static Geometry Polygon(double cx, double cy, double radius, int sides, double startAngle, double corner)
    {
        var pts = Enumerable.Range(0, sides).Select(i =>
        {
            double a = (startAngle + i * 360.0 / sides) * Math.PI / 180;
            return new Point(cx + radius * Math.Cos(a), cy - radius * Math.Sin(a));
        }).ToArray();
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (int i = 0; i < sides; i++)
            {
                var prev = pts[(i - 1 + sides) % sides];
                var cur = pts[i];
                var next = pts[(i + 1) % sides];
                var a = cur + (prev - cur) * corner;
                var b = cur + (next - cur) * corner;
                if (i == 0) c.BeginFigure(a, true, true); else c.LineTo(a, true, true);
                c.QuadraticBezierTo(cur, b, true, true);
            }
        }
        g.Freeze();
        return g;
    }
}
