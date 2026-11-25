namespace IconWorkshop.Services;

/// <summary>
/// Profiles: a complete look (all assignments with their icons) saved under a name, to switch back and forth,
/// e.g. "Dark" and "Colorful". A profile is a normal export (ZIP) in the data folder, so it also works after
/// a pack was deleted.
/// </summary>
public static class Profiles
{
    public sealed record Profile(string Name, string File, DateTime Saved)
    {
        public string Label => $"{Name}  ·  {Saved:g}";
    }

    /// <summary>The tests point this to a temp folder.</summary>
    internal static string Folder = Path.Combine(AppInfo.DataFolder, "Profiles");

    public static List<Profile> All()
    {
        if (!Directory.Exists(Folder)) return new();
        return Directory.EnumerateFiles(Folder, "*.zip")
            .Select(f => new Profile(Path.GetFileNameWithoutExtension(f), f, File.GetLastWriteTime(f)))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static string FileFor(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string safe = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('.', ' ');
        if (safe.Length == 0) safe = "Profil";
        Directory.CreateDirectory(Folder);
        return Path.Combine(Folder, safe + ".zip");
    }

    public static void Delete(Profile p) { if (File.Exists(p.File)) File.Delete(p.File); }
}
