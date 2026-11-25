using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconWorkshop.Imaging;

public enum TileShape { PackPlate, RoundedSquare, Squircle, Circle, Square, Portrait, Landscape, Pill, Hexagon, Octagon, Triangle, Shield, Drop, None, Folder, Document, Drive }
public enum FillKind { Solid, Linear, Radial }
public enum ShadowKind { None, Soft, Long }
public enum LayerKind { Glyph, Text, Image, Badge }

/// <summary>Solid color or gradient. Colors are stored as "#AARRGGBB" so designs stay readable JSON.</summary>
public sealed class FillSpec
{
    public FillKind Kind { get; set; } = FillKind.Solid;
    public string Color1 { get; set; } = "#FFFFFFFF";
    public string Color2 { get; set; } = "#FF808080";
    public double Angle { get; set; } = 135;

    [JsonIgnore] public Color C1 { get => Parse(Color1); set => Color1 = value.ToString(); }
    [JsonIgnore] public Color C2 { get => Parse(Color2); set => Color2 = value.ToString(); }

    public static FillSpec Of(Color c) => new() { C1 = c };
    internal static Color Parse(string s) { try { return (Color)ColorConverter.ConvertFromString(s); } catch { return Colors.Transparent; } }
}

public sealed class StrokeSpec
{
    public bool On { get; set; }
    public string Color { get; set; } = "#FFFFFFFF";
    public double Width { get; set; } = 6;
    [JsonIgnore] public Color C { get => FillSpec.Parse(Color); set => Color = value.ToString(); }
}

public sealed class ShadowSpec
{
    public ShadowKind Kind { get; set; } = ShadowKind.None;
    public string Color { get; set; } = "#FF000000";
    public double Opacity { get; set; } = 0.35;
    /// <summary>Blur radius (soft) or length (long), in pixels of the 256 px icon.</summary>
    public double Size { get; set; } = 18;
    public double Distance { get; set; } = 6;
    /// <summary>Direction in degrees: 315 = towards bottom right (like light from the top left).</summary>
    public double Angle { get; set; } = 315;
    [JsonIgnore] public Color C { get => FillSpec.Parse(Color); set => Color = value.ToString(); }
}

/// <summary>One content layer: symbol, text, image or badge (circle with text).</summary>
public sealed class MakerLayer
{
    public LayerKind Kind { get; set; } = LayerKind.Glyph;
    public string Name { get; set; } = "";
    public bool Visible { get; set; } = true;

    public string Glyph { get; set; } = "";
    /// <summary>Icon font of the symbol: "fluent" (Windows), "material", "tabler", "fa-solid", "fa-regular", "fa-brands".</summary>
    public string GlyphSet { get; set; } = "fluent";
    public string Text { get; set; } = "Aa";
    public string Font { get; set; } = "Segoe UI Variable Display";
    public bool Bold { get; set; } = true;
    /// <summary>Image as PNG (program icon, own image or pack icon); stored in the design so it can be re-edited anywhere.</summary>
    public byte[]? ImagePng { get; set; }
    /// <summary>Images: show in the fill color only (silhouette).</summary>
    public bool Tint { get; set; }

    public FillSpec Fill { get; set; } = FillSpec.Of(Colors.White);
    public FillSpec BadgeFill { get; set; } = FillSpec.Of(Color.FromRgb(0xE8, 0x11, 0x23));
    public StrokeSpec Outline { get; set; } = new();
    public ShadowSpec Shadow { get; set; } = new();

    /// <summary>Size relative to the icon (0.6 = 60 %), position offset relative to the icon, rotation in degrees.</summary>
    public double Size { get; set; } = 0.6;
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;

    [JsonIgnore] private BitmapSource? _image;
    [JsonIgnore]
    public BitmapSource? Image
    {
        get
        {
            if (_image is null && ImagePng is { Length: > 0 } png) _image = ImageTools.DecodeThumbnail(png, 512);
            return _image;
        }
    }

    public void SetImage(byte[] png)
    {
        ImagePng = png;
        _image = null;
    }

    public string DisplayName => Name.Length > 0 ? Name : Kind switch
    {
        LayerKind.Glyph => Loc.T("Symbol", "Symbol") + (IconFonts.Get(GlyphSet).NameOf(Glyph) is { } n ? $" ({n})" : ""),
        LayerKind.Text => Loc.T("Text", "Text") + $" „{Text}“",
        LayerKind.Image => Loc.T("Bild", "Image"),
        _ => Loc.T("Badge", "Badge") + $" „{Text}“",
    };

    public MakerLayer Clone() => JsonSerializer.Deserialize<MakerLayer>(JsonSerializer.Serialize(this))!;
}

/// <summary>A complete icon design: tile (shape, fill, border, shadow, gloss) plus layers. Saved next to own icons for re-editing.</summary>
public sealed class MakerDesign
{
    public int Version { get; set; } = 1;
    public string? StylePackId { get; set; }
    public TileShape Shape { get; set; } = TileShape.PackPlate;
    /// <summary>With the pack plate shape: use the pack's own plate image instead of the fill.</summary>
    public bool UsePackPlate { get; set; } = true;
    public FillSpec Background { get; set; } = new() { Kind = FillKind.Linear, Color1 = "#FF2563EB", Color2 = "#FF6B2FBF", Angle = 135 };
    public StrokeSpec Border { get; set; } = new() { Width = 6 };
    public ShadowSpec TileShadow { get; set; } = new();
    public bool Gloss { get; set; }
    /// <summary>Draw the pack's overlay (frame/gloss image) on top.</summary>
    public bool PackOverlay { get; set; } = true;
    /// <summary>Cut the layers to the tile shape.</summary>
    public bool ClipToShape { get; set; } = true;
    public List<MakerLayer> Layers { get; set; } = new();

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public string ToJson() => JsonSerializer.Serialize(this, Json);
    public static MakerDesign? FromJson(string json) { try { return JsonSerializer.Deserialize<MakerDesign>(json); } catch { return null; } }
    public MakerDesign Clone() => FromJson(ToJson())!;

    public static MakerDesign Default() => new()
    {
        Layers = { new MakerLayer { Kind = LayerKind.Glyph, Glyph = "", Fill = FillSpec.Of(Colors.White), Size = 0.55 } },
    };

    // ---------- Templates: change the look, keep the content ----------

    public sealed record Template(string Name, Action<MakerDesign> Apply);

    public static IReadOnlyList<Template> Templates() =>
    [
        new(Loc.T("Flach", "Flat"), d =>
        {
            d.Gloss = false; d.TileShadow.Kind = ShadowKind.None; d.Border.On = false;
            if (d.Background.Kind != FillKind.Solid) d.Background.Kind = FillKind.Solid;
            foreach (var l in d.Layers) { l.Shadow.Kind = ShadowKind.None; l.Outline.On = false; }
        }),
        new(Loc.T("Glas", "Glass"), d =>
        {
            d.Gloss = true; d.UsePackPlate = false; d.Background.Kind = FillKind.Linear; d.Background.Angle = 90;
            d.TileShadow = new ShadowSpec { Kind = ShadowKind.Soft, Opacity = 0.4, Size = 18, Distance = 6 };
            d.Border = new StrokeSpec { On = true, Color = "#55FFFFFF", Width = 4 };
            foreach (var l in d.Layers) l.Shadow = new ShadowSpec { Kind = ShadowKind.Soft, Opacity = 0.35, Size = 10, Distance = 3 };
        }),
        new(Loc.T("Langer Schatten", "Long shadow"), d =>
        {
            d.Gloss = false; d.UsePackPlate = false; d.Background.Kind = FillKind.Solid; d.ClipToShape = true;
            foreach (var l in d.Layers) l.Shadow = new ShadowSpec { Kind = ShadowKind.Long, Opacity = 0.28, Size = 120, Angle = 315 };
        }),
        new(Loc.T("Kontur", "Outline"), d =>
        {
            d.UsePackPlate = false; d.Gloss = false; d.TileShadow.Kind = ShadowKind.None;
            d.Background = FillSpec.Of(Color.FromRgb(0x1B, 0x1B, 0x1F));
            d.Border = new StrokeSpec { On = true, Color = d.Layers.FirstOrDefault()?.Fill.Color1 ?? "#FFFFFFFF", Width = 8 };
            foreach (var l in d.Layers) l.Shadow.Kind = ShadowKind.None;
        }),
        new(Loc.T("Neon", "Neon"), d =>
        {
            d.UsePackPlate = false; d.Gloss = false;
            d.Background = FillSpec.Of(Color.FromRgb(0x10, 0x10, 0x18));
            var glow = d.Layers.FirstOrDefault()?.Fill.Color1 ?? "#FF00E5FF";
            d.Border = new StrokeSpec { On = true, Color = glow, Width = 5 };
            foreach (var l in d.Layers) l.Shadow = new ShadowSpec { Kind = ShadowKind.Soft, Color = l.Fill.Color1, Opacity = 1, Size = 22, Distance = 0 };
        }),
    ];
}
