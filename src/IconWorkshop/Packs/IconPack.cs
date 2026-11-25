using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IconWorkshop.Packs;

/// <summary>One icon in a pack. The preview image lives in icons\&lt;Name&gt;.png.</summary>
public sealed class PackIcon
{
    [JsonPropertyName("n")] public string Name { get; set; } = "";

    /// <summary>Indexes into <see cref="IconPack.Categories"/>; null = no category.</summary>
    [JsonPropertyName("c")] public int[]? Categories { get; set; }

    /// <summary>Readable title: "chrome_alt6" becomes "chrome alt6".</summary>
    [JsonIgnore] public string Title => Name.Replace('_', ' ');
}

public enum PackSourceKind { Apk, Folder, Zip }

/// <summary>An imported icon pack: pack.json + icons\*.png (+ ico\*.ico, created on first use).</summary>
public sealed class IconPack
{
    public const string FileName = "pack.json";

    public int FormatVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? PackageName { get; set; }
    public PackSourceKind SourceKind { get; set; }
    public string SourceFile { get; set; } = "";
    public DateTime ImportedUtc { get; set; }
    public List<string> Categories { get; set; } = new();
    public List<PackIcon> Icons { get; set; } = new();

    /// <summary>Android app component -> icon name (from appfilter.xml), used for automatic suggestions later.</summary>
    public Dictionary<string, string> Components { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore] public string Folder { get; set; } = "";

    /// <summary>Preview cache (thumbs.bin); null while it doesn't exist yet.</summary>
    [JsonIgnore] public ThumbStore? Thumbs { get; set; }
    [JsonIgnore] public string IconsFolder => Path.Combine(Folder, "icons");
    [JsonIgnore] public string IcoFolder => Path.Combine(Folder, "ico");

    public string PngPath(PackIcon icon) => Path.Combine(IconsFolder, icon.Name + ".png");
    public string IcoPath(PackIcon icon) => Path.Combine(IcoFolder, icon.Name + ".ico");

    /// <summary>The first icon of the pack (or the one matching the pack name) for the pack card.</summary>
    [JsonIgnore]
    public string? CoverPath
    {
        get
        {
            if (Icons.Count == 0) return null;
            var cover = Icons.FirstOrDefault(i => i.Name.Equals("icon_pack", StringComparison.OrdinalIgnoreCase)
                                                 || i.Name.Equals("ic_launcher", StringComparison.OrdinalIgnoreCase))
                        ?? Icons[0];
            return PngPath(cover);
        }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public static IconPack Load(string folder)
    {
        var pack = JsonSerializer.Deserialize<IconPack>(File.ReadAllText(Path.Combine(folder, FileName)), Json)
                   ?? throw new InvalidDataException("pack.json is empty");
        pack.Folder = folder;
        pack.Components = new Dictionary<string, string>(pack.Components ?? new(), StringComparer.OrdinalIgnoreCase);
        return pack;
    }

    public void Save()
    {
        string file = Path.Combine(Folder, FileName);
        string tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, file, overwrite: true);
    }

    /// <summary>Size of the pack folder in bytes.</summary>
    public long SizeOnDisk()
    {
        try { return new DirectoryInfo(Folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
        catch { return 0; }
    }
}
