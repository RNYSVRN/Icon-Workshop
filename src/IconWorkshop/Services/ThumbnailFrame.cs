using IconWorkshop.Native;
using Microsoft.Win32;

namespace IconWorkshop.Services;

/// <summary>
/// The gray "photo border" Explorer draws around thumbnails of image files (registry value Treatment = 2 for
/// all images). PNG and ICO files are icons more often than photos, so it can be switched off for them,
/// only for the current user (HKCU overrides HKCR). Switching it on again removes the values, i.e. Windows default.
/// </summary>
public static class ThumbnailFrame
{
    /// <summary>Below HKCU; the tests point this to a test key.</summary>
    internal static string Root = @"Software\Classes\SystemFileAssociations";
    private static readonly string[] Types = [".png", ".ico"];
    private const string Value = "Treatment";

    public static bool IsHidden =>
        Types.All(t => Registry.CurrentUser.OpenSubKey($@"{Root}\{t}")?.GetValue(Value) is int v && v == 0);

    public static void SetHidden(bool hide)
    {
        foreach (var type in Types)
        {
            string path = $@"{Root}\{type}";
            if (hide)
            {
                using var key = Registry.CurrentUser.CreateSubKey(path);
                key.SetValue(Value, 0, RegistryValueKind.DWord);
                continue;
            }
            using (var key = Registry.CurrentUser.OpenSubKey(path, writable: true))
            {
                if (key is null) continue;
                key.DeleteValue(Value, throwOnMissingValue: false);
                if (key.ValueCount > 0 || key.SubKeyCount > 0) continue;
            }
            // Only the key this option created, now empty
            Registry.CurrentUser.DeleteSubKey(path, throwOnMissingSubKey: false);
        }
        ShellNative.NotifyIconsChanged();
    }
}
