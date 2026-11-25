using System.Text;
using IconWorkshop.Native;

namespace IconWorkshop.Targets;

/// <summary>
/// Sets a folder icon the way Explorer does it: IconResource in the [.ShellClassInfo] section of a hidden
/// desktop.ini, plus the read-only attribute on the folder (which tells Explorer to read desktop.ini).
/// Existing desktop.ini content (e.g. the localized names of Documents, Downloads ...) is kept.
/// </summary>
public static class FolderIconWriter
{
    public static string IniPath(string folder) => Path.Combine(folder, "desktop.ini");

    public static void SetIcon(string folder, string iconLocation)
    {
        string ini = IniPath(folder);
        var (lines, encoding) = Read(ini);

        int start = lines.FindIndex(l => l.Trim().Equals("[.ShellClassInfo]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) { lines.Insert(0, "[.ShellClassInfo]"); start = 0; }
        int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('['));
        if (end < 0) end = lines.Count;
        for (int i = end - 1; i > start; i--)
        {
            string key = lines[i].Split('=')[0].Trim();
            if (key.Equals("IconResource", StringComparison.OrdinalIgnoreCase) || key.Equals("IconFile", StringComparison.OrdinalIgnoreCase)
                || key.Equals("IconIndex", StringComparison.OrdinalIgnoreCase))
            { lines.RemoveAt(i); end--; }
        }
        lines.Insert(start + 1, "IconResource=" + iconLocation);
        Write(ini, lines, encoding);

        // Read-only on the folder makes Explorer evaluate desktop.ini (it doesn't make the folder read-only)
        var attrs = File.GetAttributes(folder);
        if ((attrs & FileAttributes.ReadOnly) == 0) File.SetAttributes(folder, attrs | FileAttributes.ReadOnly);
        Notify(folder);
    }

    /// <summary>Writes back the original desktop.ini (or removes it) and the original folder attributes.</summary>
    public static void Restore(string folder, byte[]? originalIni, FileAttributes? originalIniAttributes, FileAttributes originalFolderAttributes)
    {
        string ini = IniPath(folder);
        if (originalIni is null)
        {
            if (File.Exists(ini)) { File.SetAttributes(ini, FileAttributes.Normal); File.Delete(ini); }
        }
        else
        {
            if (File.Exists(ini)) File.SetAttributes(ini, FileAttributes.Normal);
            File.WriteAllBytes(ini, originalIni);
            File.SetAttributes(ini, originalIniAttributes ?? (FileAttributes.Hidden | FileAttributes.System));
        }
        // Only the two bits that matter for folder customization are put back
        var mask = FileAttributes.ReadOnly | FileAttributes.System;
        var current = File.GetAttributes(folder);
        File.SetAttributes(folder, (current & ~mask) | (originalFolderAttributes & mask));
        Notify(folder);
    }

    private static void Notify(string folder)
    {
        ShellNative.NotifyItemChanged(folder);
        if (Path.GetDirectoryName(folder.TrimEnd('\\')) is { } parent) ShellNative.NotifyItemChanged(parent);
    }

    /// <summary>desktop.ini of user folders is UTF-16 with BOM; new files are written the same way (any path characters work).</summary>
    private static (List<string> Lines, Encoding Encoding) Read(string ini)
    {
        if (!File.Exists(ini)) return (new List<string>(), Encoding.Unicode);
        byte[] data = File.ReadAllBytes(ini);
        Encoding enc;
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE) enc = Encoding.Unicode;
        else if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF) enc = new UTF8Encoding(true);
        else { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); enc = Encoding.GetEncoding(1252); }
        using var reader = new StreamReader(new MemoryStream(data), enc, detectEncodingFromByteOrderMarks: true);
        var lines = reader.ReadToEnd().Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return (lines, enc);
    }

    private static void Write(string ini, List<string> lines, Encoding encoding)
    {
        // Hidden/system files can't be overwritten with File.Create: clear the attributes first
        if (File.Exists(ini)) File.SetAttributes(ini, FileAttributes.Normal);
        // ANSI files can't hold every character of an icon path: switch to UTF-16 then
        if (encoding.CodePage == 1252 && lines.Any(l => l.Any(c => c > 0xFF))) encoding = Encoding.Unicode;
        File.WriteAllText(ini, string.Join("\r\n", lines) + "\r\n", encoding);
        File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
    }
}
