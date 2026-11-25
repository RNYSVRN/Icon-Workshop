using System.IO;
using System.Text.Json;

namespace IconWorkshop.Services;

/// <summary>Settings in %LOCALAPPDATA%\Icon-Workshop\settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>"auto" (Windows display language), "de" or "en".</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Custom location of the icon packs; null = default folder in the data folder.</summary>
    public string? PacksFolder { get; set; }

    /// <summary>Last selected pack in the library ("" = all packs).</summary>
    public string? LibraryPack { get; set; }

    /// <summary>Tile size in the library (small, medium, large).</summary>
    public int LibraryTileSize { get; set; } = 1;

    /// <summary>Programs shown as large tiles instead of a list.</summary>
    public bool ProgramsAsTiles { get; set; }

    /// <summary>Window position and size of the last session (null = centered default).</summary>
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    /// <summary>Navigation collapsed to symbols (menu button).</summary>
    public bool NavCollapsed { get; set; }

    /// <summary>Folders added on the Folders page.</summary>
    public List<string> CustomFolders { get; set; } = new();

    /// <summary>File types added on the File types page (".ext").</summary>
    public List<string> CustomFileTypes { get; set; } = new();

    public string EffectivePacksFolder =>
        string.IsNullOrWhiteSpace(PacksFolder) ? AppInfo.DefaultPacksFolder : PacksFolder!;

    private static string FilePath => Path.Combine(AppInfo.DataFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* corrupt file -> defaults */ }
        return new();
    }

    /// <summary>Don't save anything (screenshot mode).</summary>
    public static bool ReadOnly { get; set; }

    public void Save()
    {
        if (ReadOnly) return;
        try
        {
            Directory.CreateDirectory(AppInfo.DataFolder);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { /* saving is not critical */ }
    }
}
