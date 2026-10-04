namespace BinanceTicker.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex mutex;
    private bool disposed;
    public bool IsFirstInstance { get; }
    public SingleInstanceService(string name = @"Local\BinanceTicker.Desktop.Instance")
    {
        mutex = new Mutex(true, name, out var created);
        IsFirstInstance = created;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (IsFirstInstance) mutex.ReleaseMutex();
        mutex.Dispose();
    }
}
