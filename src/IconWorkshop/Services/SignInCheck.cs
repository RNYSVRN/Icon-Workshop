using Microsoft.Win32;

namespace IconWorkshop.Services;

/// <summary>
/// Optional silent check at sign-in: starts "IconWorkshop.exe --reapply" once, which re-applies
/// icons that program updates replaced, and exits again. Registered per user (HKCU\...\Run), no admin rights.
/// </summary>
public static class SignInCheck
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IconWorkshopReapply";

    private static string Command => $"\"{Environment.ProcessPath}\" --reapply";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue(ValueName) is string s && s.Length > 0;
            }
            catch { return false; }
        }
    }

    public static void Enable()
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        k.SetValue(ValueName, Command, RegistryValueKind.String);
    }

    public static void Disable()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        k?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Keeps the entry pointing at the current EXE (after the app was moved or updated).</summary>
    public static void RefreshPath()
    {
        try { if (IsEnabled) Enable(); } catch { }
    }

    public static string LogFile => Path.Combine(AppInfo.DataFolder, "reapply.log");
}
