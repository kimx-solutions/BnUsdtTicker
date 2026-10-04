using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Services;

public interface IHotkeyPlatform : IDisposable
{
    bool Register(int id, uint modifiers, uint key, Action callback);
    void Unregister(int id);
}

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly IHotkeyPlatform platform;
    private readonly Action toggle;
    private readonly HashSet<int> registrations = [];
    private int activeId, nextId = 0x2000;
    private HotkeyGesture? activeGesture;
    private bool disposed;
    public GlobalHotkeyService(IHotkeyPlatform platform, Action toggle) { this.platform = platform; this.toggle = toggle; }
    public Change Prepare(HotkeySettings settings)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        HotkeyGesture? gesture = settings.Enabled ? HotkeyGesture.Parse(settings.Gesture) : null;
        if (gesture == activeGesture) return new(this, activeId, gesture, true);
        var id = 0;
        if (gesture is { } value)
        {
            id = ++nextId;
            var callbackId = id;
            if (!platform.Register(id, value.Modifiers | 0x4000, value.Key, () => { if (!disposed && activeId == callbackId) toggle(); }))
                throw new InvalidOperationException($"快捷鍵 {value} 無法註冊，可能已被其他程式使用。請修改組合或停用快捷鍵。");
            registrations.Add(id);
        }
        return new(this, id, gesture, false);
    }
    private void Release(int id)
    {
        if (registrations.Remove(id)) platform.Unregister(id);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var id in registrations.ToArray()) Release(id);
        platform.Dispose();
    }
    public sealed class Change : IDisposable
    {
        private readonly GlobalHotkeyService owner;
        private readonly int id;
        private readonly HotkeyGesture? gesture;
        private readonly bool reused;
        private bool finished;
        internal Change(GlobalHotkeyService owner, int id, HotkeyGesture? gesture, bool reused)
        { this.owner = owner; this.id = id; this.gesture = gesture; this.reused = reused; }
        public void Commit()
        {
            ObjectDisposedException.ThrowIf(owner.disposed, owner);
            if (finished) throw new InvalidOperationException("快捷鍵變更已結束。");
            if (!reused) { owner.Release(owner.activeId); owner.activeId = id; owner.activeGesture = gesture; }
            finished = true;
        }
        public void Dispose() { if (!finished && !reused) owner.Release(id); finished = true; }
    }
}

public sealed class WindowsHotkeyPlatform : IHotkeyPlatform
{
    private readonly HwndSource source;
    private readonly Dictionary<int, Action> callbacks = [];
    public WindowsHotkeyPlatform(Window window)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("無法建立快捷鍵訊息視窗。");
        source.AddHook(OnMessage);
    }
    public bool Register(int id, uint modifiers, uint key, Action callback)
    {
        if (!RegisterHotKey(source.Handle, id, modifiers, key)) return false;
        callbacks.Add(id, callback); return true;
    }
    public void Unregister(int id) { UnregisterHotKey(source.Handle, id); callbacks.Remove(id); }
    private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && callbacks.TryGetValue(wParam.ToInt32(), out var callback))
        { handled = true; callback(); }
        return IntPtr.Zero;
    }
    public void Dispose() { foreach (var id in callbacks.Keys.ToArray()) Unregister(id); source.RemoveHook(OnMessage); }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
