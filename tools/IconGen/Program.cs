// Draws the Icon Workshop app icon (Windows 11 style) and writes app.ico (16 to 256 px) and app.png.
// Four tiles on a blue/violet gradient: three plain tiles and one colorful "new" icon with a sparkle.
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IconWorkshop.Imaging;

string assets = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/IconWorkshop/Assets"));
if (args.Length > 0) assets = args[0];
Directory.CreateDirectory(assets);

var frames = IcoWriter.DefaultSizes.Select(s => (s, Render(s))).ToList();
IcoWriter.Write(Path.Combine(assets, "app.ico"), frames);
var enc = new PngBitmapEncoder();
enc.Frames.Add(BitmapFrame.Create(Render(256)));
using (var fs = File.Create(Path.Combine(assets, "app.png"))) enc.Save(fs);
Console.WriteLine("written: " + assets);

static BitmapSource Render(int size)
{
    var v = new DrawingVisual();
    using (var dc = v.RenderOpen())
    {
        dc.PushTransform(new ScaleTransform(size / 64.0, size / 64.0));
        bool small = size <= 24;
        // Background
        var bg = new LinearGradientBrush(Color.FromRgb(0x3D, 0x8B, 0xFF), Color.FromRgb(0x7A, 0x4D, 0xF2), new Point(0, 0), new Point(1, 1));
        double inset = small ? 2 : 4;
        dc.DrawRoundedRectangle(bg, null, new Rect(inset, inset, 64 - 2 * inset, 64 - 2 * inset), small ? 12 : 14, small ? 12 : 14);
        // subtle top highlight
        var hl = new LinearGradientBrush(Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), new Point(0, 0), new Point(0, 0.6));
        dc.DrawRoundedRectangle(hl, null, new Rect(inset, inset, 64 - 2 * inset, 64 - 2 * inset), 14, 14);

        double t = small ? 18 : 16, gap = small ? 4 : 5;
        double x0 = 32 - t - gap / 2, x1 = 32 + gap / 2;
        double r = small ? 4 : 4.5;
        var tile = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF));
        var shadow = new SolidColorBrush(Color.FromArgb(40, 0x1A, 0x10, 0x60));
        foreach (var (x, y) in new[] { (x0, x0), (x1, x0), (x0, x1) })
        {
            if (!small) dc.DrawRoundedRectangle(shadow, null, new Rect(x, y + 1.2, t, t), r, r);
            dc.DrawRoundedRectangle(tile, null, new Rect(x, y, t, t), r, r);
        }
        // "new" icon: warm gradient tile
        var warm = new LinearGradientBrush(Color.FromRgb(0xFF, 0xC4, 0x3D), Color.FromRgb(0xFF, 0x5C, 0x8A), new Point(0, 0), new Point(1, 1));
        if (!small) dc.DrawRoundedRectangle(shadow, null, new Rect(x1, x1 + 1.2, t, t), r, r);
        dc.DrawRoundedRectangle(warm, null, new Rect(x1, x1, t, t), r, r);
        if (!small)
        {
            // Sparkle (four-pointed star) on the warm tile
            double cx = x1 + t / 2, cy = x1 + t / 2, a = t * 0.34, b = t * 0.09;
            var star = new StreamGeometry();
            using (var g = star.Open())
            {
                g.BeginFigure(new Point(cx, cy - a), true, true);
                g.QuadraticBezierTo(new Point(cx + b, cy - b), new Point(cx + a, cy), true, true);
                g.QuadraticBezierTo(new Point(cx + b, cy + b), new Point(cx, cy + a), true, true);
                g.QuadraticBezierTo(new Point(cx - b, cy + b), new Point(cx - a, cy), true, true);
                g.QuadraticBezierTo(new Point(cx - b, cy - b), new Point(cx, cy - a), true, true);
            }
            dc.DrawGeometry(Brushes.White, null, star);
            // Small glyph hints on the plain tiles (rounded "icon" dots)
            var hint = new SolidColorBrush(Color.FromArgb(0x55, 0x5B, 0x6C, 0xF5));
            foreach (var (x, y) in new[] { (x0, x0), (x1, x0), (x0, x1) })
                dc.DrawEllipse(hint, null, new Point(x + t / 2, y + t / 2), t * 0.22, t * 0.22);
        }
        dc.Pop();
    }
    var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
    rtb.Render(v);
    rtb.Freeze();
    return rtb;
}
