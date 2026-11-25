using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IconWorkshop.Imaging;
using IconWorkshop.Native;
using IconWorkshop.Packs;
using IconWorkshop.Programs;

namespace IconWorkshop.Assignments;

public enum AssignmentKind
{
    /// <summary>Existing shortcut whose icon was changed (restore = original icon back).</summary>
    Shortcut,
    /// <summary>Shortcut created by the app (restore = delete it).</summary>
    CreatedShortcut,
    /// <summary>Icon from a registry value of the current user: system icons, drives (restore = old value back).</summary>
    Registry,
    /// <summary>Folder icon via desktop.ini (restore = original desktop.ini and folder attributes back).</summary>
    Folder,
}

/// <summary>One change made by the app, with everything needed to undo it.</summary>
public sealed class Assignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public AssignmentKind Kind { get; set; }
    public string Target { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? OriginalIconPath { get; set; }
    public int OriginalIconIndex { get; set; }
    /// <summary>Copy of the untouched shortcut file (safety net).</summary>
    public string? BackupFile { get; set; }
    public string PackId { get; set; } = "";
    public string PackName { get; set; } = "";
    public string IconName { get; set; } = "";
    public string IcoPath { get; set; } = "";
    public DateTime AppliedUtc { get; set; }

    // ---------- Registry assignments (below HKEY_CURRENT_USER) ----------
    public string? RegistryKey { get; set; }
    public string? RegistryValue { get; set; }
    /// <summary>Whether the value existed before; if not, restoring deletes it again.</summary>
    public bool OriginalExisted { get; set; }
    public string? OriginalValue { get; set; }
    public string? OriginalValueKind { get; set; }
    public string? NotifyPath { get; set; }

    // ---------- Created shortcuts: what they start (to re-create them, e.g. after an import) ----------
    public string? CreatedExe { get; set; }
    public string? CreatedAumid { get; set; }

    // ---------- Folder assignments ----------
    public string? FolderPath { get; set; }
    public int? FolderAttributes { get; set; }
    public int? IniAttributes { get; set; }
}

/// <summary>Result of applying or restoring several shortcuts: what worked and what didn't.</summary>
public sealed class BatchResult
{
    public List<string> Succeeded { get; } = new();
    public List<(string Path, string Error)> Failed { get; } = new();
    public bool Cancelled { get; set; }
}

/// <summary>
/// Keeps track of all changes (assignments.json) and their backups, and applies/restores them.
/// The original is backed up before the first change of a target; later changes keep that original.
/// </summary>
public sealed partial class AssignmentStore
{
    private readonly string _file;
    private readonly string _backupFolder;
    private readonly List<Assignment> _items;

    public event Action? Changed;

    public AssignmentStore(string dataFolder)
    {
        _file = Path.Combine(dataFolder, "assignments.json");
        _backupFolder = Path.Combine(dataFolder, "Backups");
        _items = Load(_file);
    }

    public IReadOnlyList<Assignment> Items => _items;

    public Assignment? Find(string target) =>
        _items.FirstOrDefault(a => string.Equals(a.Target, target, StringComparison.OrdinalIgnoreCase));

    public int UsageCount(IconPack pack) => _items.Count(a => a.PackId.Equals(pack.Id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<string> IcoFilesInUse(IconPack pack) =>
        _items.Where(a => a.PackId.Equals(pack.Id, StringComparison.OrdinalIgnoreCase)).Select(a => a.IcoPath)
              .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // ---------- ICO files ----------

    /// <summary>Creates the Windows icon file of a pack icon on first use (icons\x.png -> ico\x.ico).</summary>
    public static string EnsureIco(IconPack pack, PackIcon icon)
    {
        string ico = pack.IcoPath(icon);
        if (!File.Exists(ico))
        {
            Directory.CreateDirectory(pack.IcoFolder);
            IcoWriter.FromImage(File.ReadAllBytes(pack.PngPath(icon)), ico);
        }
        return ico;
    }

    /// <summary>
    /// Re-creates ICO files of assignments that went missing (e.g. deleted by hand or by a cleanup tool),
    /// as long as the pack icon still exists. Returns the number of repaired files.
    /// </summary>
    public int RepairMissingIcos(PackStore packs)
    {
        int repaired = 0;
        foreach (var a in _items.Where(a => !File.Exists(a.IcoPath)).ToList())
        {
            var pack = packs.Find(a.PackId);
            var icon = pack?.Icons.FirstOrDefault(i => i.Name == a.IconName);
            if (pack is null || icon is null) continue;
            try
            {
                string ico = EnsureIco(pack, icon);
                if (!string.Equals(ico, a.IcoPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (a.Kind is AssignmentKind.Shortcut or AssignmentKind.CreatedShortcut) ShellNative.NotifyItemChanged(a.Target);
                repaired++;
            }
            catch { /* stays broken; shown on the assignments page later */ }
        }
        if (repaired > 0) ShellNative.NotifyIconsChanged();
        return repaired;
    }

    /// <summary>Creates the ICO if needed and makes sure Windows can read it (re-creates a damaged file once).</summary>
    public static string EnsureReadableIco(IconPack pack, PackIcon icon)
    {
        string ico = EnsureIco(pack, icon);
        if (IcoIsReadable(ico)) return ico;
        try { File.Delete(ico); } catch { }
        ico = EnsureIco(pack, icon);
        if (!IcoIsReadable(ico))
            throw new IOException(Loc.T("Die Icon-Datei konnte nicht erstellt werden oder ist für Windows nicht lesbar.", "The icon file could not be created or cannot be read by Windows."));
        return ico;
    }

    /// <summary>Checks that Windows can actually read the icon file (exists and contains an icon).</summary>
    public static bool IcoIsReadable(string ico) =>
        File.Exists(ico) && ShellNative.IconFromLocation(ico + ",0", 32) is not null;

    // ---------- Shortcuts ----------

    /// <summary>
    /// Sets the icon on existing shortcuts. Shortcuts of all users are collected and changed in one
    /// elevated step (one UAC prompt) if <paramref name="allowElevation"/> is set.
    /// </summary>
    public async Task<BatchResult> ApplyAsync(IReadOnlyList<ShortcutRef> shortcuts, string displayName, IconPack pack, PackIcon icon, bool allowElevation) =>
        await ApplyAsync(shortcuts, displayName, await RefOf(pack, icon), allowElevation);

    /// <summary>Which icon is assigned: pack, icon name and the ICO file Windows uses.</summary>
    public sealed record IconRef(string PackId, string PackName, string IconName, string IcoPath);

    private static void Stamp(Assignment a, IconRef r)
    {
        a.PackId = r.PackId;
        a.PackName = r.PackName;
        a.IconName = r.IconName;
        a.IcoPath = r.IcoPath;
        a.AppliedUtc = DateTime.UtcNow;
    }

    private static async Task<IconRef> RefOf(IconPack pack, PackIcon icon) =>
        new(pack.Id, pack.Name, icon.Name, await Sta.Run(() => EnsureReadableIco(pack, icon)));

    public async Task<BatchResult> ApplyAsync(IReadOnlyList<ShortcutRef> shortcuts, string displayName, IconRef r, bool allowElevation)
    {
        var result = new BatchResult();
        string ico = r.IcoPath;
        var needAdmin = new List<ShortcutRef>();

        foreach (var s in shortcuts)
        {
            var existing = Find(s.Path);
            string? backup = existing?.BackupFile ?? Backup(s.Path);
            try
            {
                await Sta.Run(() => ShellLink.SetIcon(s.Path, ico, 0));
                Record(existing, s, displayName, backup, r);
                result.Succeeded.Add(s.Path);
            }
            catch (UnauthorizedAccessException) when (allowElevation) { needAdmin.Add(s); }
            catch (Exception ex) { result.Failed.Add((s.Path, Message(ex))); }
        }

        if (needAdmin.Count > 0)
        {
            var elevated = await RunElevatedAsync(needAdmin.Select(s => new ElevatedSetIcon(s.Path, ico, 0)).ToList());
            if (elevated is null) { result.Cancelled = true; foreach (var s in needAdmin) result.Failed.Add((s.Path, Loc.T("Abgebrochen", "Cancelled"))); }
            else
                foreach (var s in needAdmin)
                {
                    if (elevated.TryGetValue(s.Path, out var err) && err is not null) { result.Failed.Add((s.Path, err)); continue; }
                    var existing = Find(s.Path);
                    Record(existing, s, displayName, existing?.BackupFile ?? Backup(s.Path), r);
                    result.Succeeded.Add(s.Path);
                }
        }

        Save();
        ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
        return result;
    }

    private void Record(Assignment? existing, ShortcutRef s, string displayName, string? backup, IconRef r)
    {
        var a = existing ?? new Assignment
        {
            Kind = AssignmentKind.Shortcut,
            Target = s.Path,
            OriginalIconPath = s.Info.IconPath,
            OriginalIconIndex = s.Info.IconIndex,
        };
        a.DisplayName = displayName;
        a.BackupFile = backup;
        Stamp(a, r);
        if (existing is null) _items.Add(a);
    }

    /// <summary>Creates own shortcuts (desktop and/or Start menu) for a program or Store app without one.</summary>
    public async Task<BatchResult> CreateShortcutsAsync(ProgramEntry program, bool desktop, bool startMenu, IconPack pack, PackIcon icon)
    {
        var r = await RefOf(pack, icon);
        var result = new BatchResult();
        string ico = r.IcoPath;
        var targets = new List<string>();
        string fileName = SafeFileName(program.Name) + ".lnk";
        if (desktop) targets.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), fileName));
        if (startMenu) targets.Add(Path.Combine(ProgramScanner.OwnStartMenuFolder, fileName));

        foreach (var original in targets)
        {
            string path = original;
            // Never overwrite an existing file: "Name (2).lnk" ...
            for (int i = 2; File.Exists(path) && Find(path) is null; i++)
                path = Path.Combine(Path.GetDirectoryName(original)!, $"{Path.GetFileNameWithoutExtension(original)} ({i}).lnk");
            try
            {
                await Sta.Run(() => ShellLink.Create(path, program.ExePath, null, null, program.Aumid, ico));
                var a = Find(path) ?? new Assignment { Kind = AssignmentKind.CreatedShortcut, Target = path };
                a.DisplayName = program.Name;
                a.CreatedExe = program.ExePath;
                a.CreatedAumid = program.Aumid;
                Stamp(a, r);
                if (!_items.Contains(a)) _items.Add(a);
                result.Succeeded.Add(path);
            }
            catch (Exception ex) { result.Failed.Add((path, Message(ex))); }
        }
        Save();
        ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
        return result;
    }

    // ---------- Registry icons (system icons, drives) ----------

    /// <summary>
    /// Sets the icon of system icons, drives and file types (per-user registry value) or folders (desktop.ini).
    /// The previous state is remembered on the first change.
    /// </summary>
    public async Task<BatchResult> ApplyTargetsAsync(IReadOnlyList<Targets.IconTarget> targets, IconPack pack, PackIcon icon) =>
        await ApplyTargetsAsync(targets, await RefOf(pack, icon));

    public async Task<BatchResult> ApplyTargetsAsync(IReadOnlyList<Targets.IconTarget> targets, IconRef r)
    {
        var result = new BatchResult();
        string ico = r.IcoPath;
        await Task.CompletedTask;
        foreach (var t in targets)
        {
            try
            {
                if (t.Unsupported is not null) throw new InvalidOperationException(t.Unsupported);
                var existing = Find(t.Id);
                Assignment a;
                if (t.FolderPath is { } folder)
                {
                    a = existing ?? new Assignment { Kind = AssignmentKind.Folder, Target = t.Id, FolderPath = folder };
                    if (existing is null)
                    {
                        string ini = Targets.FolderIconWriter.IniPath(folder);
                        a.OriginalExisted = File.Exists(ini);
                        a.BackupFile = a.OriginalExisted ? Backup(ini, t.Id) : null;
                        if (a.OriginalExisted && a.BackupFile is null) throw new IOException(Loc.T("desktop.ini konnte nicht gesichert werden.", "desktop.ini could not be backed up."));
                        a.IniAttributes = a.OriginalExisted ? (int)File.GetAttributes(ini) : null;
                        a.FolderAttributes = (int)File.GetAttributes(folder);
                    }
                    Targets.FolderIconWriter.SetIcon(folder, ShellLink.FormatLocation(ico, 0));
                }
                else
                {
                    a = existing ?? new Assignment { Kind = AssignmentKind.Registry, Target = t.Id, RegistryKey = t.KeyPath, RegistryValue = t.ValueName };
                    if (existing is null)
                    {
                        using var read = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(t.KeyPath);
                        object? old = read?.GetValue(t.ValueName, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
                        a.OriginalExisted = old is not null;
                        a.OriginalValue = old?.ToString();
                        a.OriginalValueKind = old is null ? null : read!.GetValueKind(t.ValueName).ToString();
                    }
                    WriteRegistryIcon(a.RegistryKey ?? t.KeyPath, a.RegistryValue ?? t.ValueName, ShellLink.FormatLocation(ico, 0));
                }
                a.DisplayName = t.Name;
                a.NotifyPath = t.NotifyPath;
                Stamp(a, r);
                if (existing is null) _items.Add(a);
                if (t.NotifyPath is not null) ShellNative.NotifyItemChanged(t.NotifyPath);
                result.Succeeded.Add(t.Id);
            }
            catch (Exception ex) { result.Failed.Add((t.Name, Message(ex))); }
        }
        Save();
        ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
        return result;
    }

    private static void WriteRegistryIcon(string keyPath, string valueName, string value)
    {
        using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
        k.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.String);
    }

    /// <summary>Restores the old registry value, or removes the value (and keys left empty) if there was none.</summary>
    private static void RestoreRegistry(Assignment a)
    {
        if (a.RegistryKey is null) return;
        if (a.OriginalExisted)
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(a.RegistryKey, writable: true);
            var kind = Enum.TryParse<Microsoft.Win32.RegistryValueKind>(a.OriginalValueKind, out var parsed) ? parsed : Microsoft.Win32.RegistryValueKind.String;
            k.SetValue(a.RegistryValue ?? "", a.OriginalValue ?? "", kind is Microsoft.Win32.RegistryValueKind.ExpandString ? kind : Microsoft.Win32.RegistryValueKind.String);
        }
        else
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(a.RegistryKey, writable: true))
                k?.DeleteValue(a.RegistryValue ?? "", throwOnMissingValue: false);
            // Remove keys the app created and that are empty now (DefaultIcon, then its parent), nothing above
            string key = a.RegistryKey;
            for (int level = 0; level < 2; level++)
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key))
                    if (k is null || k.SubKeyCount > 0 || k.ValueCount > 0) break;
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(key, throwOnMissingSubKey: false);
                int slash = key.LastIndexOf('\\');
                if (slash < 0) break;
                key = key[..slash];
            }
        }
        if (a.NotifyPath is not null) ShellNative.NotifyItemChanged(a.NotifyPath);
    }

    /// <summary>Undoes assignments: original icon back, or own shortcut removed.</summary>
    public async Task<BatchResult> RestoreAsync(IReadOnlyList<Assignment> assignments, bool allowElevation)
    {
        var result = new BatchResult();
        var needAdmin = new List<Assignment>();
        foreach (var a in assignments)
        {
            try
            {
                if (a.Kind == AssignmentKind.Registry)
                {
                    RestoreRegistry(a);
                }
                else if (a.Kind == AssignmentKind.Folder && a.FolderPath is { } folder)
                {
                    if (Directory.Exists(folder))
                    {
                        byte[]? ini = a.OriginalExisted && a.BackupFile is not null ? File.ReadAllBytes(a.BackupFile) : null;
                        Targets.FolderIconWriter.Restore(folder, ini, (FileAttributes?)a.IniAttributes, (FileAttributes)(a.FolderAttributes ?? 0));
                    }
                }
                else if (a.Kind == AssignmentKind.CreatedShortcut)
                {
                    if (File.Exists(a.Target)) File.Delete(a.Target);
                    ShellNative.NotifyItemChanged(a.Target);
                }
                else if (File.Exists(a.Target))
                {
                    await Sta.Run(() => ShellLink.SetIcon(a.Target, a.OriginalIconPath ?? "", a.OriginalIconIndex));
                }
                _items.Remove(a);
                result.Succeeded.Add(a.Target);
            }
            catch (UnauthorizedAccessException) when (allowElevation && a.Kind == AssignmentKind.Shortcut) { needAdmin.Add(a); }
            catch (Exception ex) { result.Failed.Add((a.Target, Message(ex))); }
        }

        if (needAdmin.Count > 0)
        {
            var elevated = await RunElevatedAsync(needAdmin.Select(a => new ElevatedSetIcon(a.Target, a.OriginalIconPath ?? "", a.OriginalIconIndex)).ToList());
            if (elevated is null) { result.Cancelled = true; foreach (var a in needAdmin) result.Failed.Add((a.Target, Loc.T("Abgebrochen", "Cancelled"))); }
            else
                foreach (var a in needAdmin)
                {
                    if (elevated.TryGetValue(a.Target, out var err) && err is not null) { result.Failed.Add((a.Target, err)); continue; }
                    _items.Remove(a);
                    result.Succeeded.Add(a.Target);
                }
        }

        Save();
        ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
        return result;
    }

    /// <summary>After the packs folder moved: point all assignments to the new ICO paths and re-apply them.</summary>
    public async Task RepointAsync(IReadOnlyDictionary<string, string> folderMap, PackStore packs)
    {
        bool any = false;
        foreach (var a in _items)
        {
            foreach (var (oldFolder, newFolder) in folderMap)
            {
                string prefix = oldFolder.TrimEnd('\\') + "\\";
                if (!a.IcoPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                a.IcoPath = Path.Combine(newFolder, a.IcoPath[prefix.Length..]);
                any = true;
                try
                {
                    if (a.Kind == AssignmentKind.Folder && a.FolderPath is { } f && Directory.Exists(f))
                        Targets.FolderIconWriter.SetIcon(f, ShellLink.FormatLocation(a.IcoPath, 0));
                    else if (a.Kind == AssignmentKind.Registry && a.RegistryKey is not null)
                        WriteRegistryIcon(a.RegistryKey, a.RegistryValue ?? "", ShellLink.FormatLocation(a.IcoPath, 0));
                    else if (File.Exists(a.Target)) await Sta.Run(() => ShellLink.SetIcon(a.Target, a.IcoPath, 0));
                }
                catch { /* e.g. shortcut of all users without admin rights: shown as broken later */ }
                break;
            }
        }
        if (!any) return;
        Save();
        ShellNative.NotifyIconsChanged();
        Changed?.Invoke();
    }

    // ---------- Elevation ----------

    public sealed record ElevatedSetIcon(string Path, string IconPath, int Index);

    /// <summary>
    /// Runs this app elevated (one UAC prompt) to change shortcuts that need admin rights.
    /// Returns path -> error (null = success), or null if the user declined the prompt.
    /// </summary>
    private static async Task<Dictionary<string, string?>?> RunElevatedAsync(List<ElevatedSetIcon> requests)
    {
        string request = Path.Combine(Path.GetTempPath(), "IconWorkshop-" + Guid.NewGuid().ToString("N") + ".json");
        string response = request + ".result";
        await File.WriteAllTextAsync(request, JsonSerializer.Serialize(requests));
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--elevated \"{request}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            if (p is null) return null;
            await p.WaitForExitAsync();
            if (!File.Exists(response)) return null;
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(await File.ReadAllTextAsync(response));
        }
        catch (Win32Exception) { return null; } // UAC prompt declined
        finally
        {
            try { File.Delete(request); File.Delete(response); } catch { }
        }
    }

    /// <summary>Entry point of the elevated helper process (--elevated &lt;file&gt;).</summary>
    public static int RunElevatedRequest(string requestFile)
    {
        var results = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var requests = JsonSerializer.Deserialize<List<ElevatedSetIcon>>(File.ReadAllText(requestFile)) ?? new();
            foreach (var r in requests)
            {
                // Only shortcut files may be changed this way
                if (!ShellLink.IsLnk(r.Path) && !ShellLink.IsUrl(r.Path)) { results[r.Path] = "not a shortcut"; continue; }
                try { ShellLink.SetIcon(r.Path, r.IconPath, r.Index); results[r.Path] = null; }
                catch (Exception ex) { results[r.Path] = Message(ex); }
            }
            ShellNative.NotifyIconsChanged();
            File.WriteAllText(requestFile + ".result", JsonSerializer.Serialize(results));
            return 0;
        }
        catch { return 1; }
    }

    // ---------- Storage ----------

    private string? Backup(string path, string? key = null)
    {
        try
        {
            Directory.CreateDirectory(_backupFolder);
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((key ?? path).ToLowerInvariant())))[..32];
            string dest = Path.Combine(_backupFolder, hash + Path.GetExtension(path));
            if (!File.Exists(dest)) File.Copy(path, dest);
            return dest;
        }
        catch { return null; }
    }

    private static List<Assignment> Load(string file)
    {
        try
        {
            if (File.Exists(file)) return JsonSerializer.Deserialize<List<Assignment>>(File.ReadAllText(file)) ?? new();
        }
        catch
        {
            // Damaged file: keep a copy instead of overwriting it with an empty list
            try { File.Copy(file, file + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); } catch { }
        }
        return new();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        string tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, _file, overwrite: true);
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim().TrimEnd('.');
    }

    private static string Message(Exception ex) => ex switch
    {
        UnauthorizedAccessException => Loc.T("Keine Schreibrechte (Administratorrechte nötig)", "No write access (admin rights needed)"),
        FileNotFoundException or DirectoryNotFoundException => Loc.T("Datei nicht gefunden", "File not found"),
        _ => ex.Message,
    };
}
