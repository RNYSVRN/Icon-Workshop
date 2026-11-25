using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace IconWorkshop.Native;

/// <summary>Win32/COM declarations for the Windows shell (shortcuts, icons, Apps folder, change notifications).</summary>
internal static partial class ShellNative
{
    // ---------- Change notifications ----------
    public const int SHCNE_UPDATEITEM = 0x00002000;
    public const int SHCNE_ASSOCCHANGED = 0x08000000;
    public const uint SHCNF_IDLIST = 0x0000;
    public const uint SHCNF_PATHW = 0x0005;
    public const uint SHCNF_FLUSHNOWAIT = 0x3000;

    [LibraryImport("shell32.dll", EntryPoint = "SHChangeNotify", StringMarshalling = StringMarshalling.Utf16)]
    public static partial void SHChangeNotify(int eventId, uint flags, string? item1, IntPtr item2);

    /// <summary>Tells Explorer that a file's icon changed.</summary>
    public static void NotifyItemChanged(string path) => SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSHNOWAIT, path, IntPtr.Zero);

    /// <summary>Tells Explorer to refresh icons in general (after a batch of changes).</summary>
    public static void NotifyIconsChanged() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, null, IntPtr.Zero);

    // ---------- Shell items ----------
    public static readonly Guid IID_IShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    public static readonly Guid IID_IShellItemImageFactory = new("bcc18b79-ba16-442f-80c4-8a59c30c463b");
    public static readonly Guid BHID_EnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    public static readonly Guid IID_IEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHCreateItemFromParsingName(string path, IntPtr bindCtx, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHParseDisplayName(string name, IntPtr bindCtx, out IntPtr pidl, uint sfgaoIn, out uint sfgaoOut);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    public static extern void SHGetNameFromIDList(IntPtr pidl, SIGDN sigdn, out IntPtr name);

    [DllImport("ole32.dll")]
    public static extern void CoTaskMemFree(IntPtr ptr);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr obj);

    public enum SIGDN : uint
    {
        NORMALDISPLAY = 0,
        PARENTRELATIVEPARSING = 0x80018001,
        DESKTOPABSOLUTEPARSING = 0x80028000,
        FILESYSPATH = 0x80058000,
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, in Guid bhid, in Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, Guid("70629033-e363-4a28-a567-0db78006e6d7"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IEnumShellItems
    {
        [PreserveSig] int Next(uint celt, [MarshalAs(UnmanagedType.Interface)] out IShellItem rgelt, out uint pceltFetched);
        void Skip(uint celt);
        void Reset();
        void Clone(out IEnumShellItems ppenum);
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; public SIZE(int x, int y) { cx = x; cy = y; } }

    public static string? DisplayName(IShellItem item, SIGDN kind)
    {
        try
        {
            item.GetDisplayName(kind, out var p);
            string? s = Marshal.PtrToStringUni(p);
            CoTaskMemFree(p);
            return s;
        }
        catch { return null; }
    }

    /// <summary>The icon of any shell item (file, shortcut, "shell:AppsFolder\AUMID" ...) as a frozen bitmap.</summary>
    public static BitmapSource? GetItemImage(string parsingName, int size)
    {
        IntPtr hbmp = IntPtr.Zero;
        try
        {
            SHCreateItemFromParsingName(parsingName, IntPtr.Zero, IID_IShellItemImageFactory, out var obj);
            var factory = (IShellItemImageFactory)obj;
            // SIIGBF_BIGGERSIZEOK (0x1) | SIIGBF_ICONONLY (0x4): the icon, not a thumbnail of the content
            if (factory.GetImage(new SIZE(size, size), 0x1 | 0x4, out hbmp) != 0 || hbmp == IntPtr.Zero) return null;
            Marshal.ReleaseComObject(obj);
            return FromHBitmap(hbmp);
        }
        catch { return null; }
        finally { if (hbmp != IntPtr.Zero) DeleteObject(hbmp); }
    }

    // SHDefExtractIcon handles resource IDs (negative index) and the .mun redirection of system DLLs (imageres.dll)
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(string iconFile, int index, uint flags, out IntPtr large, out IntPtr small, uint sizes);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    /// <summary>Icon from a location like "%SystemRoot%\System32\imageres.dll,-55" (negative = resource ID).</summary>
    public static BitmapSource? IconFromLocation(string? location, int size)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        string path = location.Trim();
        int index = 0;
        int comma = path.LastIndexOf(',');
        if (comma > 0 && int.TryParse(path[(comma + 1)..].Trim(), out int i)) { index = i; path = path[..comma]; }
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        IntPtr large = IntPtr.Zero, small = IntPtr.Zero;
        try
        {
            if (SHDefExtractIcon(path, index, 0, out large, out small, (uint)size | (16u << 16)) != 0 || large == IntPtr.Zero) return null;
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(large, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
        finally
        {
            if (large != IntPtr.Zero) DestroyIcon(large);
            if (small != IntPtr.Zero) DestroyIcon(small);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int list, in Guid riid, out IntPtr imageList);

    [DllImport("comctl32.dll")]
    private static extern IntPtr ImageList_GetIcon(IntPtr imageList, int index, uint flags);

    /// <summary>The type name Explorer shows for an extension ("PDF-Dokument", "MP3-Datei" ...).</summary>
    public static string? TypeNameForExtension(string extension)
    {
        var info = new SHFILEINFO();
        // SHGFI_TYPENAME | SHGFI_USEFILEATTRIBUTES
        if (SHGetFileInfo("file" + extension, 0x80, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x400 | 0x10) == IntPtr.Zero) return null;
        return string.IsNullOrWhiteSpace(info.szTypeName) ? null : info.szTypeName;
    }

    /// <summary>The icon Explorer shows for a file type (".pdf"), 48 px from the system image list.</summary>
    public static BitmapSource? IconForExtension(string extension)
    {
        var info = new SHFILEINFO();
        // SHGFI_SYSICONINDEX | SHGFI_USEFILEATTRIBUTES: no real file needed
        if (SHGetFileInfo("file" + extension, 0x80, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x4000 | 0x10) == IntPtr.Zero) return null;
        var iidImageList = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
        if (SHGetImageList(2 /* SHIL_EXTRALARGE, 48 px */, iidImageList, out var list) != 0 || list == IntPtr.Zero) return null;
        IntPtr icon = ImageList_GetIcon(list, info.iIcon, 0x1 /* ILD_TRANSPARENT */);
        if (icon == IntPtr.Zero) return null;
        try
        {
            var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
        finally { DestroyIcon(icon); }
    }

    // CreateBitmapSourceFromHBitmap drops the alpha channel of shell bitmaps (black corners).
    // GetDIBits returns the premultiplied BGRA pixels top-down, which map 1:1 to Pbgra32.
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight; public ushort biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("gdi32.dll")] private static extern int GetObject(IntPtr h, int size, out BITMAP bmp);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    private static BitmapSource? FromHBitmap(IntPtr hbmp)
    {
        if (GetObject(hbmp, Marshal.SizeOf<BITMAP>(), out var bm) == 0) return null;
        int w = bm.bmWidth, h = bm.bmHeight, stride = w * 4;
        var bi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
        var px = new byte[stride * h];
        IntPtr dc = GetDC(IntPtr.Zero);
        try { if (GetDIBits(dc, hbmp, 0, (uint)h, px, ref bi, 0) == 0) return null; }
        finally { ReleaseDC(IntPtr.Zero, dc); }
        // Bitmaps without any alpha (old icons) would be fully transparent: make them opaque
        bool anyAlpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { anyAlpha = true; break; }
        if (!anyAlpha) for (int i = 3; i < px.Length; i += 4) px[i] = 255;
        var src = BitmapSource.Create(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, px, stride);
        src.Freeze();
        return src;
    }

    /// <summary>Apps of the Windows "Apps" folder (shell:AppsFolder): display name and parsing name (AUMID or path).</summary>
    public static List<(string Name, string ParsingName)> EnumerateAppsFolder()
    {
        var result = new List<(string, string)>();
        try
        {
            SHCreateItemFromParsingName("shell:AppsFolder", IntPtr.Zero, IID_IShellItem, out var obj);
            var folder = (IShellItem)obj;
            folder.BindToHandler(IntPtr.Zero, BHID_EnumItems, IID_IEnumShellItems, out var e);
            var en = (IEnumShellItems)e;
            while (en.Next(1, out var item, out uint fetched) == 0 && fetched == 1)
            {
                string? name = DisplayName(item, SIGDN.NORMALDISPLAY);
                string? parsing = DisplayName(item, SIGDN.PARENTRELATIVEPARSING);
                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(parsing)) result.Add((name, parsing));
                Marshal.ReleaseComObject(item);
            }
        }
        catch { /* Apps folder not available */ }
        return result;
    }
}
