using IconWorkshop.Imaging;
using IconWorkshop.Packs;
using IconWorkshop.Programs;

namespace IconWorkshop.Services;

/// <summary>
/// Demo mode for screenshots (--demo, only together with --screenshots or --preview): a fresh, temporary data folder
/// with three generated icon packs under made-up names (icons from the bundled free symbol fonts, no third-party
/// icon pack art), the user's own name masked in paths, and programs whose current icon comes from the real
/// data folder left out. Nothing of the real setup is changed.
/// </summary>
public static class DemoMode
{
    public static bool IsOn { get; private set; }

    /// <summary>The real data folder (before the demo folder replaced it), to recognize real assigned icons.</summary>
    private static string _realDataFolder = "";
    private static readonly string Profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private const string DemoUser = "Alex";

    public static void Start()
    {
        IsOn = true;
        _realDataFolder = AppInfo.DataFolder;
        string folder = Path.Combine(Path.GetTempPath(), "IconWorkshop-Demo");
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
        Directory.CreateDirectory(folder);
        AppInfo.UseDataFolder(folder);
        CreatePacks(AppInfo.DefaultPacksFolder);
    }

    /// <summary>A path of the app for display: the temporary demo folder shows as the usual data folder, the program as installed.</summary>
    public static string DisplayPath(string path)
    {
        if (!IsOn) return path;
        path = path.Replace(AppInfo.DataFolder, _realDataFolder, StringComparison.OrdinalIgnoreCase);
        if (path.TrimEnd('\\').Equals(AppInfo.ProgramFolder.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Icon Workshop");
        return Mask(path);
    }

    /// <summary>Paths for display: the user's profile folder and name become a neutral demo user.</summary>
    public static string Mask(string text)
    {
        if (!IsOn || string.IsNullOrEmpty(text)) return text;
        string masked = text.Replace(Profile, Path.Combine(Path.GetDirectoryName(Profile) ?? @"C:\Users", DemoUser), StringComparison.OrdinalIgnoreCase);
        return masked.Replace(Environment.UserName, DemoUser, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A program whose shortcut already shows an icon of the real setup (would show third-party pack art).</summary>
    public static bool UsesRealIcons(ProgramEntry p) =>
        IsOn && p.Shortcuts.Any(s => s.Info.IconPath.StartsWith(_realDataFolder, StringComparison.OrdinalIgnoreCase));

    // ---------- Generated packs ----------

    private static readonly string[] Names =
    [
        "steam", "discord", "spotify", "firefox", "chrome", "telegram", "whatsapp", "github", "slack", "twitch", "youtube", "xbox",
        "excel", "word", "powerpoint", "obsidian", "thunderbird", "vivaldi", "pulsar", "onedrive", "wallpaper engine", "vlc",
        "music", "photos", "mail", "calendar", "notes", "calculator", "settings", "terminal", "code", "camera", "weather", "maps",
        "files", "backup", "downloads", "games", "video", "podcast", "browser", "store", "chat", "cloud", "security", "printer",
        "clock", "news", "books", "paint", "projects", "travel", "finance", "fitness", "recipes", "home",
    ];

    private sealed record DemoPack(string Name, TileShape Shape, GenLook Look, PaletteFamily Family);

    private static readonly DemoPack[] Packs =
    [
        new("Lumen", TileShape.Squircle, GenLook.Gradient, PaletteFamilies.Vivid),
        new("Pastella", TileShape.Circle, GenLook.Flat, PaletteFamilies.Pastel),
        new("Nightglow", TileShape.RoundedSquare, GenLook.Neon, PaletteFamilies.Neon),
    ];

    private static void CreatePacks(string packsRoot)
    {
        Directory.CreateDirectory(packsRoot);
        var style = PackStyle.Neutral();
        foreach (var demo in Packs)
        {
            string source = Path.Combine(Path.GetTempPath(), "IconWorkshop-DemoSource", demo.Name);
            try { if (Directory.Exists(source)) Directory.Delete(source, true); } catch { }
            Directory.CreateDirectory(source);
            for (int i = 0; i < Names.Length; i++)
            {
                var input = new GenInput { Name = Names[i], Style = style };
                var content = IconGenerator.ContentOptions(input).FirstOrDefault(c => c.Kind != GenContentKind.None) ?? new GenContent(GenContentKind.Monogram, IconGenerator.Monogram(Names[i]));
                var palette = demo.Family.Palettes[i % demo.Family.Palettes.Count];
                var design = IconGenerator.Compose(new GenParts(content, palette, demo.Shape, demo.Look), input);
                File.WriteAllBytes(Path.Combine(source, Names[i].Replace(' ', '_') + ".png"), DesignRenderer.RenderPng(design, style));
            }
            PackImporter.Import(source, Path.Combine(packsRoot, demo.Name), null, CancellationToken.None);
        }
    }
}
