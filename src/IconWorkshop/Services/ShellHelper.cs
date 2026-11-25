using System.Diagnostics;
using System.IO;
using System.Windows;

namespace IconWorkshop.Services;

/// <summary>Small helpers for Explorer, clipboard and links.</summary>
public static class ShellHelper
{
    public static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }

    public static void RevealFile(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true }); }
        catch { }
    }

    public static void Copy(string text)
    {
        try { Clipboard.SetText(text); } catch { /* clipboard briefly busy, not critical */ }
    }

    public static bool IsRealUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

    public static void OpenUrl(string url)
    {
        if (!IsRealUrl(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
