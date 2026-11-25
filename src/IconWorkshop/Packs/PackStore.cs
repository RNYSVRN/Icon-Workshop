using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

namespace IconWorkshop.Packs;

/// <summary>
/// All imported icon packs below the packs folder (default %LOCALAPPDATA%\Icon-Workshop\Packs).
/// Each pack has its own subfolder; imports are written to a temporary folder first and only
/// renamed into place when they are complete.
/// </summary>
public sealed partial class PackStore
{
    public string Root { get; private set; }

    public ObservableCollection<IconPack> Packs { get; } = new();

    /// <summary>Raised after import, rename, delete or a reload.</summary>
    public event Action? Changed;

    /// <summary>
    /// Returns how many assignments still use icons of a pack. Set by the assignment service
    /// (later stages); without it, packs are considered unused.
    /// </summary>
    public Func<IconPack, int> UsageCount { get; set; } = _ => 0;

    public PackStore(string root)
    {
        Root = root;
    }

    public void Load()
    {
        CloseThumbs();
        Packs.Clear();
        try
        {
            Directory.CreateDirectory(Root);
            foreach (var dir in Directory.EnumerateDirectories(Root))
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith('.')) { TryDeleteDirectory(dir); continue; } // leftovers of an interrupted import
                if (!File.Exists(Path.Combine(dir, IconPack.FileName))) continue;
                try { Packs.Add(IconPack.Load(dir)); } catch { /* damaged pack: skipped */ }
            }
        }
        catch { /* folder not reachable (e.g. removed drive) */ }
        Sort();
        foreach (var pack in Packs) OpenOrBuildThumbs(pack);
        Changed?.Invoke();
    }

    /// <summary>Opens the preview cache of a pack, or builds it once in the background (packs from older versions).</summary>
    private void OpenOrBuildThumbs(IconPack pack)
    {
        pack.Thumbs = ThumbStore.TryOpen(pack.Folder);
        if (pack.Thumbs is not null && pack.Thumbs.Count >= pack.Icons.Count * 0.95) return;
        pack.Thumbs?.Dispose();
        pack.Thumbs = null;
        Task.Run(() =>
        {
            try
            {
                ThumbStore.Build(pack);
                var store = ThumbStore.TryOpen(pack.Folder);
                if (Packs.Contains(pack) && Directory.Exists(pack.Folder)) pack.Thumbs = store;
                else store?.Dispose();
            }
            catch { /* stays without cache; previews are then read from the single files */ }
        });
    }

    private void CloseThumbs()
    {
        foreach (var pack in Packs)
        {
            pack.Thumbs?.Dispose();
            pack.Thumbs = null;
        }
    }

    public IconPack? Find(string? id) => id is null ? null : Packs.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public async Task<ImportResult> ImportAsync(string source, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        source = Path.GetFullPath(source); // the source path is kept (e.g. to read the pack style again later)
        Directory.CreateDirectory(Root);
        string temp = Path.Combine(Root, "." + Guid.NewGuid().ToString("N"));
        try
        {
            var result = await Task.Run(() => PackImporter.Import(source, temp, progress, ct), ct);
            string id = UniqueId(result.Pack.Name);
            string final = Path.Combine(Root, id);
            Directory.Move(temp, final);
            result.Pack.Folder = final;
            result.Pack.Id = id;
            result.Pack.Save();
            result.Pack.Thumbs = ThumbStore.TryOpen(final);
            Packs.Add(result.Pack);
            Sort();
            Changed?.Invoke();
            return result;
        }
        catch
        {
            TryDeleteDirectory(temp);
            throw;
        }
    }

    /// <summary>Folder/ID of the pack that holds icons made with the icon maker.</summary>
    public const string CustomPackId = "Eigene-Icons";

    /// <summary>
    /// Saves an icon made with the icon maker into the "My icons" pack (created on first use).
    /// The category is the pack whose style was used. Returns the pack and the new icon.
    /// </summary>
    public (IconPack Pack, PackIcon Icon) SaveCustomIcon(string name, byte[] png, string category, string? designJson = null)
    {
        var pack = Find(CustomPackId);
        bool created = pack is null;
        if (pack is null)
        {
            string folder = Path.Combine(Root, CustomPackId);
            Directory.CreateDirectory(Path.Combine(folder, "icons"));
            pack = new IconPack
            {
                Id = CustomPackId,
                Name = Loc.T("Eigene Icons", "My icons"),
                SourceKind = PackSourceKind.Folder,
                SourceFile = folder,
                ImportedUtc = DateTime.UtcNow,
                Folder = folder,
            };
        }

        string baseName = SafeIconName(name);
        string unique = baseName;
        for (int i = 2; pack.Icons.Any(x => x.Name.Equals(unique, StringComparison.OrdinalIgnoreCase)); i++) unique = $"{baseName}_{i}";
        Directory.CreateDirectory(pack.IconsFolder);
        File.WriteAllBytes(pack.PngPath(new PackIcon { Name = unique }), png);

        int cat = pack.Categories.FindIndex(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (cat < 0) { pack.Categories.Add(category); cat = pack.Categories.Count - 1; }
        var icon = new PackIcon { Name = unique, Categories = [cat] };
        pack.Icons.Add(icon);
        pack.Save();
        if (designJson is not null) WriteDesign(pack, icon, designJson);
        RebuildThumbs(pack);

        if (created) { Packs.Add(pack); Sort(); }
        Changed?.Invoke();
        return (pack, icon);
    }

    /// <summary>Overwrites an own icon after editing it again. Its ICO file is re-created, so assignments show the new look.</summary>
    public void UpdateCustomIcon(IconPack pack, PackIcon icon, byte[] png, string? designJson)
    {
        File.WriteAllBytes(pack.PngPath(icon), png);
        if (designJson is not null) WriteDesign(pack, icon, designJson);
        string ico = pack.IcoPath(icon);
        if (File.Exists(ico))
        {
            try { Imaging.IcoWriter.FromImage(png, ico); } catch { /* re-created on the next assignment */ }
        }
        RebuildThumbs(pack);
        Changed?.Invoke();
    }

    /// <summary>The saved design of an own icon (for editing it again), or null.</summary>
    public static string? DesignPath(IconPack pack, PackIcon icon)
    {
        string file = Path.Combine(pack.Folder, "designs", icon.Name + ".json");
        return File.Exists(file) ? file : null;
    }

    private static void WriteDesign(IconPack pack, PackIcon icon, string json)
    {
        string folder = Path.Combine(pack.Folder, "designs");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, icon.Name + ".json"), json);
    }

    private static void RebuildThumbs(IconPack pack)
    {
        // Small pack: rebuild the preview cache right away
        pack.Thumbs?.Dispose();
        pack.Thumbs = null;
        try { ThumbStore.Build(pack); pack.Thumbs = ThumbStore.TryOpen(pack.Folder); } catch { }
    }

    [GeneratedRegex(@"[^\w\-]+")]
    private static partial Regex NonIconChars();

    private static string SafeIconName(string name)
    {
        string s = NonIconChars().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');
        return s.Length == 0 ? "icon" : s.Length > 60 ? s[..60] : s;
    }

    public void Rename(IconPack pack, string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name == pack.Name) return;
        pack.Name = name;
        pack.Save();
        Sort();
        Changed?.Invoke();
    }

    /// <summary>
    /// Deletes a pack. ICO files that are still assigned somewhere are kept (the folder then only
    /// contains those, so existing icons keep working); everything else is removed.
    /// </summary>
    public void Delete(IconPack pack, IReadOnlyCollection<string>? icoFilesInUse = null)
    {
        Packs.Remove(pack);
        pack.Thumbs?.Dispose();
        pack.Thumbs = null;
        if (icoFilesInUse is { Count: > 0 })
        {
            foreach (var file in Directory.EnumerateFiles(pack.Folder, "*", SearchOption.AllDirectories).ToList())
                if (!icoFilesInUse.Contains(file, StringComparer.OrdinalIgnoreCase)) TryDeleteFile(file);
            foreach (var dir in Directory.EnumerateDirectories(pack.Folder, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).ToList())
                if (!Directory.EnumerateFileSystemEntries(dir).Any()) TryDeleteDirectory(dir);
            // Marker so the folder isn't mistaken for a pack and the user knows why it is still there
            try
            {
                File.WriteAllText(Path.Combine(pack.Folder, "README.txt"), Loc.T(
                    "Dieses Icon-Pack wurde entfernt. Die verbliebenen ICO-Dateien sind noch zugewiesen und werden von Windows verwendet.",
                    "This icon pack was removed. The remaining ICO files are still assigned and used by Windows."));
            }
            catch { }
        }
        else
        {
            TryDeleteDirectory(pack.Folder);
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Moves all packs to a new folder. Copies first and deletes the old files only after
    /// everything has arrived. Returns the mapping old folder -> new folder for re-pointing assignments.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> MoveRootAsync(string newRoot, IProgress<ImportProgress>? progress)
    {
        newRoot = Path.GetFullPath(newRoot);
        string oldRoot = Path.GetFullPath(Root);
        if (string.Equals(newRoot.TrimEnd('\\'), oldRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            return new Dictionary<string, string>();
        if (newRoot.StartsWith(oldRoot.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            throw new IOException(Loc.T("Der neue Ordner darf nicht im bisherigen Pack-Ordner liegen.",
                                        "The new folder must not be inside the current packs folder."));

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CloseThumbs(); // open preview caches would block deleting the old folders
        await Task.Run(() =>
        {
            Directory.CreateDirectory(newRoot);
            var dirs = Directory.Exists(oldRoot) ? Directory.GetDirectories(oldRoot).Where(d => !Path.GetFileName(d).StartsWith('.')).ToList() : new();
            var files = dirs.SelectMany(d => Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)).ToList();
            int done = 0;
            string phase = Loc.T("Icon-Packs werden verschoben …", "Moving icon packs …");
            foreach (var dir in dirs)
            {
                string target = Path.Combine(newRoot, Path.GetFileName(dir));
                if (Directory.Exists(target))
                    throw new IOException(Loc.T($"Im Zielordner gibt es bereits „{Path.GetFileName(dir)}“.",
                                                $"“{Path.GetFileName(dir)}” already exists in the target folder."));
                map[dir] = target;
            }
            foreach (var file in files)
            {
                string rel = Path.GetRelativePath(oldRoot, file);
                string dest = Path.Combine(newRoot, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: false);
                if (++done % 100 == 0) progress?.Report(new(done, files.Count, phase));
            }
            foreach (var dir in dirs) TryDeleteDirectory(dir);
        });

        Root = newRoot;
        Load();
        return map;
    }

    private void Sort()
    {
        var sorted = Packs.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            int old = Packs.IndexOf(sorted[i]);
            if (old != i) Packs.Move(old, i);
        }
    }

    [GeneratedRegex(@"[^A-Za-z0-9\-]+")]
    private static partial Regex NonSlug();

    private string UniqueId(string name)
    {
        string slug = NonSlug().Replace(name, "-").Trim('-');
        if (slug.Length == 0) slug = "pack";
        if (slug.Length > 40) slug = slug[..40].TrimEnd('-');
        string id = slug;
        for (int i = 2; Directory.Exists(Path.Combine(Root, id)); i++) id = $"{slug}-{i}";
        return id;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    private static void TryDeleteFile(string file)
    {
        try { File.Delete(file); } catch { }
    }
}
