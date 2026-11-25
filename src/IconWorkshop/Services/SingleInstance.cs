using System.Security.Principal;

namespace IconWorkshop.Services;

/// <summary>
/// Only one window of the app per user: a second start brings the running window to the front and exits.
/// Uses a named mutex plus an event the first instance listens on.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private readonly bool _owned;
    private RegisteredWaitHandle? _wait;

    public bool IsFirst => _owned;

    public SingleInstance()
    {
        string user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _mutex = new Mutex(true, @"Local\IconWorkshop-" + user, out _owned);
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\IconWorkshop-Activate-" + user);
    }

    /// <summary>First instance: calls <paramref name="onActivate"/> whenever another start asks to be shown.</summary>
    public void Listen(Action onActivate) =>
        _wait = ThreadPool.RegisterWaitForSingleObject(_activate, (_, _) => onActivate(), null, Timeout.Infinite, executeOnlyOnce: false);

    /// <summary>Second instance: asks the running one to come to the front.</summary>
    public void SignalFirst() => _activate.Set();

    public void Dispose()
    {
        _wait?.Unregister(null);
        if (_owned) { try { _mutex.ReleaseMutex(); } catch { } }
        _mutex.Dispose();
        _activate.Dispose();
    }
}
