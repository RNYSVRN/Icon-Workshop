using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconWorkshop.Imaging;

/// <summary>
/// Writes Windows .ico files with the sizes Explorer, taskbar and Start menu use (16 to 256 px).
/// Frames up to 128 px are stored as 32-bit BMP (maximum compatibility), 256 px as PNG.
/// </summary>
public static class IcoWriter
{
    public static readonly int[] DefaultSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    /// <summary>Creates an ICO from an image (PNG, WebP ...), scaling it to all sizes with high quality.</summary>
    public static void FromImage(byte[] imageData, string targetFile, int[]? sizes = null)
    {
        using var input = new MemoryStream(imageData);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        BitmapSource source = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
        Write(targetFile, (sizes ?? DefaultSizes).Select(s => (s, Render(source, s))).ToList());
    }

    /// <summary>Scales an image square into size x size (aspect ratio kept, centered, transparent margin).</summary>
    public static BitmapSource Render(BitmapSource source, int size)
    {
        double scale = Math.Min((double)size / source.PixelWidth, (double)size / source.PixelHeight);
        double w = source.PixelWidth * scale, h = source.PixelHeight * scale;
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.Fant);
        using (var dc = visual.RenderOpen())
            dc.DrawImage(source, new Rect((size - w) / 2, (size - h) / 2, w, h));
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    public static void Write(string targetFile, IReadOnlyList<(int Size, BitmapSource Bitmap)> frames)
    {
        var payloads = frames.Select(f => f.Size >= 256 ? EncodePng(f.Bitmap) : EncodeBmp(f.Bitmap)).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetFile))!);
        string tmp = targetFile + ".tmp";
        using (var fs = File.Create(tmp))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write((short)0); bw.Write((short)1); bw.Write((short)frames.Count);
            int offset = 6 + 16 * frames.Count;
            for (int i = 0; i < frames.Count; i++)
            {
                int s = frames[i].Size;
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((short)1); bw.Write((short)32);
                bw.Write(payloads[i].Length);
                bw.Write(offset);
                offset += payloads[i].Length;
            }
            foreach (var p in payloads) bw.Write(p);
        }
        File.Move(tmp, targetFile, overwrite: true);
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    /// <summary>BITMAPINFOHEADER + bottom-up BGRA pixels (straight alpha) + AND mask.</summary>
    private static byte[] EncodeBmp(BitmapSource bitmap)
    {
        var bgra = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        int w = bgra.PixelWidth, h = bgra.PixelHeight, stride = w * 4;
        var pixels = new byte[stride * h];
        bgra.CopyPixels(pixels, stride, 0);
        int maskStride = ((w + 31) / 32) * 4;

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(40); bw.Write(w); bw.Write(h * 2);
        bw.Write((short)1); bw.Write((short)32);
        bw.Write(0); bw.Write(stride * h + maskStride * h);
        bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
        for (int y = h - 1; y >= 0; y--) bw.Write(pixels, y * stride, stride);
        var mask = new byte[maskStride];
        for (int y = h - 1; y >= 0; y--)
        {
            Array.Clear(mask);
            for (int x = 0; x < w; x++)
                if (pixels[y * stride + x * 4 + 3] == 0) mask[x / 8] |= (byte)(0x80 >> (x % 8));
            bw.Write(mask);
        }
        return ms.ToArray();
    }
}
