using System.IO.Compression;
using System.Text.Json;
using IconWorkshop.Native;
using IconWorkshop.Packs;
using IconWorkshop.Programs;
using IconWorkshop.Targets;

namespace IconWorkshop.Assignments;

public enum AssignmentStatus
{
    /// <summary>Windows uses the assigned icon.</summary>
    Ok,
    /// <summary>Something else replaced the icon (e.g. a program update rewrote its shortcut).</summary>
    Changed,
    /// <summary>The shortcut, folder ... no longer exists.</summary>
    TargetMissing,
    /// <summary>The ICO file is gone and can't be re-created (pack removed).</summary>
    IcoMissing,
}

/// <summary>Result of "re-apply": how many were checked, repaired, and what couldn't be done.</summary>
public sealed record ReapplyResult(int Checked, int Repaired, List<(string Name, string Error)> Failed);

/// <summary>Status checks, re-applying after program updates, export and import.</summary>
public sealed partial class AssignmentStore
{
    // ---------- Status ----------

    public AssignmentStatus GetStatus(Assignment a)
    {
        if (!File.Exists(a.IcoPath)) return AssignmentStatus.IcoMissing;
        try
        {
            switch (a.Kind)
            {
                case AssignmentKind.Shortcut or AssignmentKind.CreatedShortcut:
                    if (!File.Exists(a.Target)) return AssignmentStatus.TargetMissing;
                    var info = ShellLink.Read(a.Target);
                    return info is not null && SamePath(info.IconPath, a.IcoPath) ? AssignmentStatus.Ok : AssignmentStatus.Changed;
                case AssignmentKind.Registry:
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(a.RegistryKey ?? ""))
                    {
                        string? v = k?.GetValue(a.RegistryValue ?? "", null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                        return v is not null && SamePath(StripIndex(v), a.IcoPath) ? AssignmentStatus.Ok : AssignmentStatus.Changed;
                    }
                case AssignmentKind.Folder:
                    if (a.FolderPath is null || !Directory.Exists(a.FolderPath)) return AssignmentStatus.TargetMissing;
                    string ini = FolderIconWriter.IniPath(a.FolderPath);
                    return File.Exists(ini) && File.ReadAllText(ini).Contains(a.IcoPath, StringComparison.OrdinalIgnoreCase)
                        ? AssignmentStatus.Ok : AssignmentStatus.Changed;
            }
        }
        catch { return AssignmentStatus.Changed; }
        return AssignmentStatus.Ok;
    }

    private static string StripIndex(string location)
    {
        int comma = location.LastIndexOf(',');
        return comma > 0 && int.TryParse(location[(comma + 1)..].Trim(), out _) ? location[..comma] : location;
    }

    private static bool SamePath(string? a, string b) =>
        a is not null && string.Equals(Environment.ExpandEnvironmentVariables(a.Trim().Trim('"')), b, StringComparison.OrdinalIgnoreCase);

    // ---------- Re-apply ----------

    /// <summary>
    /// Re-applies every assignment whose icon was replaced, e.g. because a program update re-created its shortcut.
    /// Missing ICO files are re-created first. Without <paramref name="allowElevation"/> (sign-in check),
    /// shortcuts of all users that need admin rights are only reported.
    /// </summary>
    public async Task<ReapplyResult> ReapplyAsync(PackStore packs, bool allowElevation)
    {
        RepairMissingIcos(packs);
        var failed = new List<(string, string)>();
        int repaired = 0;
        var needAdmin = new List<Assignment>();

        foreach (var a in _items.ToList())
        {
            var status = GetStatus(a);
            if (status == AssignmentStatus.Ok) continue;
            if (status != AssignmentStatus.Changed)
            {
                failed.Add((a.DisplayName, status == AssignmentStatus.TargetMissing
                    ? Loc.T("Nicht mehr vorhanden", "No longer exists")
                    : Loc.T("Icon-Datei fehlt (Pack entfernt?)", "Icon file missing (pack removed?)")));
                continue;
            }
            try
            {
                switch (a.Kind)
                {
                    case AssignmentKind.Shortcut or AssignmentKind.CreatedShortcut:
                        await Sta.Run(() => ShellLink.SetIcon(a.Target, a.IcoPath, 0));
                        break;
                    case AssignmentKind.Registry:
                        WriteRegistryIcon(a.RegistryKey!, a.RegistryValue ?? "", ShellLink.FormatLocation(a.IcoPath, 0));
                        if (a.NotifyPath is not null) ShellNative.NotifyItemChanged(a.NotifyPath);
                        break;
                    case AssignmentKind.Folder:
                        FolderIconWriter.SetIcon(a.FolderPath!, ShellLink.FormatLocation(a.IcoPath, 0));
                        break;
                }
                repaired++;
            }
            catch (UnauthorizedAccessException) when (allowElevation && a.Kind == AssignmentKind.Shortcut) { needAdmin.Add(a); }
            catch (Exception ex) { failed.Add((a.DisplayName, Message(ex))); }
        }

        if (needAdmin.Count > 0)
        {
            var elevated = await RunElevatedAsync(needAdmin.Select(a => new ElevatedSetIcon(a.Target, a.IcoPath, 0)).ToList());
            foreach (var a in needAdmin)
            {
                string? err = elevated is null ? Loc.T("Abgebrochen", "Cancelled") : elevated.GetValueOrDefault(a.Target);
                if (err is null) repaired++; else failed.Add((a.DisplayName, err));
            }
        }

        if (repaired > 0) ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
        return new ReapplyResult(_items.Count, repaired, failed);
    }

    // ---------- Many programs at once (suggestion wizard) ----------

    public sealed record BatchItem(IReadOnlyList<ShortcutRef> Shortcuts, string DisplayName, IconPack Pack, PackIcon Icon);

    /// <summary>
    /// Assigns icons to many programs in one go. Shortcuts that need admin rights are collected over all
    /// programs and changed in a single elevated step, so there is at most one UAC prompt.
    /// </summary>
    public async Task<BatchResult> ApplyManyAsync(IReadOnlyList<BatchItem> items, IProgress<int>? progress)
    {
        var result = new BatchResult();
        var needAdmin = new List<(ShortcutRef Shortcut, string Name, IconRef Ref)>();
        int done = 0;
        foreach (var item in items)
        {
            IconRef r;
            try { r = await RefOf(item.Pack, item.Icon); }
            catch (Exception ex) { result.Failed.Add((item.DisplayName, Message(ex))); continue; }
            foreach (var s in item.Shortcuts)
            {
                var existing = Find(s.Path);
                string? backup = existing?.BackupFile ?? Backup(s.Path);
                try
                {
                    await Sta.Run(() => ShellLink.SetIcon(s.Path, r.IcoPath, 0));
                    Record(existing, s, item.DisplayName, backup, r);
                    result.Succeeded.Add(s.Path);
                }
                catch (UnauthorizedAccessException) { needAdmin.Add((s, item.DisplayName, r)); }
                catch (Exception ex) { result.Failed.Add((s.Path, Message(ex))); }
            }
            progress?.Report(++done);
        }

        if (needAdmin.Count > 0)
        {
            var elevated = await RunElevatedAsync(needAdmin.Select(n => new ElevatedSetIcon(n.Shortcut.Path, n.Ref.IcoPath, 0)).ToList());
            if (elevated is null) result.Cancelled = true;
            foreach (var (s, name, r) in needAdmin)
            {
                string? err = elevated is null ? Loc.T("Abgebrochen (Administratorrechte nötig)", "Cancelled (admin rights needed)") : elevated.GetValueOrDefault(s.Path);
                if (err is not null) { result.Failed.Add((s.Path, err)); continue; }
                var existing = Find(s.Path);
                Record(existing, s, name, existing?.BackupFile ?? Backup(s.Path), r);
                result.Succeeded.Add(s.Path);
            }
        }

        Save();
        ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
        return result;
    }

    // ---------- Export / import ----------

    private const string ExportFile = "iconworkshop-export.json";

    private sealed class ExportData
    {
        public int Version { get; set; } = 1;
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        public string App { get; set; } = "Icon Workshop " + AppInfo.Version;
        public List<Assignment> Assignments { get; set; } = new();
    }

    /// <summary>
    /// Writes all assignments plus the ICO files they use into a ZIP (for a reinstall or another PC).
    /// Backups of the originals stay local: on another PC, its own originals are backed up on import.
    /// </summary>
    public async Task<int> ExportAsync(string zipPath)
    {
        await Task.Run(() =>
        {
            string tmp = zipPath + ".tmp";
            using (var zip = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                var data = new ExportData();
                var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in _items)
                {
                    string entry = $"icons/{Safe(a.PackId)}/{Safe(a.IconName)}.ico";
                    if (File.Exists(a.IcoPath) && written.Add(entry)) zip.CreateEntryFromFile(a.IcoPath, entry, CompressionLevel.Optimal);
                    var copy = JsonSerializer.Deserialize<Assignment>(JsonSerializer.Serialize(a))!;
                    copy.IcoPath = entry;
                    copy.BackupFile = null;
                    data.Assignments.Add(copy);
                }
                using var s = zip.CreateEntry(ExportFile).Open();
                JsonSerializer.Serialize(s, data, new JsonSerializerOptions { WriteIndented = true });
            }
            File.Move(tmp, zipPath, overwrite: true);
        });
        return _items.Count;
    }

    /// <summary>
    /// Applies the assignments of an export. ICO files go into the matching pack (if installed) or into
    /// Packs\_Imported. Targets that don't exist on this PC are reported; the originals here are backed up as usual.
    /// </summary>
    public async Task<BatchResult> ImportAsync(string zipPath, PackStore packs)
    {
        var result = new BatchResult();
        ExportData data;
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entry = zip.GetEntry(ExportFile) ?? throw new InvalidDataException(Loc.T("Das ist kein Export der Icon Werkstatt.", "This is not an Icon Workshop export."));
            using (var s = entry.Open()) data = JsonSerializer.Deserialize<ExportData>(s) ?? new ExportData();

            // Unpack the ICO files first
            foreach (var a in data.Assignments)
            {
                var icoEntry = zip.GetEntry(a.IcoPath);
                var pack = packs.Find(a.PackId);
                string dest = pack is not null
                    ? Path.Combine(pack.IcoFolder, Safe(a.IconName) + ".ico")
                    : Path.Combine(packs.Root, "_Imported", Safe(a.PackId), Safe(a.IconName) + ".ico");
                if (icoEntry is not null && !File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    icoEntry.ExtractToFile(dest);
                }
                a.IcoPath = File.Exists(dest) ? dest : "";
            }
        }

        foreach (var a in data.Assignments)
        {
            if (a.IcoPath.Length == 0) { result.Failed.Add((a.DisplayName, Loc.T("Icon-Datei fehlt im Export", "Icon file missing in the export"))); continue; }
            var r = new IconRef(a.PackId, a.PackName, a.IconName, a.IcoPath);
            BatchResult part;
            switch (a.Kind)
            {
                case AssignmentKind.Shortcut when File.Exists(a.Target) && ShellLink.Read(a.Target) is { } info:
                    part = await ApplyAsync([new ShortcutRef { Path = a.Target, Location = LinkLocation.Desktop, AllUsers = !a.Target.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase), Info = info }],
                                            a.DisplayName, r, allowElevation: true);
                    break;
                case AssignmentKind.CreatedShortcut when (a.CreatedExe is { } exe && File.Exists(exe)) || a.CreatedAumid is not null:
                    part = await RecreateShortcutAsync(a, r);
                    break;
                case AssignmentKind.Registry when a.RegistryKey is not null:
                    part = await ApplyTargetsAsync([new IconTarget { Id = a.Target, Name = a.DisplayName, Subtitle = "", Glyph = "", KeyPath = a.RegistryKey, ValueName = a.RegistryValue ?? "", NotifyPath = a.NotifyPath, SearchTerms = [] }], r);
                    break;
                case AssignmentKind.Folder when a.FolderPath is { } f && Directory.Exists(f):
                    part = await ApplyTargetsAsync([new IconTarget { Id = a.Target, Name = a.DisplayName, Subtitle = "", Glyph = "", FolderPath = f, SearchTerms = [] }], r);
                    break;
                default:
                    result.Failed.Add((a.DisplayName, Loc.T("Auf diesem PC nicht vorhanden", "Not present on this PC")));
                    continue;
            }
            result.Succeeded.AddRange(part.Succeeded);
            result.Failed.AddRange(part.Failed);
            result.Cancelled |= part.Cancelled;
        }
        return result;
    }

    private async Task<BatchResult> RecreateShortcutAsync(Assignment template, IconRef r)
    {
        var result = new BatchResult();
        try
        {
            if (File.Exists(template.Target)) await Sta.Run(() => ShellLink.SetIcon(template.Target, r.IcoPath, 0));
            else await Sta.Run(() => ShellLink.Create(template.Target, template.CreatedExe, null, null, template.CreatedAumid, r.IcoPath));
            var a = Find(template.Target) ?? new Assignment { Kind = AssignmentKind.CreatedShortcut, Target = template.Target };
            a.DisplayName = template.DisplayName;
            a.CreatedExe = template.CreatedExe;
            a.CreatedAumid = template.CreatedAumid;
            Stamp(a, r);
            if (!_items.Contains(a)) _items.Add(a);
            Save();
            Changed?.Invoke();
            result.Succeeded.Add(template.Target);
        }
        catch (Exception ex) { result.Failed.Add((template.DisplayName, Message(ex))); }
        return result;
    }

    private static string Safe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().Trim('.'); // "." / ".." from a tampered export must not leave the target folder
        return name.Length == 0 ? "icon" : name;
    }
}
