namespace OpsisNewsCrawler.Core;

/// <summary>
/// One crawler per user session: the stand-alone program and the Opsis plugin use the same named mutex, so they never run two bars (and
/// two writers of the same settings file) at the same time. Whoever starts first owns it; the other one stays out and says so.
/// </summary>
public sealed class InstanceGuard : IDisposable
{
    /// <summary>The name the installer's AppMutex also waits for ("close the program first").</summary>
    public const string DefaultName = "OpsisNewsCrawler.SingleInstance";

    private Mutex? mutex;

    /// <summary>Takes the mutex; false when another instance (stand-alone or plugin) already owns it. Must be released on the same thread.</summary>
    public bool TryAcquire(string name = DefaultName)
    {
        if (mutex is not null) return true;
        var created = new Mutex(true, name, out var createdNew);
        if (!createdNew) { created.Dispose(); return false; }
        mutex = created;
        return true;
    }

    public bool IsOwner => mutex is not null;

    public void Release()
    {
        if (mutex is null) return;
        try { mutex.ReleaseMutex(); }
        catch (ApplicationException) { }   // released from another thread: the OS frees it when the process ends
        mutex.Dispose();
        mutex = null;
    }

    public void Dispose() => Release();
}
