using BinanceTicker.Core.Models;

namespace BinanceTicker.Services;

public sealed class DesktopPreferencesService(GlobalHotkeyService hotkeys, IStartupRegistration startup)
{
    public Change Prepare(AppSettings settings, bool updateStartup = true)
    {
        var previous = updateStartup ? startup.Read() : null;
        var hotkey = hotkeys.Prepare(settings.Hotkey);
        try
        {
            if (updateStartup) startup.Apply(settings.StartWithWindows);
            return new(hotkey, updateStartup ? startup : null, previous);
        }
        catch (Exception error)
        {
            hotkey.Dispose();
            try { if (updateStartup) startup.Restore(previous); }
            catch (Exception rollback) { throw new AggregateException("登入啟動變更與還原失敗，請檢查 Windows 啟動項後重試。", error, rollback); }
            throw;
        }
    }
    public sealed class Change(GlobalHotkeyService.Change hotkey, IStartupRegistration? startup, string? previous) : IDisposable
    {
        private bool committed, disposed;
        public void Commit() { hotkey.Commit(); committed = true; }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (!committed) startup?.Restore(previous); }
            finally { hotkey.Dispose(); }
        }
    }
}
