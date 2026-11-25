using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconWorkshop.Imaging;

/// <summary>Image helpers based on WPF/WIC (PNG, WebP, JPEG, BMP, ICO).</summary>
public static class ImageTools
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool IsPng(byte[] data) => data.Length > 8 && data.AsSpan(0, 8).SequenceEqual(PngSignature);

    /// <summary>Converts any image format WIC can read into PNG. ICO: largest frame.</summary>
    public static byte[] ToPng(byte[] data)
    {
        using var input = new MemoryStream(data);
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.OnLoad);
        BitmapSource frame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight)
                                          .ThenByDescending(f => f.Format.BitsPerPixel).First();
        if (frame.Format != PixelFormats.Bgra32 && frame.Format != PixelFormats.Pbgra32)
            frame = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    /// <summary>Loads an image file as a small, frozen thumbnail (thread-safe, file is not locked).</summary>
    public static BitmapSource? LoadThumbnail(string path, int size)
    {
        try { return DecodeThumbnail(File.ReadAllBytes(path), size); }
        catch { return null; }
    }

    /// <summary>Decodes image bytes as a small, frozen thumbnail (thread-safe).</summary>
    public static BitmapSource? DecodeThumbnail(byte[] bytes, int size)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            img.DecodePixelWidth = size;
            img.StreamSource = new MemoryStream(bytes);
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }
}
