using System.Windows.Media;

namespace IconWorkshop.Imaging;

/// <summary>A named set of matching backgrounds (gradient pairs).</summary>
public sealed record PaletteFamily(string Id, string Name, IReadOnlyList<GenPalette> Palettes);

/// <summary>Curated color families for the editor and the generator.</summary>
public static class PaletteFamilies
{
    private static GenPalette P(string a, string b) => new((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b));

    public static readonly PaletteFamily Vivid = new("vivid", Loc.T("Kräftig", "Vivid"), IconGenerator.Palettes);

    public static readonly PaletteFamily Fluent = new("fluent", "Fluent",
    [
        P("#0078D4", "#005A9E"), P("#0099BC", "#005B70"), P("#8764B8", "#5C2E91"), P("#00B294", "#00796B"),
        P("#E74856", "#C30052"), P("#FFB900", "#F7630C"), P("#498205", "#107C10"), P("#6B69D6", "#4A4899"),
    ]);

    public static readonly PaletteFamily Pastel = new("pastel", Loc.T("Pastell", "Pastel"),
    [
        P("#A5B4FC", "#818CF8"), P("#93C5FD", "#60A5FA"), P("#6EE7B7", "#34D399"), P("#FDE68A", "#FBBF24"),
        P("#FDA4AF", "#FB7185"), P("#C4B5FD", "#A78BFA"), P("#99F6E4", "#5EEAD4"), P("#FDBA74", "#FB923C"),
    ]);

    public static readonly PaletteFamily Neon = new("neon", "Neon",
    [
        P("#FF00E5", "#7C00FF"), P("#00F0FF", "#0066FF"), P("#39FF14", "#00A86B"), P("#FFE600", "#FF7A00"),
        P("#FF2D55", "#B0005B"), P("#8B5CF6", "#06B6D4"),
    ]);

    public static readonly PaletteFamily Mono = new("mono", Loc.T("Grautöne", "Grays"),
    [
        P("#4B5563", "#1F2937"), P("#6B7280", "#374151"), P("#9CA3AF", "#4B5563"), P("#1F2937", "#000000"),
        P("#E5E7EB", "#9CA3AF"), P("#334155", "#0F172A"),
    ]);

    public static readonly PaletteFamily Earth = new("earth", Loc.T("Erdtöne", "Earth tones"),
    [
        P("#A16207", "#713F12"), P("#B45309", "#78350F"), P("#4D7C0F", "#365314"), P("#9A3412", "#7C2D12"),
        P("#57534E", "#292524"), P("#A8A29E", "#57534E"), P("#0F766E", "#134E4A"),
    ]);

    public static readonly IReadOnlyList<PaletteFamily> All = [Vivid, Fluent, Pastel, Neon, Mono, Earth];

    public static PaletteFamily Get(string? id) => All.FirstOrDefault(f => f.Id == id) ?? Vivid;
}

/// <summary>
/// A category of icons with its typical look: shapes, color family, looks and symbols to fall back on.
/// Folders, drives and file types use the Windows-like shapes.
/// </summary>
public sealed record IconCategory(
    string Id, string Name, string Glyph,
    IReadOnlyList<TileShape> Shapes, IReadOnlyList<string> Families, IReadOnlyList<GenLook> Looks,
    IReadOnlyList<string> Terms, bool UsePackShape = false)
{
    public IEnumerable<GenPalette> Palettes => Families.SelectMany(f => PaletteFamilies.Get(f).Palettes);
}

public static class IconCategories
{
    public static readonly IconCategory Free = new("free", Loc.T("Frei", "Free"), "",
        [TileShape.Squircle, TileShape.RoundedSquare, TileShape.Circle], ["vivid"],
        [GenLook.Gradient, GenLook.Flat, GenLook.LongShadow, GenLook.Glass, GenLook.Neon, GenLook.Light], [], UsePackShape: true);

    public static readonly IconCategory Programs = new("programs", Loc.T("Programme", "Programs"), "",
        [TileShape.Squircle, TileShape.RoundedSquare], ["fluent", "vivid"], [GenLook.Gradient, GenLook.Flat, GenLook.Glass, GenLook.Light],
        ["apps", "widgets"], UsePackShape: true);

    public static readonly IconCategory Games = new("games", Loc.T("Spiele", "Games"), "",
        [TileShape.Squircle, TileShape.Hexagon, TileShape.Shield, TileShape.Circle], ["neon", "vivid"],
        [GenLook.Neon, GenLook.Gradient, GenLook.LongShadow, GenLook.Glass], ["sports_esports", "joystick", "swords", "trophy"]);

    public static readonly IconCategory Tools = new("tools", Loc.T("Tools", "Tools"), "",
        [TileShape.RoundedSquare, TileShape.Square, TileShape.Squircle], ["mono", "fluent"],
        [GenLook.Flat, GenLook.Light, GenLook.Gradient], ["build", "terminal", "code", "tune"]);

    public static readonly IconCategory Office = new("office", Loc.T("Büro", "Office"), "",
        [TileShape.RoundedSquare, TileShape.Squircle, TileShape.Document], ["fluent", "pastel"],
        [GenLook.Light, GenLook.Flat, GenLook.Gradient], ["description", "edit_note", "calendar_month", "mail"]);

    public static readonly IconCategory Media = new("media", Loc.T("Medien", "Media"), "",
        [TileShape.Circle, TileShape.Squircle, TileShape.Drop], ["vivid", "pastel"],
        [GenLook.Gradient, GenLook.Glass, GenLook.LongShadow], ["play_circle", "music_note", "photo_camera", "movie"]);

    public static readonly IconCategory System = new("system", Loc.T("System", "System"), "",
        [TileShape.Squircle, TileShape.Circle, TileShape.RoundedSquare], ["mono", "fluent"],
        [GenLook.Flat, GenLook.Light, GenLook.Gradient], ["settings", "shield", "memory", "monitor"]);

    public static readonly IconCategory Folder = new("folder", Loc.T("Ordner", "Folder"), "",
        [TileShape.Folder], ["fluent", "vivid", "earth", "pastel"], [GenLook.Flat, GenLook.Gradient], ["star", "work", "lock", "cloud"]);

    public static readonly IconCategory Drive = new("drive", Loc.T("Laufwerk", "Drive"), "",
        [TileShape.Drive], ["mono", "fluent"], [GenLook.Gradient, GenLook.Flat], ["hard_drive", "save", "backup", "cloud"]);

    public static readonly IconCategory FileType = new("filetype", Loc.T("Dateityp", "File type"), "",
        [TileShape.Document], ["fluent", "vivid"], [GenLook.Light, GenLook.Flat, GenLook.Gradient], ["description"]);

    public static readonly IReadOnlyList<IconCategory> All = [Free, Programs, Games, Tools, Office, Media, System, Folder, Drive, FileType];

    public static IconCategory Get(string? id) => All.FirstOrDefault(c => c.Id == id) ?? Free;
}
