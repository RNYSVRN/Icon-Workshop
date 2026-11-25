using System.Diagnostics;
using System.Windows;
using IconWorkshop.Services;
using IconWorkshop.Views;

namespace IconWorkshop;

public partial class App : Application
{
    public static AppSettings Settings { get; private set; } = new();

    /// <summary>Writes unexpected errors to %LOCALAPPDATA%\Icon-Workshop\error.log.</summary>
    public static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataFolder);
            File.AppendAllText(Path.Combine(AppInfo.DataFolder, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {AppInfo.Version}\n{ex}\n\n");
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Elevated helper (started by the app itself with one UAC prompt): change shortcuts of all users, no window
        if (e.Args.Length == 2 && e.Args[0] == "--elevated")
        {
            Shutdown(Assignments.AssignmentStore.RunElevatedRequest(e.Args[1]));
            return;
        }

        // Silent check at sign-in: re-apply icons that program updates replaced, then exit (no window)
        if (e.Args.Contains("--reapply"))
        {
            _ = ReapplySilentlyAsync();
            return;
        }

        // Uninstaller: remove what the app registered in Windows (context menu, sign-in check), no window.
        // Assigned icons stay; their ICO files remain in the data folder, so nothing turns blank.
        if (e.Args.Contains("--uninstall"))
        {
            try { ExplorerMenu.SetEnabled(false, ""); } catch (Exception ex) { LogError(ex); }
            try { SignInCheck.Disable(); } catch (Exception ex) { LogError(ex); }
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            Controls.FluentDialog.Show(MainWindow, Loc.T("Unerwarteter Fehler", "Unexpected error"), args.Exception.Message, Loc.Ok,
                                       glyph: Controls.FluentDialog.GlyphError);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) => { LogError(args.Exception); args.SetObserved(); };

        Settings = AppSettings.Load();
        string? lang = null;
        string? screenshotDir = null;
        string? perfFile = null;
        bool preview = false;
        bool demo = false;
        string? openPath = null;
        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--lang" && i + 1 < e.Args.Length) lang = e.Args[++i];
            else if (e.Args[i] == "--screenshots" && i + 1 < e.Args.Length) screenshotDir = e.Args[++i];
            else if (e.Args[i] == "--perf" && i + 1 < e.Args.Length) perfFile = e.Args[++i];
            else if (e.Args[i] == "--open" && i + 1 < e.Args.Length) openPath = CleanOpenPath(e.Args[++i]); // Explorer context menu
            else if (e.Args[i] == "--demo") demo = true; // screenshots: generated packs, masked paths, temporary data folder
            else if (e.Args[i] == "--preview") preview = true; // dev: window only, settings read-only, no single instance
            else if (e.Args[i] == "--theme" && i + 1 < e.Args.Length)
                ThemeMode = e.Args[++i] == "dark" ? ThemeMode.Dark : ThemeMode.Light; // screenshots only
        }
        if (screenshotDir is not null || perfFile is not null || preview) AppSettings.ReadOnly = true;
        Loc.Init(lang ?? Settings.Language);
        bool devMode = screenshotDir is not null || perfFile is not null || preview;
        if (demo && devMode)
        {
            DemoMode.Start();
            Settings = new AppSettings { Language = lang ?? Settings.Language };
        }

        // Only one window: a second start brings the running one to the front
        if (!devMode)
        {
            _instance = new SingleInstance();
            if (!_instance.IsFirst)
            {
                // Explorer context menu while the app runs: hand the path to the running window
                if (openPath is not null) { try { Directory.CreateDirectory(AppInfo.DataFolder); File.WriteAllText(OpenRequestFile, openPath); } catch (Exception ex) { LogError(ex); } }
                _instance.SignalFirst();
                _instance.Dispose();
                _instance = null;
                Shutdown();
                return;
            }
            SignInCheck.RefreshPath();
            // Fixed name so the installer can see the app is running (the single-instance mutex is per user)
            _installerMutex = new Mutex(false, "IconWorkshopRunning");
            TakeOpenRequest(); // a leftover request from a start that never reached a window
        }

        var window = new MainWindow();
        MainWindow = window;
        if (!devMode) window.RestorePlacement(Settings);
        window.Show();
        _instance?.Listen(() => Dispatcher.BeginInvoke(() =>
        {
            window.BringToFront();
            if (TakeOpenRequest() is { } requested) window.OpenPath(requested);
        }));
        if (!devMode) Services.ExplorerMenu.RefreshPath(Environment.ProcessPath ?? "");
        if (openPath is not null) window.OpenPath(openPath);

        if (perfFile is not null) _ = window.MeasurePerformanceAsync(perfFile);
        if (screenshotDir is not null)
            window.CaptureScreenshotsAsync(screenshotDir).ContinueWith(t => { if (t.Exception is { } ex) LogError(ex); }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private static string OpenRequestFile => Path.Combine(AppInfo.DataFolder, "open-request.txt");
    private Mutex? _installerMutex;

    /// <summary>
    /// Explorer passes a drive as "C:\" in quotes; the backslash escapes the closing quote, so the argument
    /// arrives as C:" - repair that (and stray quotes in general).
    /// </summary>
    internal static string CleanOpenPath(string arg)
    {
        string p = arg.Trim().Trim('"');
        if (p.Length == 2 && char.IsLetter(p[0]) && p[1] == ':') p += "\\";
        return p;
    }

    /// <summary>A path a second start (Explorer context menu) left for this window, read once.</summary>
    private static string? TakeOpenRequest()
    {
        try
        {
            if (!File.Exists(OpenRequestFile)) return null;
            string path = File.ReadAllText(OpenRequestFile).Trim();
            File.Delete(OpenRequestFile);
            return path.Length > 0 ? path : null;
        }
        catch (Exception ex) { LogError(ex); return null; }
    }

    private async Task ReapplySilentlyAsync()
    {
        int code = 0;
        try
        {
            Settings = AppSettings.Load();
            Loc.Init(Settings.Language);
            var packs = new Packs.PackStore(Settings.EffectivePacksFolder);
            packs.Load();
            var store = new Assignments.AssignmentStore(AppInfo.DataFolder);
            var r = await store.ReapplyAsync(packs, allowElevation: false);
            Directory.CreateDirectory(AppInfo.DataFolder);
            File.WriteAllText(SignInCheck.LogFile,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  geprüft {r.Checked}, erneut angewendet {r.Repaired}, nicht möglich {r.Failed.Count}\n" +
                string.Concat(r.Failed.Select(f => $"  {f.Name}: {f.Error}\n")));
        }
        catch (Exception ex) { LogError(ex); code = 1; }
        Shutdown(code);
    }

    private SingleInstance? _instance;

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Restarts the app, e.g. after a language change.</summary>
    public void Restart()
    {
        // Release the single-instance lock first, otherwise the new start would only activate this one
        _instance?.Dispose();
        _instance = null;
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory });
        }
        catch { /* if it fails, the user starts the app again */ }
        Shutdown();
    }
}
