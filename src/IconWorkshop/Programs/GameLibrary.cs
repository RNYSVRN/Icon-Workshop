using IconWorkshop.Native;
using Microsoft.Win32;

namespace IconWorkshop.Programs;

public enum GameLauncher { Steam, Epic }

/// <summary>A game shortcut: which launcher and the game's ID there (Steam app ID, Epic app name).</summary>
public sealed record GameInfo(GameLauncher Launcher, string Id)
{
    public string Label => Launcher == GameLauncher.Steam ? Loc.T("Steam-Spiel", "Steam game") : Loc.T("Epic-Spiel", "Epic game");
}

/// <summary>
/// Recognizes game shortcuts of Steam and the Epic Games Launcher and finds Steam's own small game icon.
/// Steam creates desktop shortcuts whose icon file (steam\games\*.ico) is often missing - the "blank icon" problem.
/// </summary>
public static class GameLibrary
{
    /// <summary>Steam: steam://rungameid/ID (internet shortcut) or steam.exe -applaunch ID / steam://rungameid/ID as arguments;
    /// Epic: com.epicgames.launcher://apps/...:APPNAME?action=launch.</summary>
    public static GameInfo? Detect(LinkInfo info)
    {
        string text = (info.Url ?? "") + " " + (info.Target ?? "") + " " + (info.Arguments ?? "");
        int i = text.IndexOf("steam://rungameid/", StringComparison.OrdinalIgnoreCase);
        if (i >= 0) return Steam(text[(i + 18)..]);
        i = text.IndexOf("-applaunch ", StringComparison.OrdinalIgnoreCase);
        if (i >= 0 && (info.Target ?? "").EndsWith("steam.exe", StringComparison.OrdinalIgnoreCase)) return Steam(text[(i + 11)..]);
        i = text.IndexOf("com.epicgames.launcher://apps/", StringComparison.OrdinalIgnoreCase);
        if (i >= 0)
        {
            string rest = Uri.UnescapeDataString(text[(i + 30)..].Split('?', ' ')[0]);
            string app = rest.Split(':').Last();
            return app.Length > 0 ? new GameInfo(GameLauncher.Epic, app) : null;
        }
        return null;
    }

    private static GameInfo? Steam(string rest)
    {
        string id = new(rest.TakeWhile(char.IsDigit).ToArray());
        return id.Length > 0 ? new GameInfo(GameLauncher.Steam, id) : null;
    }

    /// <summary>The shortcut points to an icon file that doesn't exist (Windows then shows a blank icon).</summary>
    public static bool IconFileMissing(LinkInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.IconPath)) return info.Url is not null && Detect(info) is not null;
        string path = Environment.ExpandEnvironmentVariables(info.IconPath.Trim('"'));
        return !path.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !File.Exists(path);
    }

    /// <summary>Steam installation folder (registry), null if Steam isn't installed.</summary>
    public static string? SteamFolder
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                return key?.GetValue("SteamPath") is string p && Directory.Exists(p) ? Path.GetFullPath(p) : null;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// Steam's own small icon of a game from its library cache: newer clients keep it as a short-named hash .jpg in
    /// appcache\librarycache\ID\, older ones as appcache\librarycache\ID_icon.jpg.
    /// </summary>
    public static string? SteamClientIcon(string appId, string? steamFolder = null)
    {
        steamFolder ??= SteamFolder;
        if (steamFolder is null) return null;
        string cache = Path.Combine(steamFolder, "appcache", "librarycache");
        string old = Path.Combine(cache, appId + "_icon.jpg");
        if (File.Exists(old)) return old;
        string folder = Path.Combine(cache, appId);
        if (!Directory.Exists(folder)) return null;
        try
        {
            return Directory.EnumerateFiles(folder, "*.jpg", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileNameWithoutExtension(f).Length == 40 && new FileInfo(f).Length < 16_000)
                .OrderByDescending(f => new FileInfo(f).Length)
                .FirstOrDefault();
        }
        catch { return null; }
    }
}
