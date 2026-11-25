using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconWorkshop.Packs;

/// <summary>
/// The look of a pack for icons it doesn't contain, as Android launchers use it:
/// a background plate (iconback), a mask that cuts the content into shape (iconmask) and an overlay (iconupon).
/// Packs without these images get a neutral rounded tile.
/// </summary>
public sealed class PackStyle
{
    public const string FolderName = "style";
    public const int Size = 256;

    public string Name { get; init; } = "";
    public BitmapSource? Back { get; init; }
    public BitmapSource? Upon { get; init; }
    /// <summary>Alpha = where content stays visible (already converted from the Android mask).</summary>
    public BitmapSource? Keep { get; init; }
    /// <summary>Alpha = outline of the tile (for a custom background color).</summary>
    public BitmapSource Shape { get; init; } = null!;
    /// <summary>Android scale factor of the pack (1 = unchanged).</summary>
    public double Scale { get; init; } = 1;
    public bool IsNeutral { get; init; }

    /// <summary>Width of the visible tile relative to the icon (narrow portrait plates ~0.6), for a good default content size.</summary>
    public double ShapeWidth
    {
        get
        {
            var src = new FormatConvertedBitmap(Shape, PixelFormats.Bgra32, null, 0);
            int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            src.CopyPixels(px, stride, 0);
            int y = h / 2, min = w, max = -1;
            for (int x = 0; x < w; x++)
                if (px[y * stride + x * 4 + 3] > 128) { min = Math.Min(min, x); max = Math.Max(max, x); }
            return max < 0 ? 1 : (max - min + 1) / (double)w;
        }
    }

    /// <summary>A content color that stands out on the plate: white on dark plates, near-black on light ones.</summary>
    public Color ContrastColor
    {
        get
        {
            if (Back is null) return Colors.White;
            var src = new FormatConvertedBitmap(Back, PixelFormats.Bgra32, null, 0);
            int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
            var px = new byte[stride * h];
            src.CopyPixels(px, stride, 0);
            double sum = 0, n = 0;
            for (int i = 0; i < px.Length; i += 16)
            {
                if (px[i + 3] < 128) continue;
                sum += 0.2126 * px[i + 2] + 0.7152 * px[i + 1] + 0.0722 * px[i];
                n++;
            }
            return n > 0 && sum / n > 150 ? Color.FromRgb(0x20, 0x20, 0x24) : Colors.White;
        }
    }

    private static readonly Dictionary<string, PackStyle> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The style of a pack (cached). Order: style images of the pack (from appfilter.xml, read again from the
    /// original APK for older imports) - otherwise a shape learned from the pack's icons - otherwise neutral.
    /// </summary>
    public static PackStyle For(IconPack? pack)
    {
        if (pack is null) return Neutral();
        lock (Cache)
        {
            if (Cache.TryGetValue(pack.Folder, out var cached)) return cached;
            var style = Load(pack);
            Cache[pack.Folder] = style;
            return style;
        }
    }

    private static PackStyle Load(IconPack pack)
    {
        string folder = Path.Combine(pack.Folder, FolderName);
        // Try the APK only once; the marker avoids re-reading a large APK every time for packs without a style
        string marker = Path.Combine(folder, "checked.txt");
        if (!File.Exists(marker))
        {
            if (!File.Exists(Path.Combine(folder, "back.png")) && !File.Exists(Path.Combine(folder, "mask.png"))) PackImporter.TryExtractStyle(pack);
            try { Directory.CreateDirectory(folder); File.WriteAllText(marker, DateTime.UtcNow.ToString("o")); } catch { }
        }

        var back = Load(Path.Combine(folder, "back.png"));
        var mask = Load(Path.Combine(folder, "mask.png"));
        var upon = Load(Path.Combine(folder, "upon.png"));
        double scale = 1;
        try { double.TryParse(File.ReadAllText(Path.Combine(folder, "style.txt")), NumberStyles.Float, CultureInfo.InvariantCulture, out scale); } catch { }
        if (back is null && mask is null) return Learned(pack, folder) ?? Neutral(pack.Name);

        var keep = mask is null ? null : KeepFromMask(mask);
        // Content may only show where the mask keeps it AND where the plate actually is
        // (some masks are larger than the plate, e.g. Recticons: long shadows would stick out)
        var solidBack = back is null ? null : SolidPart(back);
        if (keep is not null && solidBack is not null) keep = Intersect(keep, solidBack);
        return new PackStyle
        {
            Name = pack.Name,
            Back = back,
            Upon = upon,
            Keep = keep,
            Shape = solidBack ?? keep!,
            Scale = scale > 0 ? scale : 1,
        };
    }

    /// <summary>The shape was derived from the pack's icons (pack without own style images).</summary>
    public bool IsLearned { get; init; }

    /// <summary>
    /// Many packs have no style images, but all their icons share one silhouette (circle, squircle ...).
    /// Lays a few hundred icons on top of each other: if the average outline is crisp, that is the pack's shape.
    /// Saved as style\learned.png (or a "none" marker) so it is computed only once.
    /// </summary>
    private static PackStyle? Learned(IconPack pack, string folder)
    {
        string file = Path.Combine(folder, "learned.png");
        string none = Path.Combine(folder, "learned-none.txt");
        if (File.Exists(none)) return null;
        var shape = Load(file);
        if (shape is null)
        {
            shape = LearnShape(pack);
            try
            {
                Directory.CreateDirectory(folder);
                if (shape is null) { File.WriteAllText(none, "no common shape"); return null; }
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(shape));
                using var fs = File.Create(file);
                enc.Save(fs);
            }
            catch { }
            if (shape is null) return null;
        }
        return new PackStyle { Name = pack.Name, Keep = shape, Shape = shape, IsLearned = true };
    }

    private static BitmapSource? LearnShape(IconPack pack)
    {
        const int S = 128;
        var sum = new double[S * S];
        int used = 0;
        int step = Math.Max(1, pack.Icons.Count / 300);
        for (int i = 0; i < pack.Icons.Count && used < 300; i += step)
        {
            var icon = pack.Icons[i];
            byte[]? data = pack.Thumbs?.Get(icon.Name);
            if (data is null) { try { data = File.ReadAllBytes(pack.PngPath(icon)); } catch { continue; } }
            var img = Imaging.ImageTools.DecodeThumbnail(data, S);
            if (img is null || img.PixelWidth != S || img.PixelHeight != S) continue;
            var px = new byte[S * S * 4];
            new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0).CopyPixels(px, S * 4, 0);
            for (int p = 0; p < S * S; p++) sum[p] += px[p * 4 + 3];
            used++;
        }
        if (used < 30) return null;

        // Crisp = few pixels in the uncertain middle range compared to the covered area
        int covered = 0, uncertain = 0;
        var avg = new double[S * S];
        for (int p = 0; p < S * S; p++)
        {
            avg[p] = sum[p] / used / 255.0;
            if (avg[p] > 0.2) covered++;
            if (avg[p] > 0.2 && avg[p] < 0.8) uncertain++;
        }
        if (covered < S * S * 0.25 || uncertain > covered * 0.18) return null;

        var px2 = new byte[S * S * 4];
        for (int p = 0; p < S * S; p++)
        {
            double t = Math.Clamp((avg[p] - 0.35) / 0.3, 0, 1);
            px2[p * 4 + 3] = (byte)Math.Round(t * t * (3 - 2 * t) * 255);
        }
        var small = BitmapSource.Create(S, S, 96, 96, PixelFormats.Bgra32, null, px2, S * 4);
        var result = Imaging.IcoWriter.Render(small, Size);
        return result;
    }

    /// <summary>Rounded tile in the size of the icon, for packs without style images.</summary>
    public static PackStyle Neutral(string name = "")
    {
        var shape = Render(dc => dc.DrawRoundedRectangle(Brushes.Black, null, new Rect(16, 16, Size - 32, Size - 32), 48, 48));
        var back = Render(dc => dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x34)), null, new Rect(16, 16, Size - 32, Size - 32), 48, 48));
        return new PackStyle { Name = name, Back = back, Keep = shape, Shape = shape, IsNeutral = true };
    }

    private static BitmapSource? Load(string file)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            img.StreamSource = new MemoryStream(File.ReadAllBytes(file));
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    /// <summary>
    /// Android removes the content where the mask is opaque (DST_OUT). Some packs use fully opaque
    /// black/white masks instead; then white means "keep".
    /// </summary>
    private static BitmapSource KeepFromMask(BitmapSource mask)
    {
        var src = new FormatConvertedBitmap(ScaleTo(mask), PixelFormats.Bgra32, null, 0);
        int stride = Size * 4;
        var px = new byte[stride * Size];
        src.CopyPixels(px, stride, 0);
        int minAlpha = 255;
        for (int i = 3; i < px.Length; i += 4) minAlpha = Math.Min(minAlpha, px[i]);
        bool opaqueMask = minAlpha > 250;
        for (int i = 0; i < px.Length; i += 4)
        {
            byte keep = opaqueMask ? (byte)((px[i] + px[i + 1] + px[i + 2]) / 3) : (byte)(255 - px[i + 3]);
            px[i] = px[i + 1] = px[i + 2] = 0;
            px[i + 3] = keep;
        }
        var result = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// Only the solid part of a plate: many plates carry their own soft drop shadow (low alpha around the tile),
    /// which must not count as tile area. Alpha below ~40 % becomes 0, above ~80 % fully opaque, smooth in between.
    /// </summary>
    private static BitmapSource SolidPart(BitmapSource src)
    {
        int stride = Size * 4;
        var px = new byte[stride * Size];
        new FormatConvertedBitmap(ScaleTo(src), PixelFormats.Bgra32, null, 0).CopyPixels(px, stride, 0);
        for (int i = 0; i < px.Length; i += 4)
        {
            double t = Math.Clamp((px[i + 3] - 100) / 105.0, 0, 1);
            px[i] = px[i + 1] = px[i + 2] = 0;
            px[i + 3] = (byte)Math.Round(t * t * (3 - 2 * t) * 255);
        }
        var result = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, px, stride);
        result.Freeze();
        return result;
    }

    /// <summary>Alpha = minimum of both images' alpha.</summary>
    private static BitmapSource Intersect(BitmapSource a, BitmapSource b)
    {
        int stride = Size * 4;
        var pa = new byte[stride * Size];
        var pb = new byte[stride * Size];
        new FormatConvertedBitmap(ScaleTo(a), PixelFormats.Bgra32, null, 0).CopyPixels(pa, stride, 0);
        new FormatConvertedBitmap(ScaleTo(b), PixelFormats.Bgra32, null, 0).CopyPixels(pb, stride, 0);
        for (int i = 0; i < pa.Length; i += 4)
        {
            pa[i] = pa[i + 1] = pa[i + 2] = 0;
            pa[i + 3] = Math.Min(pa[i + 3], pb[i + 3]);
        }
        var result = BitmapSource.Create(Size, Size, 96, 96, PixelFormats.Bgra32, null, pa, stride);
        result.Freeze();
        return result;
    }

    private static BitmapSource ScaleTo(BitmapSource src) =>
        src.PixelWidth == Size && src.PixelHeight == Size ? src : Imaging.IcoWriter.Render(src, Size);

    private static BitmapSource Render(Action<DrawingContext> draw)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) draw(dc);
        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(v);
        rtb.Freeze();
        return rtb;
    }
}
