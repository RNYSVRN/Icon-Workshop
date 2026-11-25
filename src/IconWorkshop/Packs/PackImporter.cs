using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using IconWorkshop.Imaging;

namespace IconWorkshop.Packs;

public sealed record ImportProgress(int Done, int Total, string Phase);

/// <summary>Result of an import: the new pack plus what could not be taken over.</summary>
public sealed record ImportResult(IconPack Pack, int Missing, int Unsupported, int Failed);

/// <summary>
/// Turns an Android icon pack (APK) or a folder/ZIP of images into an <see cref="IconPack"/>.
/// Only PNG previews are stored; ICO files are created later when an icon is actually assigned.
/// </summary>
public static partial class PackImporter
{
    private static readonly string[] ImageExtensions = [".png", ".webp", ".jpg", ".jpeg", ".bmp"];
    private static readonly string[] FolderExtensions = [".png", ".webp", ".jpg", ".jpeg", ".bmp", ".ico"];

    public static bool IsApk(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".apk" or ".apks" or ".xapk") return true;
        if (ext != ".zip") return false;
        try
        {
            using var zip = ZipFile.OpenRead(path);
            return zip.GetEntry("AndroidManifest.xml") is not null || zip.GetEntry("resources.arsc") is not null;
        }
        catch { return false; }
    }

    /// <summary>Imports into <paramref name="targetFolder"/> (must not exist yet).</summary>
    public static ImportResult Import(string source, string targetFolder, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        if (Directory.Exists(source)) return ImportFolder(source, targetFolder, progress, ct);
        if (IsApk(source)) return ImportApk(source, targetFolder, progress, ct);
        if (Path.GetExtension(source).Equals(".zip", StringComparison.OrdinalIgnoreCase)) return ImportZip(source, targetFolder, progress, ct);
        throw new InvalidDataException(Loc.T("Dieser Dateityp wird nicht unterstützt.", "This file type is not supported."));
    }

    // ===================================================================== APK

    private static ImportResult ImportApk(string apkPath, string target, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new(0, 0, Loc.T("APK wird gelesen …", "Reading APK …")));
        using var zip = ZipFile.OpenRead(apkPath);

        // Split APKs (.apks/.xapk) contain the actual APKs: use the largest inner base APK.
        var inner = zip.Entries.Where(e => e.FullName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)).OrderByDescending(e => e.Length).FirstOrDefault();
        if (zip.GetEntry("resources.arsc") is null && inner is not null)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "IconWorkshop-" + Guid.NewGuid().ToString("N") + ".apk");
            try
            {
                inner.ExtractToFile(tmp);
                var r = ImportApk(tmp, target, progress, ct);
                r.Pack.SourceFile = apkPath;
                r.Pack.Save();
                return r;
            }
            finally { TryDelete(tmp); }
        }

        var arsc = zip.GetEntry("resources.arsc") is { } arscEntry ? ArscTable.TryParse(ReadAll(arscEntry)) : null;

        // Pack name and version from the manifest
        string name = Path.GetFileNameWithoutExtension(apkPath);
        string? version = null, package = null;
        if (zip.GetEntry("AndroidManifest.xml") is { } manifestEntry)
        {
            try
            {
                var tags = XmlElements.Read(ReadAll(manifestEntry), arsc);
                var manifest = tags.FirstOrDefault(t => t.Name == "manifest");
                var app = tags.FirstOrDefault(t => t.Name == "application");
                version = manifest?["versionName"];
                package = manifest?["package"];
                string? label = app?["label"];
                if (!string.IsNullOrWhiteSpace(label) && !label.StartsWith('@')) name = label.Trim();
            }
            catch { /* name stays the file name */ }
        }

        // appfilter.xml and drawable.xml: assets (text) first, then res/xml (binary)
        var appfilter = ReadPackXml(zip, arsc, "appfilter");
        var drawable = ReadPackXml(zip, arsc, "drawable");

        // Index of all image files below res/ (for APKs without an obfuscated resource table)
        var fileIndex = new Dictionary<string, List<(string Path, int Score)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in zip.Entries)
        {
            var m = ResFileRegex().Match(e.FullName);
            if (!m.Success) continue;
            string key = m.Groups[1].Value + "/" + m.Groups[3].Value;
            if (!fileIndex.TryGetValue(key, out var list)) fileIndex[key] = list = new();
            list.Add((e.FullName, DensityScore(m.Groups[2].Value, e.FullName)));
        }

        // Excluded: helper images for launchers (background, mask, overlay)
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in appfilter)
        {
            if (tag.Name is "iconback" or "iconmask" or "iconupon")
                foreach (var v in tag.Attributes.Values) excluded.Add(CleanName(v));
            else if (tag.Name == "item" && tag["component"] is { } comp && tag["drawable"] is { } dr && dr.Length > 0)
                components.TryAdd(comp, CleanName(dr));
        }

        // Icon list: drawable.xml (with categories) + everything referenced in appfilter.xml
        var categories = new List<string>();
        var ordered = new List<(string Name, int? Category)>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // name -> index in ordered
        var categoryMembers = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        int? current = null;
        foreach (var tag in drawable)
        {
            if (tag.Name == "category" && tag["title"] is { } title && title.Trim().Length > 0)
            {
                int idx = categories.FindIndex(c => c.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase));
                if (idx < 0) { categories.Add(title.Trim()); idx = categories.Count - 1; }
                current = idx;
            }
            else if (tag.Name == "item" && tag["drawable"] is { } dn)
            {
                string n = CleanName(dn);
                if (n.Length == 0 || excluded.Contains(n)) continue;
                if (!seen.ContainsKey(n)) { seen[n] = ordered.Count; ordered.Add((n, current)); }
                if (current is int c)
                {
                    if (!categoryMembers.TryGetValue(n, out var set)) categoryMembers[n] = set = new();
                    set.Add(c);
                }
            }
        }
        foreach (var n in components.Values)
            if (!excluded.Contains(n) && !seen.ContainsKey(n)) { seen[n] = ordered.Count; ordered.Add((n, null)); }

        // No lists at all: every image in the high-resolution drawable folders counts as an icon
        if (ordered.Count == 0)
        {
            foreach (var key in fileIndex.Keys.Where(k => k.StartsWith("drawable/", StringComparison.OrdinalIgnoreCase)))
            {
                string n = key["drawable/".Length..];
                if (fileIndex[key].Any(f => f.Score >= 600) && !excluded.Contains(n) && seen.TryAdd(n, ordered.Count)) ordered.Add((n, null));
            }
        }

        var pack = NewPack(target, name, PackSourceKind.Apk, apkPath);
        pack.Version = version;
        pack.PackageName = package;
        pack.Categories = categories;
        pack.Components = components;

        int missing = 0, unsupported = 0, failed = 0, done = 0;
        string phase = Loc.T("APK wird gelesen …", "Reading APK …");
        var pending = new List<PendingIcon>();
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var folderCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Step 1 (sequential, the ZIP reader is not thread-safe): read the image bytes
        void Take(ZipArchiveEntry entry, string iconName)
        {
            string safe = SafeName(iconName);
            if (!written.Add(safe)) return;
            try
            {
                pending.Add(new PendingIcon(safe, ReadAll(entry),
                    categoryMembers.TryGetValue(iconName, out var cats) ? cats.Order().ToArray() : null));
                string dir = Path.GetDirectoryName(entry.FullName.Replace('/', '\\')) ?? "";
                folderCounts[dir] = folderCounts.GetValueOrDefault(dir) + 1;
            }
            catch { written.Remove(safe); failed++; }
        }

        foreach (var (iconName, _) in ordered)
        {
            ct.ThrowIfCancellationRequested();
            if (++done % 200 == 0) progress?.Report(new(done, ordered.Count, phase));

            var entry = FindImage(zip, arsc, fileIndex, iconName, out bool onlyVector);
            if (entry is null) { if (onlyVector) unsupported++; else missing++; continue; }
            Take(entry, iconName);
        }

        // Icons that the pack ships but forgot to list (e.g. "gmail_alt3", "*_old"): everything else in the
        // folder that holds almost all listed icons. Launcher logos and preview images of the pack app are skipped.
        var main = folderCounts.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (main.Key is { } mainDir && main.Value >= pending.Count * 0.8
            && Regex.IsMatch(mainDir, @"^res\\(drawable|mipmap)", RegexOptions.IgnoreCase))
        {
            foreach (var e in zip.Entries)
            {
                if (!string.Equals(Path.GetDirectoryName(e.FullName.Replace('/', '\\')), mainDir, StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(e.Name).ToLowerInvariant();
                if (!ImageExtensions.Contains(ext)) continue;
                string n = Path.GetFileNameWithoutExtension(e.Name);
                if (n.EndsWith(".9", StringComparison.Ordinal) || excluded.Contains(n) || IsPackAppImage(n)) continue;
                if (!written.Contains(SafeName(n))) Take(e, n);
            }
        }

        // Step 2 (parallel): convert, write and create the preview cache
        failed += ProcessIcons(pack, pending, progress, ct);

        // Style building blocks of the pack (background plate, mask, overlay, scale) for the icon maker
        try { WriteStyle(zip, arsc, fileIndex, appfilter, pack.Folder); } catch { /* the maker then uses a neutral style */ }

        if (pack.Icons.Count == 0)
            throw new InvalidDataException(Loc.T("In dieser APK wurden keine Icons gefunden. Ist es ein Icon-Pack?",
                                                 "No icons were found in this APK. Is it an icon pack?"));
        DropEmptyCategories(pack);
        pack.Save();
        return new ImportResult(pack, missing, unsupported, failed);
    }

    /// <summary>An icon read from the source, not yet converted or written.</summary>
    private sealed record PendingIcon(string Name, byte[] Data, int[]? Categories, byte[]? OriginalIco = null);

    /// <summary>
    /// Converts to PNG, writes icons\*.png (and the original ICO if any) and builds thumbs.bin, using all cores.
    /// Adds the icons to the pack in their original order. Returns the number of failed icons.
    /// </summary>
    private static int ProcessIcons(IconPack pack, List<PendingIcon> pending, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        string phase = Loc.T("Icons werden übernommen …", "Importing icons …");
        var thumbs = new byte[]?[pending.Count];
        var ok = new bool[pending.Count];
        int done = 0;
        if (pending.Any(p => p.OriginalIco is not null)) Directory.CreateDirectory(pack.IcoFolder);

        Parallel.For(0, pending.Count, new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            var p = pending[i];
            try
            {
                byte[] png = ImageTools.IsPng(p.Data) ? p.Data : ImageTools.ToPng(p.Data);
                File.WriteAllBytes(Path.Combine(pack.IconsFolder, p.Name + ".png"), png);
                if (p.OriginalIco is not null) File.WriteAllBytes(Path.Combine(pack.IcoFolder, p.Name + ".ico"), p.OriginalIco);
                try { thumbs[i] = ThumbStore.MakeThumb(png); } catch { /* no preview cache for this one */ }
                ok[i] = true;
            }
            catch { /* counted below */ }
            int n = Interlocked.Increment(ref done);
            if (n % 100 == 0) progress?.Report(new(n, pending.Count, phase));
        });
        progress?.Report(new(pending.Count, pending.Count, phase));

        var list = new List<(string, byte[])>(pending.Count);
        for (int i = 0; i < pending.Count; i++)
        {
            if (!ok[i]) continue;
            pack.Icons.Add(new PackIcon { Name = pending[i].Name, Categories = pending[i].Categories });
            if (thumbs[i] is { } t) list.Add((pending[i].Name, t));
        }
        ThumbStore.Write(pack.Folder, list);
        return ok.Count(x => !x);
    }

    /// <summary>Images of the icon pack app itself (launcher logos for "apply", previews, splash screens).</summary>
    private static bool IsPackAppImage(string name) =>
        name.StartsWith("ic_", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("preview", StringComparison.OrdinalIgnoreCase)
        || name.Contains("splash", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("drawer_header", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("wallpaper", StringComparison.OrdinalIgnoreCase);

    private static List<XmlTag> ReadPackXml(ZipArchive zip, ArscTable? arsc, string name)
    {
        var entry = zip.GetEntry($"assets/{name}.xml") ?? zip.GetEntry($"res/xml/{name}.xml");
        if (entry is null && arsc is not null)
            foreach (var f in arsc.Files("xml", name))
                if ((entry = zip.GetEntry(f.Path)) is not null) break;
        if (entry is null) return new();
        try { return XmlElements.Read(ReadAll(entry), arsc); }
        catch { return new(); }
    }

    // ===================================================================== Pack style

    /// <summary>Saves iconback/iconmask/iconupon of appfilter.xml as style\back.png, mask.png, upon.png plus the scale factor.</summary>
    private static void WriteStyle(ZipArchive zip, ArscTable? arsc, Dictionary<string, List<(string Path, int Score)>> index,
                                   List<XmlTag> appfilter, string packFolder)
    {
        string styleFolder = Path.Combine(packFolder, PackStyle.FolderName);
        double scale = 1;
        bool any = false;
        foreach (var tag in appfilter)
        {
            string? file = tag.Name switch { "iconback" => "back", "iconmask" => "mask", "iconupon" => "upon", _ => null };
            if (tag.Name == "scale" && double.TryParse(tag["factor"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) && f > 0)
                scale = f;
            if (file is null) continue;
            // img1 is the first (often only) variant
            string? name = tag["img1"] ?? tag.Attributes.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var entry = FindImage(zip, arsc, index, CleanName(name), out _);
            if (entry is null) continue;
            byte[] data = ReadAll(entry);
            if (!ImageTools.IsPng(data)) data = ImageTools.ToPng(data);
            Directory.CreateDirectory(styleFolder);
            File.WriteAllBytes(Path.Combine(styleFolder, file + ".png"), data);
            any = true;
        }
        if (any || scale != 1)
        {
            Directory.CreateDirectory(styleFolder);
            File.WriteAllText(Path.Combine(styleFolder, "style.txt"), scale.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>For packs imported before the icon maker existed: read the style from the original APK again (if it still exists).</summary>
    public static bool TryExtractStyle(IconPack pack)
    {
        if (pack.SourceKind != PackSourceKind.Apk || !File.Exists(pack.SourceFile)) return false;
        try
        {
            using var zip = ZipFile.OpenRead(pack.SourceFile);
            var arsc = zip.GetEntry("resources.arsc") is { } a ? ArscTable.TryParse(ReadAll(a)) : null;
            var index = new Dictionary<string, List<(string Path, int Score)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in zip.Entries)
            {
                var m = ResFileRegex().Match(e.FullName);
                if (!m.Success) continue;
                string key = m.Groups[1].Value + "/" + m.Groups[3].Value;
                if (!index.TryGetValue(key, out var list)) index[key] = list = new();
                list.Add((e.FullName, DensityScore(m.Groups[2].Value, e.FullName)));
            }
            WriteStyle(zip, arsc, index, ReadPackXml(zip, arsc, "appfilter"), pack.Folder);
            return Directory.Exists(Path.Combine(pack.Folder, PackStyle.FolderName));
        }
        catch { return false; }
    }

    private static ZipArchiveEntry? FindImage(ZipArchive zip, ArscTable? arsc, Dictionary<string, List<(string Path, int Score)>> index,
                                              string name, out bool onlyVector)
    {
        onlyVector = false;
        var candidates = new List<(string Path, int Score)>();
        if (arsc is not null)
            foreach (var type in new[] { "drawable", "mipmap" })
                foreach (var f in arsc.Files(type, name))
                    candidates.Add((f.Path, ArscDensityScore(f.Density, f.Path)));
        foreach (var type in new[] { "drawable", "mipmap" })
            if (index.TryGetValue(type + "/" + name, out var list)) candidates.AddRange(list);

        foreach (var c in candidates.OrderByDescending(c => c.Score))
        {
            string ext = Path.GetExtension(c.Path).ToLowerInvariant();
            if (ext == ".xml") { onlyVector = true; continue; }
            if (ext.Length > 0 && !ImageExtensions.Contains(ext)) continue;
            if (zip.GetEntry(c.Path) is { } entry) { onlyVector = false; return entry; }
        }
        return null;
    }

    [GeneratedRegex(@"^res/(drawable|mipmap)((?:-[^/]+)?)/([^/]+?)(?:\.9)?\.\w+$", RegexOptions.IgnoreCase)]
    private static partial Regex ResFileRegex();

    /// <summary>Higher = better. Vector files get a low score so bitmaps of the same name win.</summary>
    private static int DensityScore(string qualifiers, string path)
    {
        if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return 1;
        string q = qualifiers.ToLowerInvariant();
        if (q.Contains("nodpi")) return 700;
        if (q.Contains("xxxhdpi")) return 640;
        if (q.Contains("xxhdpi")) return 480;
        if (q.Contains("xhdpi")) return 320;
        if (q.Contains("hdpi")) return 240;
        if (q.Contains("mdpi")) return 160;
        if (q.Contains("ldpi")) return 120;
        if (q.Contains("anydpi")) return 2;
        return 150;
    }

    private static int ArscDensityScore(int density, string path)
    {
        if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) return 1;
        return density switch
        {
            0xFFFF => 700,  // nodpi
            0xFFFE => 2,    // anydpi
            0 => 150,       // default
            _ => Math.Min(density, 699),
        };
    }

    // ===================================================================== Folder / ZIP

    private static ImportResult ImportZip(string zipPath, string target, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "IconWorkshop-" + Guid.NewGuid().ToString("N"));
        try
        {
            progress?.Report(new(0, 0, Loc.T("ZIP wird entpackt …", "Extracting ZIP …")));
            ZipFile.ExtractToDirectory(zipPath, tmp);
            var result = ImportFolder(tmp, target, progress, ct);
            result.Pack.Name = Path.GetFileNameWithoutExtension(zipPath);
            result.Pack.SourceKind = PackSourceKind.Zip;
            result.Pack.SourceFile = zipPath;
            result.Pack.Save();
            return result;
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }

    private static ImportResult ImportFolder(string folder, string target, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        progress?.Report(new(0, 0, Loc.T("Ordner wird durchsucht …", "Scanning folder …")));
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                             .Where(f => FolderExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        if (files.Count == 0)
            throw new InvalidDataException(Loc.T("Im Ordner wurden keine Bilder (PNG, ICO, WebP, JPG) gefunden.",
                                                 "No images (PNG, ICO, WebP, JPG) were found in the folder."));

        var pack = NewPack(target, new DirectoryInfo(folder).Name, PackSourceKind.Folder, folder);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<PendingIcon>();
        int failed = 0, done = 0;
        string phase = Loc.T("Ordner wird gelesen …", "Reading folder …");

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            if (++done % 100 == 0) progress?.Report(new(done, files.Count, phase));
            try
            {
                byte[] data = File.ReadAllBytes(file);
                bool isIco = Path.GetExtension(file).Equals(".ico", StringComparison.OrdinalIgnoreCase);

                string baseName = SafeName(Path.GetFileNameWithoutExtension(file));
                string unique = baseName;
                for (int i = 2; !names.Add(unique); i++) unique = $"{baseName}_{i}";

                // Subfolders become categories
                string? rel = Path.GetDirectoryName(Path.GetRelativePath(folder, file));
                int[]? cats = null;
                if (!string.IsNullOrEmpty(rel))
                {
                    int idx = pack.Categories.FindIndex(c => c.Equals(rel, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0) { pack.Categories.Add(rel); idx = pack.Categories.Count - 1; }
                    cats = [idx];
                }
                pending.Add(new PendingIcon(unique, data, cats, isIco ? data : null)); // ICO: the original is kept
            }
            catch { failed++; }
        }
        failed += ProcessIcons(pack, pending, progress, ct);
        if (pack.Icons.Count == 0)
            throw new InvalidDataException(Loc.T("Keines der Bilder konnte gelesen werden.", "None of the images could be read."));
        pack.Save();
        return new ImportResult(pack, 0, 0, failed);
    }

    // ===================================================================== Helpers

    private static IconPack NewPack(string target, string name, PackSourceKind kind, string source)
    {
        Directory.CreateDirectory(Path.Combine(target, "icons"));
        return new IconPack
        {
            Id = Path.GetFileName(target),
            Name = name,
            SourceKind = kind,
            SourceFile = source,
            ImportedUtc = DateTime.UtcNow,
            Folder = target,
        };
    }

    private static void DropEmptyCategories(IconPack pack)
    {
        var used = pack.Icons.Where(i => i.Categories is not null).SelectMany(i => i.Categories!).ToHashSet();
        if (used.Count == pack.Categories.Count) return;
        var map = new Dictionary<int, int>();
        var kept = new List<string>();
        for (int i = 0; i < pack.Categories.Count; i++)
            if (used.Contains(i)) { map[i] = kept.Count; kept.Add(pack.Categories[i]); }
        pack.Categories = kept;
        foreach (var icon in pack.Icons)
            if (icon.Categories is not null) icon.Categories = icon.Categories.Select(c => map[c]).ToArray();
    }

    private static string CleanName(string value)
    {
        value = value.Trim();
        int slash = value.LastIndexOf('/');
        if (value.StartsWith('@') && slash >= 0) value = value[(slash + 1)..];
        return value;
    }

    [GeneratedRegex(@"[^\w\-. ]")]
    private static partial Regex UnsafeChars();

    private static string SafeName(string name)
    {
        string s = UnsafeChars().Replace(name, "_").Trim().TrimEnd('.');
        return s.Length == 0 ? "icon" : s.Length > 100 ? s[..100] : s;
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var s = entry.Open();
        using var ms = new MemoryStream((int)Math.Max(0, Math.Min(entry.Length, int.MaxValue)));
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch { }
    }
}
