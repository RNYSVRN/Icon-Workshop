using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace IconWorkshop.Imaging;

/// <summary>One symbol of an icon font: the character plus its name and extra search words.</summary>
public sealed record FontGlyph(string Glyph, string Name, string Terms);

/// <summary>
/// An icon font usable in the icon maker. Besides the Windows symbols (Segoe Fluent Icons), free fonts are
/// embedded whose licenses allow use and redistribution: Material Symbols (Apache 2.0), Tabler Icons (MIT)
/// and Font Awesome Free (fonts SIL OFL 1.1, icons CC BY 4.0).
/// </summary>
public sealed class IconFontSet
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required FontFamily Family { get; init; }
    public FontWeight Weight { get; init; } = FontWeights.Normal;
    /// <summary>Embedded name list ("name hex [search words]"); null = names from <see cref="GlyphNames"/>.</summary>
    public string? NamesResource { get; init; }
    public string? Attribution { get; init; }

    private List<FontGlyph>? _glyphs;
    private Dictionary<string, FontGlyph>? _byGlyph;

    public IReadOnlyList<FontGlyph> Glyphs
    {
        get
        {
            if (_glyphs is null)
            {
                _glyphs = NamesResource is null ? FluentGlyphs() : ReadNames(NamesResource);
                _byGlyph = new Dictionary<string, FontGlyph>();
                foreach (var g in _glyphs) _byGlyph.TryAdd(g.Glyph, g);
            }
            return _glyphs;
        }
    }

    public string? NameOf(string glyph)
    {
        _ = Glyphs;
        return _byGlyph!.TryGetValue(glyph, out var g) && g.Name.Length > 0 ? g.Name : null;
    }

    public Typeface Typeface => new(Family, FontStyles.Normal, Weight, FontStretches.Normal);

    private IDictionary<int, ushort>? _charMap;

    /// <summary>Whether the font file really contains the symbol (name lists can be ahead of the font).</summary>
    public bool Has(string glyph)
    {
        if (string.IsNullOrEmpty(glyph)) return false;
        if (_charMap is null)
            _charMap = Typeface.TryGetGlyphTypeface(out var gt) ? gt.CharacterToGlyphMap : new Dictionary<int, ushort>();
        return _charMap.Count == 0 || _charMap.ContainsKey(char.ConvertToUtf32(glyph, 0));
    }

    /// <summary>Search over names and search words; German words are translated (Haus -> home ...).</summary>
    public IReadOnlyList<string> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Glyphs.Select(g => g.Glyph).ToList();
        var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                         .Select(w => (Word: w, English: GlyphNames.Translate(w))).ToList();
        return Glyphs.Where(g =>
        {
            string hay = (g.Name + " " + g.Terms).ToLowerInvariant();
            return words.All(w => hay.Contains(w.Word) || (w.English is not null && hay.Contains(w.English)));
        })
        // Best matches first: exact name, name contains the word, exact search word, rest; shorter names first
        .OrderBy(g => Rank(g, words))
        .ThenBy(g => g.Name.Length)
        .Select(g => g.Glyph).ToList();
    }

    private static int Rank(FontGlyph g, List<(string Word, string? English)> words)
    {
        bool Is(string s, string? w) => w is not null && s.Equals(w, StringComparison.OrdinalIgnoreCase);
        bool Has(string s, string? w) => w is not null && s.Contains(w, StringComparison.OrdinalIgnoreCase);
        if (words.Any(w => Is(g.Name, w.Word) || Is(g.Name, w.English))) return 0;
        if (words.Any(w => Has(g.Name, w.Word) || Has(g.Name, w.English))) return 1;
        var terms = g.Terms.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => terms.Any(t => Is(t, w.Word) || Is(t, w.English)))) return 2;
        return 3;
    }

    private static List<FontGlyph> ReadNames(string resource)
    {
        var list = new List<FontGlyph>();
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
        if (s is null) return list;
        using var reader = new StreamReader(s);
        var seen = new HashSet<int>();
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out int code)) continue;
            if (code is < 0x20 or > 0x10FFFF or (>= 0xD800 and <= 0xDFFF) || !seen.Add(code)) continue; // aliases share a code point
            list.Add(new FontGlyph(char.ConvertFromUtf32(code), parts[0].Replace('_', ' ').Replace('-', ' '), parts.Length > 2 ? parts[2] : ""));
        }
        return list;
    }

    private static List<FontGlyph> FluentGlyphs()
    {
        var result = new List<FontGlyph>();
        foreach (var name in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            var tf = new Typeface(new FontFamily(name), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!tf.TryGetGlyphTypeface(out var glyphs)) continue;
            foreach (var code in glyphs.CharacterToGlyphMap.Keys.Where(k => k >= 0xE700 && k <= 0xF8FF).OrderBy(k => k))
            {
                string g = char.ConvertFromUtf32(code);
                result.Add(new FontGlyph(g, GlyphNames.NameOf(g) ?? "", GlyphNames.TermsOf(g)));
            }
            if (result.Count > 0) break;
        }
        // Named symbols first, so searching and browsing start with the useful ones
        return result.OrderBy(g => g.Name.Length == 0 ? 1 : 0).ToList();
    }
}

public static class IconFonts
{
    private const string FontBase = "pack://application:,,,/IconWorkshop;component/Assets/Fonts/";

    /// <summary>Font from a specific embedded file, so faces sharing a family name (FA Solid/Regular) are not mixed up.</summary>
    private static FontFamily Embedded(string file, string family) => new(new Uri(FontBase), "./" + file + "#" + family);

    public static readonly IReadOnlyList<IconFontSet> All =
    [
        new() { Id = "fluent", Name = "Windows (Segoe Fluent Icons)", Family = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets") },
        new() { Id = "material", Name = "Material Symbols", Family = Embedded("MaterialSymbolsRounded.ttf", "Material Symbols Rounded"), NamesResource = "Fonts.material-names.txt" },
        new() { Id = "tabler", Name = "Tabler Icons", Family = Embedded("tabler-icons.ttf", "tabler-icons"), NamesResource = "Fonts.tabler-names.txt" },
        new() { Id = "fa-solid", Name = "Font Awesome (Solid)", Family = Embedded("fa-solid-900.ttf", "Font Awesome 6 Free Solid"), Weight = FontWeights.Black, NamesResource = "Fonts.fa-solid-names.txt",
                Attribution = "Font Awesome Free by Fonticons, Inc. (fontawesome.com), CC BY 4.0 / SIL OFL 1.1" },
        new() { Id = "fa-regular", Name = "Font Awesome (Regular)", Family = Embedded("fa-regular-400.ttf", "Font Awesome 6 Free"), Weight = FontWeights.Normal, NamesResource = "Fonts.fa-regular-names.txt",
                Attribution = "Font Awesome Free by Fonticons, Inc. (fontawesome.com), CC BY 4.0 / SIL OFL 1.1" },
        new() { Id = "fa-brands", Name = "Font Awesome (Marken)", Family = Embedded("fa-brands-400.ttf", "Font Awesome 6 Brands"), NamesResource = "Fonts.fa-brands-names.txt",
                Attribution = "Font Awesome Free by Fonticons, Inc. (fontawesome.com), CC BY 4.0 / SIL OFL 1.1. Marken-Logos sind Markenzeichen ihrer Inhaber." },
    ];

    public static IconFontSet Get(string? id) => All.FirstOrDefault(s => s.Id == id) ?? All[0];

    /// <summary>License text of an embedded font (shown on the licenses page).</summary>
    public static string? License(string file)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Fonts." + file);
        if (s is null) return null;
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
