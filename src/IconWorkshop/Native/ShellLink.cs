using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace IconWorkshop.Native;

/// <summary>What a shortcut (.lnk or .url) points to and which icon it shows.</summary>
public sealed record LinkInfo(string Path, string? Target, string? Arguments, string? WorkingDirectory,
                              string? AppUserModelId, string? Url, string IconPath, int IconIndex);

/// <summary>Reads and writes Windows shortcuts: .lnk via IShellLink, .url as INI text.</summary>
public static class ShellLink
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class CShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr fd, uint flags);
        void GetIDList(out IntPtr pidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCmd);
        void SetShowCmd(int showCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int cch, out int icon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int icon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr hwnd, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint count);
        void GetAt(uint index, out PROPERTYKEY key);
        void GetValue(in PROPERTYKEY key, out PROPVARIANT value);
        void SetValue(in PROPERTYKEY key, in PROPVARIANT value);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT { public ushort vt; public ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PROPVARIANT pv);

    private static readonly PROPERTYKEY PKEY_AppUserModel_ID = new() { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
    private const ushort VT_LPWSTR = 31;
    private const int STGM_READ = 0;

    public static bool IsLnk(string path) => path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
    public static bool IsUrl(string path) => path.EndsWith(".url", StringComparison.OrdinalIgnoreCase);

    public static LinkInfo? Read(string path)
    {
        try { return IsUrl(path) ? ReadUrl(path) : IsLnk(path) ? ReadLnk(path) : null; }
        catch { return null; }
    }

    private static LinkInfo ReadLnk(string path)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(path, STGM_READ);
            var sb = new StringBuilder(1024);
            link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0x4 /* SLGP_RAWPATH */);
            string? target = Empty(Environment.ExpandEnvironmentVariables(sb.ToString()));
            sb.Clear(); link.GetArguments(sb, sb.Capacity);
            string? args = Empty(sb.ToString());
            sb.Clear(); link.GetWorkingDirectory(sb, sb.Capacity);
            string? wd = Empty(sb.ToString());
            sb.Clear(); link.GetIconLocation(sb, sb.Capacity, out int index);
            string icon = sb.ToString();

            string? aumid = ReadAumid(link);
            if (target is null && aumid is null) aumid = AumidFromIdList(link);
            return new LinkInfo(path, target, args, wd, aumid, null, icon, index);
        }
        finally { Marshal.ReleaseComObject(link); }
    }

    private static string? ReadAumid(IShellLinkW link)
    {
        try
        {
            var store = (IPropertyStore)link;
            store.GetValue(PKEY_AppUserModel_ID, out var pv);
            try { return pv.vt == VT_LPWSTR ? Empty(Marshal.PtrToStringUni(pv.p)) : null; }
            finally { PropVariantClear(ref pv); }
        }
        catch { return null; }
    }

    /// <summary>Store app shortcuts point to "shell:AppsFolder\&lt;AUMID&gt;" via an ID list instead of a path.</summary>
    private static string? AumidFromIdList(IShellLinkW link)
    {
        try
        {
            link.GetIDList(out var pidl);
            if (pidl == IntPtr.Zero) return null;
            try
            {
                ShellNative.SHGetNameFromIDList(pidl, ShellNative.SIGDN.DESKTOPABSOLUTEPARSING, out var p);
                string? name = Marshal.PtrToStringUni(p);
                ShellNative.CoTaskMemFree(p);
                if (name is null) return null;
                string last = name[(name.LastIndexOf('\\') + 1)..];
                return last.Contains('!') ? last : null;
            }
            finally { ShellNative.CoTaskMemFree(pidl); }
        }
        catch { return null; }
    }

    private static LinkInfo ReadUrl(string path)
    {
        string? url = null, iconFile = null;
        int iconIndex = 0;
        bool inSection = false;
        foreach (var raw in ReadUrlLines(path, out _))
        {
            string line = raw.Trim();
            if (line.StartsWith('[')) { inSection = line.Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!inSection) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line[..eq].Trim(), value = line[(eq + 1)..].Trim();
            if (key.Equals("URL", StringComparison.OrdinalIgnoreCase)) url = value;
            else if (key.Equals("IconFile", StringComparison.OrdinalIgnoreCase)) iconFile = value;
            else if (key.Equals("IconIndex", StringComparison.OrdinalIgnoreCase)) int.TryParse(value, out iconIndex);
        }
        return new LinkInfo(path, null, null, null, null, url, iconFile ?? "", iconIndex);
    }

    /// <summary>Sets the icon of a shortcut. An empty icon path removes the custom icon (Windows uses the target's icon).</summary>
    public static void SetIcon(string path, string iconPath, int index)
    {
        if (IsUrl(path)) { SetUrlIcon(path, iconPath, index); return; }
        var link = (IShellLinkW)new CShellLink();
        try
        {
            var pf = (IPersistFile)link;
            pf.Load(path, 2 /* STGM_READWRITE */);
            link.SetIconLocation(iconPath, index);
            pf.Save(path, true);
        }
        finally { Marshal.ReleaseComObject(link); }
        ShellNative.NotifyItemChanged(path);
    }

    private static void SetUrlIcon(string path, string iconPath, int index)
    {
        var lines = ReadUrlLines(path, out var encoding).ToList();
        int start = lines.FindIndex(l => l.Trim().Equals("[InternetShortcut]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) { lines.Insert(0, "[InternetShortcut]"); start = 0; }
        int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
        if (end < 0) end = lines.Count;
        for (int i = end - 1; i > start; i--)
        {
            string k = lines[i].Split('=')[0].Trim();
            if (k.Equals("IconFile", StringComparison.OrdinalIgnoreCase) || k.Equals("IconIndex", StringComparison.OrdinalIgnoreCase))
            { lines.RemoveAt(i); end--; }
        }
        if (iconPath.Length > 0) lines.InsertRange(end, [$"IconFile={iconPath}", $"IconIndex={index}"]);
        File.WriteAllLines(path, lines, encoding);
        ShellNative.NotifyItemChanged(path);
    }

    /// <summary>Creates a .lnk to a program (exe path) or a Store app (AUMID) with the given icon.</summary>
    public static void Create(string linkPath, string? exePath, string? arguments, string? workingDirectory, string? aumid, string iconPath)
    {
        var link = (IShellLinkW)new CShellLink();
        IntPtr pidl = IntPtr.Zero;
        try
        {
            if (aumid is not null)
            {
                ShellNative.SHParseDisplayName(@"shell:AppsFolder\" + aumid, IntPtr.Zero, out pidl, 0, out _);
                link.SetIDList(pidl);
                SetAumid(link, aumid);
            }
            else if (exePath is not null)
            {
                link.SetPath(exePath);
                if (!string.IsNullOrEmpty(arguments)) link.SetArguments(arguments);
                link.SetWorkingDirectory(workingDirectory ?? System.IO.Path.GetDirectoryName(exePath) ?? "");
            }
            link.SetIconLocation(iconPath, 0);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
            ((IPersistFile)link).Save(linkPath, true);
        }
        finally
        {
            if (pidl != IntPtr.Zero) ShellNative.CoTaskMemFree(pidl);
            Marshal.ReleaseComObject(link);
        }
        ShellNative.NotifyItemChanged(linkPath);
    }

    /// <summary>Same AUMID as the app, so the shortcut groups with the running app on the taskbar.</summary>
    private static void SetAumid(IShellLinkW link, string aumid)
    {
        var pv = new PROPVARIANT { vt = VT_LPWSTR, p = Marshal.StringToCoTaskMemUni(aumid) };
        try
        {
            var store = (IPropertyStore)link;
            store.SetValue(PKEY_AppUserModel_ID, pv);
            store.Commit();
        }
        catch { /* not essential */ }
        finally { PropVariantClear(ref pv); }
    }

    /// <summary>"C:\x.exe,3" style location to path and index. The index of a shortcut icon is often negative (resource ID).</summary>
    public static string FormatLocation(string path, int index) => path.Length == 0 ? "" : $"{path},{index}";

    /// <summary>.url files are usually ANSI (Windows-1252), sometimes UTF-8/UTF-16 with BOM. Written back in the same encoding.</summary>
    private static string[] ReadUrlLines(string path, out Encoding encoding)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) encoding = Encoding.Unicode;
        else if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) encoding = new UTF8Encoding(true);
        else
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encoding = Encoding.GetEncoding(1252);
        }
        using var reader = new StreamReader(new MemoryStream(data), encoding, detectEncodingFromByteOrderMarks: true);
        var lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return lines.ToArray();
    }

    private static string? Empty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
