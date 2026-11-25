using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace IconWorkshop;

/// <summary>App information for the title and the about section. The version comes from &lt;Version&gt; in the csproj.</summary>
public static class AppInfo
{
    public static string Version { get; } = FormatVersion(Assembly.GetExecutingAssembly().GetName().Version);

    public static string Title => $"{Loc.AppName} {Version}";

    public static string Runtime => RuntimeInformation.FrameworkDescription;

    /// <summary>%LOCALAPPDATA%\Icon-Workshop: settings, backups and (by default) the icon packs.</summary>
    public static string DataFolder { get; private set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Icon-Workshop");

    /// <summary>Demo mode only: a temporary data folder instead of the real one.</summary>
    internal static void UseDataFolder(string folder) => DataFolder = folder;

    /// <summary>Paths as shown in the settings (masked in demo mode).</summary>
    public static string DataFolderDisplay => Services.DemoMode.DisplayPath(DataFolder);
    public static string ProgramFolderDisplay => Services.DemoMode.DisplayPath(ProgramFolder);

    public static string DefaultPacksFolder => Path.Combine(DataFolder, "Packs");

    public static string ProgramFolder => AppContext.BaseDirectory;

    // ---------- Links (empty or "#" = not available yet, shown as a placeholder) ----------
    public const string KofiUrl = "https://ko-fi.com/RNYSVRN";
    public const string KofiLabel = "ko-fi.com/RNYSVRN";
    public const string GitHubUrl = "https://github.com/RNYSVRN/Icon-Workshop";
    public const string GitHubIssuesUrl = GitHubUrl + "/issues/new";

    private static string FormatVersion(Version? v) =>
        v is null ? "?" : v.Build > 0 || v.Revision <= 0 ? $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}" : v.ToString();
}
