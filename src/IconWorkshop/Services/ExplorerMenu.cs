using Microsoft.Win32;

namespace IconWorkshop.Services;

/// <summary>
/// Explorer context menu entry "Change icon with Icon Workshop" for shortcuts (.lnk, .url), folders and drives,
/// only for the current user (HKCU\Software\Classes\...\shell). Windows 11 shows such classic entries under
/// "Show more options" (Shift+F10); the new short menu only takes packaged apps.
/// </summary>
public static class ExplorerMenu
{
    /// <summary>Below HKCU; the tests point this to a test key.</summary>
    internal static string Root = @"Software\Classes";
    private const string Verb = "IconWorkshop";
    /// <summary>Shortcuts, internet shortcuts (Steam/Epic games), folders, drives.</summary>
    private static readonly string[] Classes = ["lnkfile", "InternetShortcut", "Directory", "Drive"];

    public static string MenuText => Loc.T("Icon ändern mit Icon Werkstatt", "Change icon with Icon Workshop");

    public static bool IsEnabled => Classes.All(c => Registry.CurrentUser.OpenSubKey($@"{Root}\{c}\shell\{Verb}\command") is { } k && Dispose(k));

    private static bool Dispose(RegistryKey k) { k.Dispose(); return true; }

    public static void SetEnabled(bool on, string exePath)
    {
        foreach (var c in Classes)
        {
            string path = $@"{Root}\{c}\shell\{Verb}";
            if (!on)
            {
                Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                continue;
            }
            using var verb = Registry.CurrentUser.CreateSubKey(path);
            verb.SetValue("", MenuText);
            verb.SetValue("Icon", $"\"{exePath}\",0");
            using var cmd = verb.CreateSubKey("command");
            cmd.SetValue("", $"\"{exePath}\" --open \"%1\"");
        }
    }

    /// <summary>At startup: if the entry exists but points to an old location of the app (moved), point it to this one.</summary>
    public static void RefreshPath(string exePath)
    {
        try
        {
            using var cmd = Registry.CurrentUser.OpenSubKey($@"{Root}\{Classes[0]}\shell\{Verb}\command");
            if (cmd?.GetValue("") is string current && !current.Contains(exePath, StringComparison.OrdinalIgnoreCase)) SetEnabled(true, exePath);
        }
        catch { }
    }
}
