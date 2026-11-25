using System.Security.Principal;
using IconWorkshop.Native;
using Microsoft.Win32;

namespace IconWorkshop.Programs;

public enum LinkLocation { Desktop, StartMenu, Taskbar }

public enum ProgramKind { Shortcuts, InstalledOnly, StoreApp }

/// <summary>A shortcut file found on the desktop, in the Start menu or on the taskbar.</summary>
public sealed class ShortcutRef
{
    public required string Path { get; init; }
    public required LinkLocation Location { get; init; }
    public required bool AllUsers { get; init; }
    public required LinkInfo Info { get; init; }

    /// <summary>Shortcuts of all users can only be changed with admin rights.</summary>
    public bool NeedsAdmin => AllUsers && !ProgramScanner.IsElevated;

    public string LocationLabel => ProgramScanner.LocationLabel(Location, AllUsers);
}

/// <summary>A program with all its shortcuts (same target and arguments, or same Store app).</summary>
public sealed class ProgramEntry
{
    public required string Key { get; init; }
    public required string Name { get; set; }
    public required ProgramKind Kind { get; init; }
    public List<ShortcutRef> Shortcuts { get; } = new();
    public string? ExePath { get; init; }
    public string? Aumid { get; init; }
    /// <summary>Steam or Epic game (from its shortcut).</summary>
    public GameInfo? Game { get; set; }
    /// <summary>All shortcuts point to an icon file that doesn't exist; Windows shows a blank icon.</summary>
    public bool IconMissing { get; set; }

    /// <summary>Shell parsing name used to show the current icon.</summary>
    public string IconSource => Shortcuts.FirstOrDefault()?.Path ?? (Aumid is not null ? @"shell:AppsFolder\" + Aumid : ExePath ?? "");

    public string Subtitle => Kind switch
    {
        ProgramKind.StoreApp when Shortcuts.Count == 0 => Loc.T("Windows-App · ohne Verknüpfung", "Windows app · no shortcut"),
        ProgramKind.InstalledOnly when Shortcuts.Count == 0 => Loc.T("Installiert · ohne Verknüpfung", "Installed · no shortcut"),
        _ => string.Join(" · ", (Game is { } g ? [g.Label] : Array.Empty<string>())
                 .Concat(Shortcuts.Select(s => s.LocationLabel).Distinct())
                 .Concat(IconMissing ? [Loc.T("Icon fehlt", "Icon missing")] : Array.Empty<string>())),
    };
}

/// <summary>Finds programs: shortcuts (desktop, Start menu, pinned taskbar), installed programs and Windows/Store apps.</summary>
public static class ProgramScanner
{
    public static bool IsElevated { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static string TaskbarFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    /// <summary>Folder for shortcuts created by the app in the user's Start menu.</summary>
    public static string OwnStartMenuFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Icon Workshop");

    public static string LocationLabel(LinkLocation location, bool allUsers)
    {
        string l = location switch
        {
            LinkLocation.Desktop => Loc.T("Desktop", "Desktop"),
            LinkLocation.StartMenu => Loc.T("Startmenü", "Start menu"),
            _ => Loc.T("Taskleiste", "Taskbar"),
        };
        return allUsers ? l + Loc.T(" (alle Benutzer)", " (all users)") : l;
    }

    /// <summary>Short label for where a shortcut file lives ("Desktop", "Startmenü (alle Benutzer)" ...), null if elsewhere.</summary>
    public static string? LocationLabelOf(string path)
    {
        var places = new (Environment.SpecialFolder Folder, LinkLocation Location, bool AllUsers)[]
        {
            (Environment.SpecialFolder.DesktopDirectory, LinkLocation.Desktop, false),
            (Environment.SpecialFolder.CommonDesktopDirectory, LinkLocation.Desktop, true),
            (Environment.SpecialFolder.Programs, LinkLocation.StartMenu, false),
            (Environment.SpecialFolder.CommonPrograms, LinkLocation.StartMenu, true),
        };
        foreach (var (folder, location, allUsers) in places)
        {
            string root = Environment.GetFolderPath(folder);
            if (root.Length > 0 && path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                return LocationLabel(location, allUsers);
        }
        return path.StartsWith(TaskbarFolder + "\\", StringComparison.OrdinalIgnoreCase) ? LocationLabel(LinkLocation.Taskbar, false) : null;
    }

    private static readonly string[] SkippedExtensions = [".txt", ".pdf", ".chm", ".rtf", ".htm", ".html", ".md", ".log", ".ini", ".xml"];

    /// <summary>Scans everything. Runs COM calls, so call it on an STA thread (see <see cref="ScanAsync"/>).</summary>
    public static List<ProgramEntry> Scan()
    {
        var groups = new Dictionary<string, ProgramEntry>(StringComparer.OrdinalIgnoreCase);

        void AddFolder(string folder, LinkLocation location, bool allUsers, bool recursive)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder, "*.*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList(); }
            catch { return; }
            foreach (var file in files)
            {
                if (!ShellLink.IsLnk(file) && !ShellLink.IsUrl(file)) continue;
                var info = ShellLink.Read(file);
                if (info is null || IsUninstaller(file, info)) continue;
                if (info.Target is { } t && SkippedExtensions.Contains(System.IO.Path.GetExtension(t).ToLowerInvariant())) continue;

                string key = KeyOf(info) ?? "file:" + file;
                if (!groups.TryGetValue(key, out var entry))
                {
                    groups[key] = entry = new ProgramEntry
                    {
                        Key = key,
                        Name = System.IO.Path.GetFileNameWithoutExtension(file),
                        Kind = info.AppUserModelId is not null && info.Target is null ? ProgramKind.StoreApp : ProgramKind.Shortcuts,
                        ExePath = info.Target,
                        Aumid = info.AppUserModelId,
                    };
                }
                // The Start menu name is usually the nicest one ("Google Chrome" instead of "Chrome - Verknüpfung")
                if (location == LinkLocation.StartMenu && entry.Shortcuts.All(s => s.Location != LinkLocation.StartMenu))
                    entry.Name = System.IO.Path.GetFileNameWithoutExtension(file);
                entry.Shortcuts.Add(new ShortcutRef { Path = file, Location = location, AllUsers = allUsers, Info = info });
            }
        }

        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), LinkLocation.Desktop, false, false);
        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), LinkLocation.Desktop, true, false);
        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.Programs), LinkLocation.StartMenu, false, true);
        AddFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), LinkLocation.StartMenu, true, true);
        AddFolder(TaskbarFolder, LinkLocation.Taskbar, false, false);

        // Installed desktop programs without any shortcut
        var exeKeys = new HashSet<string>(groups.Values.Where(g => g.ExePath is not null).Select(g => g.ExePath!), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, exe) in InstalledPrograms())
        {
            // Same program under another path (e.g. a versioned EXE folder) usually has the same name
            if (groups.Values.Any(g => g.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)) || !exeKeys.Add(exe)) continue;
            string key = "exe:" + exe.ToLowerInvariant() + "|";
            groups.TryAdd(key, new ProgramEntry { Key = key, Name = name, Kind = ProgramKind.InstalledOnly, ExePath = exe });
        }

        // Windows/Store apps (Calculator, Terminal, Settings ...) without a shortcut
        var aumids = new HashSet<string>(groups.Values.Where(g => g.Aumid is not null).Select(g => g.Aumid!), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, parsing) in ShellNative.EnumerateAppsFolder())
        {
            if (parsing.Contains('!'))
            {
                if (!aumids.Add(parsing)) continue;
                string key = "app:" + parsing.ToLowerInvariant();
                groups.TryAdd(key, new ProgramEntry { Key = key, Name = name, Kind = ProgramKind.StoreApp, Aumid = parsing });
            }
            // Desktop programs and Windows tools of the Apps folder (Task Manager, Command Prompt ...) without a shortcut
            else if (ResolveAppsFolderPath(parsing) is { } exe && exeKeys.Add(exe) && !IsUninstallerName(exe))
            {
                string key = "exe:" + exe.ToLowerInvariant() + "|";
                groups.TryAdd(key, new ProgramEntry { Key = key, Name = name, Kind = ProgramKind.InstalledOnly, ExePath = exe });
            }
        }

        // Games (Steam, Epic) and shortcuts whose icon file is gone (Steam's blank icons)
        foreach (var g in groups.Values.Where(g => g.Shortcuts.Count > 0))
        {
            g.Game = g.Shortcuts.Select(s => GameLibrary.Detect(s.Info)).FirstOrDefault(x => x is not null);
            g.IconMissing = g.Shortcuts.All(s => GameLibrary.IconFileMissing(s.Info));
        }
        return groups.Values.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static Task<List<ProgramEntry>> ScanAsync() => Sta.Run(Scan);

    /// <summary>Grouping key: same Store app, or same target with the same arguments, or same URL.</summary>
    public static string? KeyOf(LinkInfo info)
    {
        if (info.Target is { } target)
            return "exe:" + target.ToLowerInvariant() + "|" + (info.Arguments ?? "").Trim().ToLowerInvariant();
        if (info.AppUserModelId is { } aumid) return "app:" + aumid.ToLowerInvariant();
        if (info.Url is { } url) return "url:" + url.Trim().ToLowerInvariant();
        return null;
    }

    /// <summary>Apps folder entries are "C:\...\x.exe" or "{KnownFolderGuid}\x.exe"; returns the existing EXE path.</summary>
    private static string? ResolveAppsFolderPath(string parsing)
    {
        string path = parsing;
        if (parsing.StartsWith('{') && parsing.IndexOf('}') is int end and > 0)
        {
            string? folder = parsing[1..end].ToUpperInvariant() switch
            {
                "1AC14E77-02E7-4E5D-B744-2EB1AE5198B7" => Environment.GetFolderPath(Environment.SpecialFolder.System),
                "D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27" => Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                "F38BF404-1D43-42F2-9305-67DE0B28FC23" => Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "6D809377-6AF0-444B-8957-A3773F02200E" => Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "905E63B6-C1BF-494E-B29C-65B732D3D21A" => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                _ => null,
            };
            if (folder is null) return null;
            path = System.IO.Path.Combine(folder, parsing[(end + 1)..].TrimStart('\\'));
        }
        return path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && System.IO.Path.IsPathRooted(path) && File.Exists(path) ? path : null;
    }

    private static bool IsUninstallerName(string exe)
    {
        string t = System.IO.Path.GetFileName(exe).ToLowerInvariant();
        return t.StartsWith("unins") || t.Contains("uninstall");
    }

    private static bool IsUninstaller(string file, LinkInfo info)
    {
        string n = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        string t = System.IO.Path.GetFileName(info.Target ?? "").ToLowerInvariant();
        return n.Contains("uninstall") || n.Contains("deinstall") || t.StartsWith("unins") || t.Contains("uninstall");
    }

    /// <summary>Desktop programs from the Windows program list (64 and 32 bit, all users and current user) with their EXE.</summary>
    private static IEnumerable<(string Name, string Exe)> InstalledPrograms()
    {
        var result = new List<(string, string)>();
        var roots = new (RegistryKey Hive, RegistryView View)[]
        {
            (Registry.LocalMachine, RegistryView.Registry64), (Registry.LocalMachine, RegistryView.Registry32), (Registry.CurrentUser, RegistryView.Default),
        };
        foreach (var (hive, view) in roots)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive == Registry.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view);
                using var uninstall = baseKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;
                foreach (var sub in uninstall.GetSubKeyNames())
                {
                    using var k = uninstall.OpenSubKey(sub);
                    if (k is null) continue;
                    if (k.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    if (k.GetValue("ParentKeyName") is not null) continue;
                    if (k.GetValue("DisplayName") is not string name || string.IsNullOrWhiteSpace(name)) continue;
                    string? exe = ExeFromIcon(k.GetValue("DisplayIcon") as string);
                    if (exe is null) continue;
                    string file = System.IO.Path.GetFileName(exe).ToLowerInvariant();
                    if (file.StartsWith("unins") || file.Contains("uninstall") || file.Contains("setup") || file.Contains("installer")) continue;
                    // Installer caches (redistributables, SDK setups) are not programs to start
                    if (exe.Contains(@"Package Cache\", StringComparison.OrdinalIgnoreCase) || exe.Contains(@"Installer\", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add((name.Trim(), exe));
                }
            }
            catch { /* key not readable */ }
        }
        return result;
    }

    private static string? ExeFromIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        string s = icon.Trim();
        int comma = s.LastIndexOf(',');
        if (comma > 0 && int.TryParse(s[(comma + 1)..].Trim(), out _)) s = s[..comma];
        s = Environment.ExpandEnvironmentVariables(s.Trim().Trim('"'));
        return s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(s) ? s : null;
    }
}

/// <summary>Runs work on a dedicated STA thread (the Windows shell COM objects expect STA).</summary>
public static class Sta
{
    public static Task<T> Run<T>(Func<T> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        }) { IsBackground = true, Name = "Shell STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    public static Task Run(Action work) => Run(() => { work(); return true; });
}
