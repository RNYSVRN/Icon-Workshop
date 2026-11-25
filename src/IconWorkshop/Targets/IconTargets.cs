using Microsoft.Win32;

namespace IconWorkshop.Targets;

/// <summary>
/// Something that gets an icon outside of shortcuts:
/// a registry value of the current user (HKCU) - system icons, drives, file types - or a folder (desktop.ini).
/// Changing it needs no admin rights (except for protected folders) and affects only this user.
/// </summary>
public sealed class IconTarget
{
    /// <summary>Unique ID, also the assignment target ("sys:...", "drive:X", "type:.pdf", "folder:path").</summary>
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Subtitle { get; init; }
    public required string Glyph { get; init; }
    /// <summary>Folder whose icon is set via desktop.ini; null for registry targets.</summary>
    public string? FolderPath { get; init; }
    /// <summary>File extension (".pdf") to show the current icon of a file type.</summary>
    public string? Extension { get; init; }
    /// <summary>Not changeable (e.g. file types whose icon is drawn by the program itself); the reason is shown.</summary>
    public string? Unsupported { get; init; }
    /// <summary>Key below HKEY_CURRENT_USER (registry targets).</summary>
    public string KeyPath { get; init; } = "";
    /// <summary>Value name; "" = default value.</summary>
    public string ValueName { get; init; } = "";
    /// <summary>Shell parsing name for the current icon ("::{CLSID}", "C:\").</summary>
    public string? ShellSource { get; init; }
    /// <summary>Windows' own icon location (for icons the shell can't show directly, e.g. Recycle Bin full).</summary>
    public string? DefaultLocation { get; init; }
    /// <summary>Search terms for the icon suggestion, best first.</summary>
    public required string[] SearchTerms { get; init; }
    /// <summary>Extra path to notify Explorer about after a change (drive root).</summary>
    public string? NotifyPath { get; init; }

    /// <summary>The icon location currently set for this user (null = Windows default).</summary>
    public string? CurrentValue
    {
        get
        {
            if (FolderPath is not null || KeyPath.Length == 0) return null;
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(KeyPath);
                return k?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            }
            catch { return null; }
        }
    }
}

/// <summary>The Windows desktop and system icons that can be changed per user.</summary>
public static class SystemIcons
{
    private const string ClsidBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\";

    private static string DefaultIconOf(string clsid, string value)
    {
        try
        {
            using var k = Registry.ClassesRoot.OpenSubKey($@"CLSID\{clsid}\DefaultIcon");
            return k?.GetValue(value) as string ?? "";
        }
        catch { return ""; }
    }

    private static IconTarget Clsid(string clsid, string glyph, string de, string en, string descDe, string descEn, string[] terms, string value = "", bool useLocation = false) => new()
    {
        Id = $"sys:{clsid}|{value}".ToLowerInvariant(),
        Name = Loc.T(de, en),
        Subtitle = Loc.T(descDe, descEn),
        Glyph = glyph,
        KeyPath = ClsidBase + clsid + @"\DefaultIcon",
        ValueName = value,
        ShellSource = useLocation ? null : "::" + clsid,
        DefaultLocation = useLocation ? DefaultIconOf(clsid, value) : null,
        SearchTerms = terms,
    };

    public static List<IconTarget> All() =>
    [
        Clsid("{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "\uE7F4", "Dieser PC", "This PC", "Früher „Arbeitsplatz“ bzw. „Computer“", "Formerly “My Computer”",
              ["my files", "file manager", "files", "my computer", "laptop", "computer"]),
        Clsid("{59031a47-3f72-44a7-89c5-5595fe6b30ee}", "\uE77B", "Benutzerordner", "User folder", "Dein persönlicher Ordner", "Your personal folder",
              ["profile", "account", "contacts", "person", "user"]),
        Clsid("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", "\uE968", "Netzwerk", "Network", "Andere Geräte im Netzwerk", "Other devices on the network",
              ["network monitor", "network", "wifi", "router"]),
        Clsid("{645FF040-5081-101B-9F08-00AA002F954E}", "\uE74D", "Papierkorb (leer)", "Recycle Bin (empty)", "Wird angezeigt, wenn der Papierkorb leer ist", "Shown when the Recycle Bin is empty",
              ["recycle bin", "recycle", "trash", "dustbin", "garbage", "cleaner"], "empty", useLocation: true),
        Clsid("{645FF040-5081-101B-9F08-00AA002F954E}", "\uE74D", "Papierkorb (voll)", "Recycle Bin (full)", "Wird angezeigt, wenn etwas im Papierkorb liegt", "Shown when there is something in the Recycle Bin",
              ["recycle bin full", "trash full", "recycle bin", "recycle", "trash", "dustbin", "garbage", "cleaner"], "full", useLocation: true),
        Clsid("{26EE0668-A00A-44D7-9371-BEB064C98683}", "\uE713", "Systemsteuerung", "Control Panel", "Klassische Windows-Einstellungen", "Classic Windows settings",
              ["settings", "control panel", "system settings", "tools"]),
        Clsid("{031E4825-7B94-4dc3-B131-E946B44C8DD5}", "\uE8F1", "Bibliotheken", "Libraries", "Dokumente, Bilder, Musik und Videos zusammengefasst", "Documents, pictures, music and videos combined",
              ["library", "bookshelf", "books", "folder"]),
    ];
}

/// <summary>Drive letters; the icon is set per user and also applies to drives that are currently not connected.</summary>
public static class DriveIcons
{
    public const string Base = @"Software\Classes\Applications\Explorer.exe\Drives\";

    public static IconTarget For(char letter, DriveInfo? drive)
    {
        letter = char.ToUpperInvariant(letter);
        var type = drive?.DriveType ?? DriveType.Unknown;
        string label = "";
        try { if (drive is { IsReady: true }) label = drive.VolumeLabel; } catch { }

        string typeText = type switch
        {
            DriveType.Fixed => Loc.T("Lokaler Datenträger", "Local disk"),
            DriveType.Removable => Loc.T("Wechseldatenträger", "Removable drive"),
            DriveType.Network => Loc.T("Netzlaufwerk", "Network drive"),
            DriveType.CDRom => Loc.T("CD/DVD-Laufwerk", "CD/DVD drive"),
            DriveType.Ram => Loc.T("RAM-Disk", "RAM disk"),
            _ => Loc.T("Derzeit nicht verbunden", "Currently not connected"),
        };
        bool system = string.Equals(Path.GetPathRoot(Environment.SystemDirectory), $"{letter}:\\", StringComparison.OrdinalIgnoreCase);
        string[] terms = type switch
        {
            DriveType.Removable => ["usb", "sd card", "pendrive", "storage", "files"],
            DriveType.Network => ["nas", "synology", "network drive", "server", "cloud"],
            DriveType.CDRom => ["dvd", "cd", "disc", "music"],
            _ when system => ["windows", "disk usage", "diskinfo", "storage", "hard disk"],
            _ => ["disk usage", "diskinfo", "storage", "hard disk", "hdd", "disk"],
        };
        return new IconTarget
        {
            Id = $"drive:{letter}".ToLowerInvariant(),
            Name = string.IsNullOrWhiteSpace(label) ? $"{letter}:" : $"{label} ({letter}:)",
            Subtitle = system ? typeText + Loc.T(" · Windows", " · Windows") : typeText,
            Glyph = type switch { DriveType.Removable => "\uE88E", DriveType.Network => "\uE8CE", DriveType.CDRom => "\uE958", _ => "\uEDA2" },
            KeyPath = Base + letter + @"\DefaultIcon",
            ShellSource = $"{letter}:\\",
            SearchTerms = terms,
            NotifyPath = $"{letter}:\\",
        };
    }

    /// <summary>All connected drives plus letters that already have a custom icon (e.g. a USB stick that is unplugged).</summary>
    public static List<IconTarget> All(IEnumerable<char> extraLetters)
    {
        var drives = DriveInfo.GetDrives().ToDictionary(d => char.ToUpperInvariant(d.Name[0]));
        var letters = new SortedSet<char>(drives.Keys);
        foreach (var l in extraLetters) letters.Add(char.ToUpperInvariant(l));
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Base.TrimEnd('\\'));
            if (k is not null)
                foreach (var sub in k.GetSubKeyNames())
                    if (sub.Length == 1 && char.IsLetter(sub[0])) letters.Add(char.ToUpperInvariant(sub[0]));
        }
        catch { }
        return letters.Select(l => For(l, drives.GetValueOrDefault(l))).ToList();
    }
}

/// <summary>File types: the icon is set per user on the program ID that currently opens the type (HKCU\Software\Classes).</summary>
public static class FileTypeIcons
{
    /// <summary>Common types shown by default; more can be added on the page.</summary>
    public static readonly string[] Curated =
    [
        ".pdf", ".txt", ".zip", ".7z", ".rar", ".mp3", ".flac", ".wav", ".mp4", ".mkv", ".avi", ".mov",
        ".jpg", ".png", ".gif", ".svg", ".psd", ".docx", ".xlsx", ".pptx", ".csv", ".html", ".json", ".xml",
        ".md", ".log", ".iso", ".py", ".ps1", ".bat",
    ];

    public static List<IconTarget> All(IEnumerable<string> extra, IEnumerable<string> assignedIds)
    {
        var exts = new List<string>(Curated);
        foreach (var e in extra.Concat(assignedIds.Where(id => id.StartsWith("type:")).Select(id => id[5..])))
            if (!exts.Contains(e, StringComparer.OrdinalIgnoreCase)) exts.Add(e.ToLowerInvariant());
        return exts.Select(For).ToList();
    }

    public static bool IsValidExtension(string ext) =>
        ext.Length is >= 2 and <= 20 && ext[0] == '.' && ext.Skip(1).All(c => char.IsLetterOrDigit(c) || c is '_' or '-');

    /// <summary>The program ID Windows uses for the extension: the user's choice first, then the registered default.</summary>
    public static string? ProgIdOf(string ext)
    {
        try
        {
            using (var uc = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice"))
                if (uc?.GetValue("ProgId") is string p && p.Length > 0) return p;
            using var k = Registry.ClassesRoot.OpenSubKey(ext);
            return k?.GetValue("") as string is { Length: > 0 } d ? d : null;
        }
        catch { return null; }
    }

    public static IconTarget For(string ext)
    {
        ext = ext.ToLowerInvariant();
        string? progId = ProgIdOf(ext);
        string? typeName = null, defaultIcon = null;
        try
        {
            using var k = Registry.ClassesRoot.OpenSubKey(progId ?? ext);
            typeName = k?.GetValue("") as string;
            using var di = Registry.ClassesRoot.OpenSubKey((progId ?? ext) + @"\DefaultIcon");
            defaultIcon = di?.GetValue("", null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch { }
        // Windows app associations (AppX...) have no readable name in the registry: ask the shell
        if (string.IsNullOrWhiteSpace(typeName) || typeName == progId || typeName.StartsWith('@'))
            typeName = IconWorkshop.Native.ShellNative.TypeNameForExtension(ext);

        // "%1" means the file draws its own icon (e.g. .ico, .exe): can't be replaced per type
        string? unsupported = defaultIcon?.Trim() == "%1"
            ? Loc.T("Dateien dieses Typs zeigen ihr eigenes Icon an, deshalb lässt sich hier kein Icon für den Typ festlegen.",
                    "Files of this type show their own icon, so no icon can be set for the type.")
            : null;

        return new IconTarget
        {
            Id = "type:" + ext,
            Name = ext.TrimStart('.').ToUpperInvariant(),
            Subtitle = typeName ?? (progId is null ? Loc.T("Keinem Programm zugeordnet", "Not associated with a program") : progId),
            Glyph = "",
            Extension = ext,
            KeyPath = $@"Software\Classes\{progId ?? ext}\DefaultIcon",
            DefaultLocation = defaultIcon,
            Unsupported = unsupported,
            SearchTerms = TermsFor(ext),
        };
    }

    private static string[] TermsFor(string ext) => ext switch
    {
        ".pdf" => ["pdf", "adobe acrobat", "pdf reader", "document"],
        ".txt" or ".log" => ["notes", "notepad", "text", "editor"],
        ".md" => ["markdown", "notes", "obsidian", "editor"],
        ".zip" or ".7z" or ".rar" => ["zarchiver", "archive", "zip", "rar", "7zip", "winrar"],
        ".mp3" or ".flac" or ".wav" => ["music", "audio", "music player", "mp3"],
        ".mp4" or ".mkv" or ".avi" or ".mov" => ["video player", "video", "vlc", "movie"],
        ".jpg" or ".png" or ".gif" or ".svg" => ["gallery", "photos", "image", "picture"],
        ".psd" => ["photoshop", "image editor"],
        ".docx" or ".doc" => ["word", "docs", "document"],
        ".xlsx" or ".xls" or ".csv" => ["excel", "sheets", "spreadsheet"],
        ".pptx" or ".ppt" => ["powerpoint", "slides", "presentation"],
        ".html" or ".htm" => ["browser", "html", "web", "chrome"],
        ".json" or ".xml" => ["json", "code", "editor"],
        ".py" => ["python", "code", "pydroid"],
        ".ps1" or ".bat" => ["terminal", "termux", "console", "code"],
        ".iso" => ["disc", "dvd", "iso"],
        _ => [ext.TrimStart('.'), "file", "document"],
    };
}

/// <summary>Folders: user folders for quick access plus any folder the user adds (icon via desktop.ini).</summary>
public static class FolderIcons
{
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);

    private static string? KnownFolder(Guid id)
    {
        if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out var p) != 0) return null;
        try { return System.Runtime.InteropServices.Marshal.PtrToStringUni(p); }
        finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(p); }
    }

    private static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");

    public static List<IconTarget> All(IEnumerable<string> customFolders, IEnumerable<string> assignedIds)
    {
        var list = new List<IconTarget>();
        void Add(string? path, string glyph, string[] terms, bool quick)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            string id = IdOf(path);
            if (list.Any(t => t.Id == id)) return;
            list.Add(new IconTarget
            {
                Id = id,
                Name = Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } n ? n : path,
                Subtitle = quick ? Loc.T("Benutzerordner · ", "User folder · ") + path : path,
                Glyph = glyph,
                FolderPath = path,
                ShellSource = path,
                SearchTerms = terms,
            });
        }

        Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "", ["folder general", "folder generic", "folder", "desktop"], true);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "", ["folder work", "folder productivity", "folder documents", "documents", "folder"], true);
        Add(KnownFolder(Downloads), "", ["folder download", "folder sync", "downloads", "download", "folder"], true);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "", ["folder photos", "folder photography", "gallery", "photos", "folder"], true);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "", ["folder music", "music", "folder"], true);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "", ["folder movies", "folder tv", "videos", "video", "folder"], true);

        foreach (var path in customFolders.Concat(assignedIds.Where(id => id.StartsWith("folder:")).Select(PathFromId)))
            Add(path, "", ["folder " + Path.GetFileName(path.TrimEnd('\\')), Path.GetFileName(path.TrimEnd('\\')), "folder generic", "folder"], false);
        return list;
    }

    public static string IdOf(string path) => "folder:" + Path.GetFullPath(path).TrimEnd('\\').ToLowerInvariant();

    private static string PathFromId(string id) => id["folder:".Length..];
}
