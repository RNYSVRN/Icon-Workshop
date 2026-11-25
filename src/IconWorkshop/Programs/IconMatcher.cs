using System.Globalization;
using System.Text;
using IconWorkshop.Packs;

namespace IconWorkshop.Programs;

/// <summary>A suggested icon for a program, with a score (100 = exact name match).</summary>
public sealed record IconSuggestion(ProgramEntry Program, IconPack Pack, PackIcon Icon, int Score)
{
    /// <summary>Confident enough to be pre-selected.</summary>
    public bool IsConfident => Score >= 85;
}

/// <summary>
/// Finds the best icon of a pack for a Windows program by comparing names: the program name, the EXE
/// name and the app names hidden in the Android package names of the pack (com.spotify.music -> spotify).
/// Plain icons win over variants (_alt, _old, _dark ...).
/// </summary>
public sealed class IconMatcher
{
    private readonly IconPack _pack;
    private readonly Dictionary<string, PackIcon> _byKey = new(StringComparer.Ordinal);   // normalized name -> icon
    private readonly Dictionary<string, PackIcon> _byPackageToken = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PackIcon> _byCompact = new(StringComparer.Ordinal);       // name without "_" -> icon

    // Words that say nothing about the app itself
    private static readonly HashSet<string> Noise = new(StringComparer.Ordinal)
    {
        "microsoft", "google", "adobe", "the", "for", "and", "app", "apps", "desktop", "windows", "x64", "x86", "64", "32",
        "bit", "pro", "free", "edition", "launcher", "client", "beta", "preview", "insiders", "community", "studio",
    };

    // General words: never a match on their own (they lead to random apps that happen to contain them)
    private static readonly HashSet<string> Generic = new(StringComparer.Ordinal)
    {
        "manager", "tool", "tools", "system", "configuration", "information", "player", "reader", "settings", "management",
        "monitor", "viewer", "editor", "recorder", "data", "sources", "source", "connection", "remote", "services", "service",
        "security", "media", "task", "print", "disk", "memory", "diagnostics", "screen", "keyboard", "compiler", "example",
        "scripts", "faq", "control", "panel", "event", "component", "components", "computer", "command", "prompt", "registry",
        "performance", "resource", "steps", "run", "code", "setup", "help", "documentation", "music", "video", "photo",
        "photos", "mail", "browser", "file", "files", "files", "cleanup", "recovery", "drive", "options", "scheduler",
        "explorer", "notes", "scanner", "cloud", "update", "updater", "uninstall", "web", "online", "center", "hub",
    };
    private static readonly string[] Variants = ["_alt", "_old", "_dark", "_light", "_new", "_black", "_white", "_classic", "_minimal", "_color", "_mono"];

    public IconMatcher(IconPack pack)
    {
        _pack = pack;
        foreach (var icon in pack.Icons)
        {
            string key = Normalize(icon.Name);
            // Keep the plain icon for a key ("chrome" over "chrome_2")
            if (!_byKey.TryGetValue(key, out var existing) || VariantPenalty(icon.Name) < VariantPenalty(existing.Name))
                _byKey[key] = icon;
            string compact = key.Replace("_", "");
            if (!_byCompact.TryGetValue(compact, out var c) || VariantPenalty(icon.Name) < VariantPenalty(c.Name))
                _byCompact[compact] = icon;
        }
        // Android package names: "com.valvesoftware.android.steam.community" -> tokens with the mapped icon
        var byName = pack.Icons.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var (component, drawable) in pack.Components)
        {
            if (!byName.TryGetValue(drawable, out var icon)) continue;
            string package = component.Split('/')[0];
            int brace = package.IndexOf('{');
            if (brace >= 0) package = package[(brace + 1)..];
            foreach (var token in package.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.Length < 4 || token is "android" or "apps" or "app" or "mobile" or "client" or "activity") continue;
                string t = Normalize(token);
                if (!_byPackageToken.TryGetValue(t, out var e) || VariantPenalty(icon.Name) < VariantPenalty(e.Name)) _byPackageToken[t] = icon;
            }
        }
    }

    public IconSuggestion? Suggest(ProgramEntry program)
    {
        var candidates = new List<(PackIcon Icon, int Score)>();
        string name = Normalize(program.Name);
        var words = Words(program.Name);
        string withoutNoise = string.Join("_", words.Where(w => !Noise.Contains(w)));
        string exe = program.ExePath is { } e ? Normalize(Path.GetFileNameWithoutExtension(e)) : "";

        void Try(string key, int score)
        {
            if (key.Length < 2) return;
            if (_byKey.TryGetValue(key, out var icon)) candidates.Add((icon, score - VariantPenalty(icon.Name)));
        }

        bool Distinctive(string key) => key.Length >= 4 && !Generic.Contains(key) && !Noise.Contains(key) && !key.All(char.IsDigit);

        // Indirect matches (package names, compact names) to an icon whose name has its own distinctive word
        // ("Premiere Pro" -> adobe_premiere_rush via com.adobe.premiere.rush) are another product: never "confident".
        var known = new HashSet<string>(words.Concat(Words(exe)), StringComparer.Ordinal);
        string compactName = name.Replace("_", "") + exe.Replace("_", "");
        int OtherProduct(PackIcon icon) =>
            Words(icon.Name).Any(w => w.Length >= 3 && !known.Contains(w) && !compactName.Contains(w) && !Noise.Contains(w)
                                      && !Generic.Contains(w) && !w.All(char.IsDigit) && !Variants.Contains("_" + w)) ? 16 : 0;

        Try(name, 100);                                    // "steam" == steam
        Try(name.Replace("_", ""), 98);                    // "Wallpaper Engine" -> wallpaperengine
        if (name.Replace("_", "").Length >= 5 && _byCompact.TryGetValue(name.Replace("_", ""), out var compactIcon))
            candidates.Add((compactIcon, 97 - VariantPenalty(compactIcon.Name) - OtherProduct(compactIcon)));   // "OneDrive" -> one_drive
        if (withoutNoise.Length > 0 && !Generic.Contains(withoutNoise)) Try(withoutNoise, 95);   // "Google Chrome" -> chrome
        if (Distinctive(exe)) Try(exe, 90);                // Spotify.exe -> spotify
        if (words.Count >= 2)
        {
            // Initials plus last word: "Visual Studio Code" -> vs_code / vscode
            string initials = string.Concat(words.Take(words.Count - 1).Select(w => w[0]));
            Try(initials + "_" + words[^1], 88);
            Try(initials + words[^1], 88);
        }
        // Android package names of the pack: "com.spotify.music" knows "spotify"
        if (Distinctive(withoutNoise) && _byPackageToken.TryGetValue(withoutNoise, out var viaPackage))
            candidates.Add((viaPackage, 85 - VariantPenalty(viaPackage.Name) - OtherProduct(viaPackage)));
        if (Distinctive(exe) && _byPackageToken.TryGetValue(exe, out var viaExe))
            candidates.Add((viaExe, 80 - VariantPenalty(viaExe.Name) - OtherProduct(viaExe)));

        // Single distinctive words ("Adobe Photoshop 2025" -> photoshop); never general words
        foreach (var w in words.Where(w => w.Length >= 5 && Distinctive(w)))
            Try(w, 72);

        if (candidates.Count == 0) return null;
        var best = candidates.OrderByDescending(c => c.Score).First();
        return best.Score >= 70 ? new IconSuggestion(program, _pack, best.Icon, best.Score) : null;
    }

    private static int VariantPenalty(string iconName)
    {
        string n = iconName.ToLowerInvariant();
        int penalty = Variants.Any(v => n.Contains(v)) ? 8 : 0;
        if (n.Length > 0 && char.IsDigit(n[^1])) penalty += 4;
        return penalty;
    }

    /// <summary>"Google Chrome (64-bit)" -> "google_chrome_64_bit"; umlauts and accents are reduced to base letters.</summary>
    public static string Normalize(string s) => string.Join("_", Words(s));

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
