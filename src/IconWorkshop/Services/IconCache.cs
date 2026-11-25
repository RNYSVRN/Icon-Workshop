using System.Diagnostics;
using IconWorkshop.Native;

namespace IconWorkshop.Services;

/// <summary>Getting Windows to show new icons: a gentle refresh, or clearing the icon and thumbnail cache.</summary>
public static class IconCache
{
    /// <summary>Asks Explorer to reload icons without restarting anything.</summary>
    public static void Refresh()
    {
        ShellNative.NotifyIconsChanged();
        try
        {
            using var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "ie4uinit.exe"), "-show")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            p?.WaitForExit(10000);
        }
        catch { /* not available: the notification alone */ }
    }

    /// <summary>
    /// Ends Explorer of the current session, deletes the icon and thumbnail cache files of this user and
    /// starts Explorer again. The taskbar and open Explorer windows disappear for a few seconds.
    /// Returns the number of deleted cache files.
    /// </summary>
    public static async Task<int> ClearAsync()
    {
        int session = Process.GetCurrentProcess().SessionId;
        var explorers = Process.GetProcessesByName("explorer").Where(p => p.SessionId == session).ToList();
        foreach (var p in explorers)
        {
            try { p.Kill(); } catch { }
        }
        foreach (var p in explorers)
        {
            try { await p.WaitForExitAsync(new CancellationTokenSource(5000).Token); } catch { }
        }

        int deleted = 0;
        await Task.Run(async () =>
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var files = new List<string> { Path.Combine(local, "IconCache.db") };
            string explorerFolder = Path.Combine(local, @"Microsoft\Windows\Explorer");
            if (Directory.Exists(explorerFolder))
            {
                files.AddRange(Directory.GetFiles(explorerFolder, "iconcache_*.db"));
                files.AddRange(Directory.GetFiles(explorerFolder, "thumbcache_*.db"));
            }
            // Files can stay locked for a moment after Explorer ended: retry briefly
            for (int attempt = 0; attempt < 3 && files.Count > 0; attempt++)
            {
                foreach (var f in files.ToList())
                {
                    try
                    {
                        if (File.Exists(f)) { File.Delete(f); deleted++; }
                        files.Remove(f);
                    }
                    catch { /* still in use */ }
                }
                if (files.Count > 0) await Task.Delay(500);
            }
        });

        // Explorer is started again in any case
        try { Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = true }); }
        catch { }
        return deleted;
    }
}
