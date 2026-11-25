using System.Globalization;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IconWorkshop.Packs;

namespace IconWorkshop.Imaging;

public enum GenContentKind { Glyph, Monogram, ProgramIcon, None }
public enum GenLook { Flat, Gradient, LongShadow, Glass, Neon, Light }

/// <summary>What a variant shows: a symbol of an icon font, initials, or the program's own icon.</summary>
public sealed record GenContent(GenContentKind Kind, string Value = "", string GlyphSet = "");

/// <summary>Two colors of a background (gradient from C1 to C2).</summary>
public sealed record GenPalette(Color C1, Color C2);

/// <summary>The four independent parts of a generated variant; each can be kept ("locked") while rolling the dice.</summary>
public sealed record GenParts(GenContent Content, GenPalette Palette, TileShape Shape, GenLook Look);

/// <summary>Input of the generator: what the icon is for.</summary>
public sealed class GenInput
{
    public string Name { get; init; } = "";
    /// <summary>The program's current icon as PNG (optional): becomes a content option and gives the main color.</summary>
    public byte[]? ProgramPng { get; init; }
    /// <summary>Only for the colors (e.g. Steam's tiny game icon when the real icon is missing).</summary>
    public byte[]? ColorPng { get; init; }
    public PackStyle Style { get; init; } = PackStyle.Neutral();
    public string? StylePackId { get; init; }
    /// <summary>Typical look of the icon (programs, games, folder, drive ...): shapes, colors, looks, fallback symbols.</summary>
    public IconCategory Category { get; init; } = IconCategories.Free;
}

/// <summary>Which parts stay as they are in the selected variant.</summary>
public sealed record GenLocks(bool Content, bool Colors, bool Shape, bool Look)
{
    public static readonly GenLocks None = new(false, false, false, false);
}

/// <summary>
/// One-click generator: turns a name (and optionally a program icon) into many finished icon designs.
/// Symbols come from the icon fonts (brand logos only on an exact name match, topics via the name search with
/// German synonyms), otherwise initials. Colors come from curated palettes and the main color of the program icon.
/// Everything runs locally; the results are normal <see cref="MakerDesign"/>s that can be edited further.
/// </summary>
public static class IconGenerator
{
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "microsoft", "google", "adobe", "the", "for", "and", "app", "desktop", "windows", "x64", "x86", "bit", "pro", "free",
        "edition", "launcher", "client", "beta", "preview", "der", "die", "das", "und", "fur", "mein", "meine", "my", "new", "neu",
    };

    /// <summary>Curated background pairs that look good with white symbols.</summary>
    public static readonly IReadOnlyList<GenPalette> Palettes =
    [
        P("#2563EB", "#7C3AED"), P("#0EA5E9", "#2563EB"), P("#10B981", "#0F766E"), P("#22C55E", "#15803D"),
        P("#F59E0B", "#EA580C"), P("#F43F5E", "#BE123C"), P("#EC4899", "#8B5CF6"), P("#6366F1", "#312E81"),
        P("#14B8A6", "#0EA5E9"), P("#EF4444", "#F97316"), P("#64748B", "#1E293B"), P("#374151", "#111827"),
        P("#EAB308", "#D97706"), P("#8B5CF6", "#4C1D95"), P("#06B6D4", "#3B82F6"), P("#84CC16", "#16A34A"),
    ];


    /// <summary>All content options for a name, best first.</summary>
    public static List<GenContent> ContentOptions(GenInput input)
    {
        var list = new List<GenContent>();
        var words = Words(input.Name).Where(w => !Noise.Contains(w)).ToList();
        string compact = string.Concat(words);
        var category = input.Category;
        // Folders: a plain folder in a color is often just right
        if (category == IconCategories.Folder) list.Add(new GenContent(GenContentKind.None));
        // File types: the extension on the page ("PSD"); drives: the letter ("D:")
        if (category == IconCategories.FileType && input.Name.Trim().TrimStart('.') is { Length: > 0 and <= 5 } ext && ext.All(char.IsLetterOrDigit))
            list.Add(new GenContent(GenContentKind.Monogram, ext.ToUpperInvariant()));
        if (category == IconCategories.Drive && input.Name.Length >= 2 && char.IsLetter(input.Name[0]) && input.Name[1] == ':')
            list.Add(new GenContent(GenContentKind.Monogram, char.ToUpperInvariant(input.Name[0]) + ":"));

        // Brand logo (Font Awesome Brands): only exact names ("steam", "discord", "spotify")
        var brands = IconFonts.Get("fa-brands");
        foreach (var key in new[] { compact }.Concat(words).Where(k => k.Length >= 3).Distinct())
        {
            var hit = brands.Glyphs.FirstOrDefault(g => g.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) list.Add(new GenContent(GenContentKind.Glyph, hit.Glyph, brands.Id));
        }
        if (input.ProgramPng is not null) list.Add(new GenContent(GenContentKind.ProgramIcon));

        // Topic symbols: name search (Material first, then Tabler), German words are translated
        foreach (var (setId, take) in new[] { ("material", 4), ("tabler", 2) })
        {
            var set = IconFonts.Get(setId);
            foreach (var w in words.Where(w => w.Length >= 3))
            {
                // Whole words of the symbol name first ("work" -> work, home_work; not network or brand-upwork)
                string? en = GlyphNames.Translate(w);
                var usable = set.Search(w).Where(g => set.Has(g) && !IsNegative(set.NameOf(g)) && !Tokens(set.NameOf(g)).Contains("brand")).ToList();
                var whole = usable.Where(g => Tokens(set.NameOf(g)).Any(t => t == w || t == en)).ToList();
                foreach (var g in Preferred(set, w, en).Concat(whole.Count > 0 ? whole : list.Any(c => c.Kind == GenContentKind.Glyph) ? [] : usable).Distinct().Take(take))
                    if (!list.Any(c => c.Value == g && c.GlyphSet == setId)) list.Add(new GenContent(GenContentKind.Glyph, g, setId));
            }
        }
        if (category != IconCategories.FileType && category != IconCategories.Drive && Monogram(input.Name) is { Length: > 0 } initials) list.Add(new GenContent(GenContentKind.Monogram, initials));
        // Typical symbols of the category, or a few friendly general ones when the name gives nothing
        if (category.Terms.Count > 0 || !list.Any(c => c.Kind == GenContentKind.Glyph))
        {
            var material = IconFonts.Get("material");
            foreach (var q in category.Terms.Count > 0 ? category.Terms : ["star", "bolt", "favorite", "rocket"])
                if (material.Search(q).FirstOrDefault(g => material.Has(g) && !IsNegative(material.NameOf(g)) && (Tokens(material.NameOf(g)).Contains(q) || string.Join("_", Tokens(material.NameOf(g))) == q)) is { } g && !list.Any(c => c.Value == g)) list.Add(new GenContent(GenContentKind.Glyph, g, material.Id));
        }
        return list;
    }

    // Symbols that say "not" (crossed out, removed, error ...) make poor app icons
    private static readonly HashSet<string> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "off", "remove", "cancel", "block", "blocked", "disabled", "no", "x", "minus", "slash", "error", "warning", "alert", "question", "exclamation", "dollar", "check", "plus",
    };

    // The classic symbol for common topics (the name search alone finds e.g. "game button l" before the gamepad)
    private static readonly Dictionary<string, string[]> Classic = new(StringComparer.OrdinalIgnoreCase)
    {
        ["game"] = ["sports esports", "stadia controller", "joystick", "gamepad"], ["games"] = ["sports esports", "stadia controller", "joystick"],
        ["music"] = ["music note", "library music", "headphones", "album"], ["photo"] = ["photo camera", "image", "photo library"],
        ["image"] = ["image", "photo library"], ["movie"] = ["movie", "videocam", "theaters"], ["video"] = ["movie", "videocam", "smart display"],
        ["work"] = ["work", "business center", "home work"], ["code"] = ["code", "terminal", "data object"], ["mail"] = ["mail", "inbox"],
        ["download"] = ["download", "downloading"], ["description"] = ["description", "article"], ["database"] = ["database", "storage"],
        ["chat"] = ["chat", "forum"], ["settings"] = ["settings", "tune"], ["cloud"] = ["cloud", "cloud upload"],
    };

    private static IEnumerable<string> Preferred(IconFontSet set, string word, string? english)
    {
        if (!Classic.TryGetValue(english ?? word, out var names) && !Classic.TryGetValue(word, out names)) yield break;
        foreach (var name in names)
            if (set.Glyphs.FirstOrDefault(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || g.Name.Replace('-', ' ').Equals(name, StringComparison.OrdinalIgnoreCase)) is { } hit && set.Has(hit.Glyph))
                yield return hit.Glyph;
    }

    private static string[] Tokens(string? glyphName) => (glyphName ?? "").ToLowerInvariant().Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);

    private static bool IsNegative(string? glyphName) =>
        glyphName is not null && glyphName.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries).Any(NegativeWords.Contains);

    /// <summary>Creates <paramref name="count"/> variants. Locked parts are taken from <paramref name="keep"/>.</summary>
    public static List<GenParts> Roll(GenInput input, int count, int seed, GenLocks? locks = null, GenParts? keep = null)
    {
        locks ??= GenLocks.None;
        var rnd = new Random(seed);
        var contents = ContentOptions(input);
        var palettes = PalettesFor(input).ToList();
        var shapes = ShapesFor(input).ToList();
        var looks = input.Category.Looks;
        var result = new List<GenParts>();
        var seen = new HashSet<GenParts>();
        int offset = rnd.Next(1000);
        for (int i = 0; result.Count < count && i < count * 6; i++)
        {
            // Round robin over the contents (best ones appear first), random rest
            var content = locks.Content && keep is not null ? keep.Content : contents[(i + (seed == 0 ? 0 : offset)) % contents.Count];
            var palette = locks.Colors && keep is not null ? keep.Palette
                : i < 2 && seed == 0 ? palettes[i % palettes.Count] : palettes[rnd.Next(palettes.Count)];
            var shape = locks.Shape && keep is not null ? keep.Shape : shapes[rnd.Next(shapes.Count)];
            var look = locks.Look && keep is not null ? keep.Look : i == 0 && seed == 0 ? looks[0] : looks[rnd.Next(looks.Count)];
            var parts = new GenParts(content, palette, shape, look);
            if (seen.Add(parts)) result.Add(parts);
        }
        return result;
    }

    /// <summary>Builds the editable design of a variant.</summary>
    public static MakerDesign Compose(GenParts p, GenInput input)
    {
        var style = input.Style;
        bool plate = p.Shape == TileShape.PackPlate && style.Back is not null;
        var d = new MakerDesign
        {
            StylePackId = input.StylePackId,
            Shape = p.Shape,
            UsePackPlate = plate,
            PackOverlay = true,
            ClipToShape = true,
            Background = new FillSpec { Kind = FillKind.Linear, C1 = p.Palette.C1, C2 = p.Palette.C2, Angle = 135 },
        };

        // Content color: white on colored tiles, the palette color on light tiles, the plate's contrast color on pack plates
        Color ink = plate ? style.ContrastColor : Luminance(p.Palette.C1) > 175 ? Color.FromRgb(0x1F, 0x29, 0x37) : Colors.White;
        if (!plate && p.Look == GenLook.Light) ink = Darken(p.Palette.C1, Luminance(p.Palette.C1) > 175 ? 0.45 : 0.1);
        if (!plate && p.Look == GenLook.Neon) ink = Lighten(p.Palette.C1, 0.25);

        var (areaY, areaMax) = DesignRenderer.ContentArea(p.Shape);
        double Fit(double wanted) => p.Shape == TileShape.PackPlate ? Math.Round(Math.Min(wanted, style.ShapeWidth * 0.78), 2) : Math.Min(wanted, areaMax);

        if (p.Content.Kind == GenContentKind.None) { ApplyTileLook(d, p, ink, null); return d; }
        var layer = p.Content.Kind switch
        {
            GenContentKind.Glyph => new MakerLayer { Kind = LayerKind.Glyph, Glyph = p.Content.Value, GlyphSet = p.Content.GlyphSet, Size = Fit(0.54) },
            GenContentKind.Monogram => new MakerLayer { Kind = LayerKind.Text, Text = p.Content.Value, Size = Fit(p.Content.Value.Length > 1 ? 0.56 : 0.5), Bold = true },
            _ => new MakerLayer { Kind = LayerKind.Image, Size = Fit(0.62), Name = input.Name },
        };
        if (p.Content.Kind == GenContentKind.ProgramIcon && input.ProgramPng is not null) layer.SetImage(input.ProgramPng);
        layer.Fill = FillSpec.Of(ink);
        layer.Y = areaY;
        d.Layers.Add(layer);

        if (plate) return d; // the pack's plate, mask and frame give the look
        ApplyTileLook(d, p, ink, layer);
        // Program icons keep their colors; neon/light only change the tile around them
        if (p.Content.Kind == GenContentKind.ProgramIcon) layer.Shadow = new ShadowSpec { Kind = ShadowKind.Soft, Opacity = 0.3, Size = 12, Distance = 4 };
        return d;
    }

    private static double Luminance(Color c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    private static void ApplyTileLook(MakerDesign d, GenParts p, Color ink, MakerLayer? layer)
    {
        layer ??= new MakerLayer(); // no content: effects go nowhere
        switch (p.Look)
        {
            case GenLook.Flat:
                d.Background.Kind = FillKind.Solid;
                break;
            case GenLook.Gradient:
                layer.Shadow = new ShadowSpec { Kind = ShadowKind.Soft, Opacity = 0.25, Size = 10, Distance = 4 };
                break;
            case GenLook.LongShadow:
                d.Background.Kind = FillKind.Solid;
                layer.Shadow = new ShadowSpec { Kind = ShadowKind.Long, Opacity = 0.28, Size = 120, Angle = 315 };
                break;
            case GenLook.Glass:
                d.Gloss = true;
                d.Background.Angle = 90;
                d.Border = new StrokeSpec { On = true, Color = "#55FFFFFF", Width = 4 };
                layer.Shadow = new ShadowSpec { Kind = ShadowKind.Soft, Opacity = 0.35, Size = 10, Distance = 3 };
                break;
            case GenLook.Neon:
                d.Background = new FillSpec { Kind = FillKind.Linear, C1 = Color.FromRgb(0x1A, 0x1A, 0x24), C2 = Color.FromRgb(0x0B, 0x0B, 0x12), Angle = 135 };
                d.Border = new StrokeSpec { On = true, C = ink, Width = 5 };
                layer.Shadow = new ShadowSpec { Kind = ShadowKind.Soft, C = ink, Opacity = 1, Size = 22, Distance = 0 };
                break;
            case GenLook.Light:
                d.Background = new FillSpec { Kind = FillKind.Linear, C1 = Color.FromRgb(0xFF, 0xFF, 0xFF), C2 = Mix(Color.FromRgb(0xF1, 0xF2, 0xF6), p.Palette.C1, 0.12), Angle = 135 };
                d.Border = new StrokeSpec { On = true, C = Mix(Color.FromRgb(0xE2, 0xE4, 0xEA), p.Palette.C1, 0.2), Width = 3 };
                break;
        }
    }

    /// <summary>
    /// The same variant for another name (series): shape, look and colors stay, the content follows the name
    /// in the way of the template (symbol -> best symbol for the name, initials -> its initials, program icon -> its icon).
    /// </summary>
    public static GenParts ForName(GenParts template, GenInput input, GenPalette? palette = null)
    {
        var options = ContentOptions(input);
        GenContent? content = template.Content.Kind switch
        {
            GenContentKind.None => template.Content,
            GenContentKind.ProgramIcon when input.ProgramPng is not null => new GenContent(GenContentKind.ProgramIcon),
            GenContentKind.Monogram => options.FirstOrDefault(o => o.Kind == GenContentKind.Monogram),
            _ => null,
        };
        content ??= options.FirstOrDefault(o => o.Kind == GenContentKind.Glyph && !input.Category.Terms.Contains(IconFonts.Get(o.GlyphSet).NameOf(o.Value) ?? ""))
                    ?? options.FirstOrDefault(o => o.Kind == GenContentKind.Monogram)
                    ?? options.FirstOrDefault(o => o.Kind != GenContentKind.None)
                    ?? template.Content;
        return template with { Content = content, Palette = palette ?? template.Palette };
    }

    /// <summary>The program icon in the style of a pack (pack plate, otherwise a tile in the icon's main color) - fills gaps of a pack.</summary>
    public static MakerDesign InPackStyle(GenInput input)
    {
        var palette = PalettesFor(input).First();
        var shape = input.Style.IsNeutral ? TileShape.Squircle : TileShape.PackPlate;
        // Without a usable program icon (e.g. Steam's blank ones): the best symbol for the name instead
        var content = input.ProgramPng is not null ? new GenContent(GenContentKind.ProgramIcon)
            : ContentOptions(input).FirstOrDefault(o => o.Kind is GenContentKind.Glyph or GenContentKind.Monogram) ?? new GenContent(GenContentKind.Monogram, Monogram(input.Name));
        return Compose(new GenParts(content, palette, shape, GenLook.Gradient), input);
    }

    private static IEnumerable<GenPalette> PalettesFor(GenInput input)
    {
        // The program's own color first: it keeps the icon recognizable
        if ((input.ProgramPng ?? input.ColorPng) is { } colorSource && MainColor(colorSource) is { } c)
        {
            yield return new GenPalette(c, Darken(c, 0.35));
            yield return new GenPalette(Lighten(c, 0.15), Shift(c, 30));
        }
        foreach (var p in input.Category.Palettes) yield return p;
    }

    private static IEnumerable<TileShape> ShapesFor(GenInput input)
    {
        // A pack with its own plate or a learned common shape: that shape twice as often (not for folders, drives, file types)
        if (input.Category.UsePackShape && !input.Style.IsNeutral) { yield return TileShape.PackPlate; yield return TileShape.PackPlate; }
        foreach (var s in input.Category.Shapes) yield return s;
    }

    /// <summary>"Visual Studio Code" -> "VC", "Steam" -> "St", "7-Zip" -> "7Z".</summary>
    public static string Monogram(string name)
    {
        var words = Words(name).Where(w => !Noise.Contains(w)).ToList();
        if (words.Count == 0) words = Words(name);
        if (words.Count == 0) return "";
        if (words.Count == 1) return words[0].Length == 1 ? words[0].ToUpperInvariant() : char.ToUpperInvariant(words[0][0]) + words[0][1..2];
        return string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[^1][0]));
    }

    /// <summary>The dominant saturated color of an icon (hue histogram weighted by saturation and opacity); null for gray icons.</summary>
    public static Color? MainColor(byte[] png)
    {
        var img = ImageTools.DecodeThumbnail(png, 48);
        if (img is null) return null;
        var src = new FormatConvertedBitmap(img, PixelFormats.Bgra32, null, 0);
        int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
        var px = new byte[stride * h];
        src.CopyPixels(px, stride, 0);
        var weight = new double[12];
        var sums = new double[12, 3];
        for (int i = 0; i < px.Length; i += 4)
        {
            double a = px[i + 3] / 255.0;
            if (a < 0.5) continue;
            byte b = px[i], g = px[i + 1], r = px[i + 2];
            var (hue, sat, light) = Hsl(Color.FromRgb(r, g, b));
            if (sat < 0.25 || light < 0.12 || light > 0.92) continue;
            int bin = (int)(hue / 30) % 12;
            double wgt = a * sat;
            weight[bin] += wgt; sums[bin, 0] += r * wgt; sums[bin, 1] += g * wgt; sums[bin, 2] += b * wgt;
        }
        int best = Array.IndexOf(weight, weight.Max());
        if (weight[best] < w * h * 0.04) return null;
        var c = Color.FromRgb((byte)(sums[best, 0] / weight[best]), (byte)(sums[best, 1] / weight[best]), (byte)(sums[best, 2] / weight[best]));
        // Very light main colors would make white symbols unreadable: darken a bit
        var (hh, ss, ll) = Hsl(c);
        return ll > 0.62 ? FromHsl(hh, ss, 0.55) : c;
    }

    // ---------- Color helpers ----------

    private static GenPalette P(string a, string b) => new((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b));

    public static Color Mix(Color a, Color b, double t) =>
        Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    public static Color Darken(Color c, double t) => Mix(c, Colors.Black, t);
    public static Color Lighten(Color c, double t) => Mix(c, Colors.White, t);
    private static Color Shift(Color c, double degrees) { var (h, s, l) = Hsl(c); return FromHsl((h + degrees) % 360, s, l); }

    private static (double H, double S, double L) Hsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2, d = max - min;
        if (d < 1e-6) return (0, 0, l);
        double s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        double F(double t)
        {
            t = (t % 1 + 1) % 1;
            return t < 1 / 6.0 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3.0 ? p + (q - p) * (2 / 3.0 - t) * 6 : p;
        }
        return Color.FromRgb((byte)(F(h / 360 + 1 / 3.0) * 255), (byte)(F(h / 360) * 255), (byte)(F(h / 360 - 1 / 3.0) * 255));
    }

    private static List<string> Words(string s)
    {
        string d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
