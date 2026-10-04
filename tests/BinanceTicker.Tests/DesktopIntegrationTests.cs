using BinanceTicker.Core.Models;
using BinanceTicker.Services;

namespace BinanceTicker.Tests;

public sealed class DesktopIntegrationTests
{
    [Fact]
    public void CancelledPreferencesRestoreStartupAndHotkey()
    {
        var platform = new HotkeyPlatform();
        using var hotkeys = new GlobalHotkeyService(platform, () => { });
        var startup = new StartupRegistration();
        var preferences = new DesktopPreferencesService(hotkeys, startup);
        using (preferences.Prepare(new() { StartWithWindows = true, Hotkey = new() { Enabled = true } }))
        {
            Assert.Equal("current.exe", startup.Command);
            Assert.Single(platform.Callbacks);
        }
        Assert.Null(startup.Command); Assert.Empty(platform.Callbacks);
        using (var change = preferences.Prepare(new() { StartWithWindows = true, Hotkey = new() { Enabled = true } })) change.Commit();
        Assert.Equal("current.exe", startup.Command); Assert.Single(platform.Callbacks);
        startup.Fail = true;
        Assert.Throws<IOException>(() => preferences.Prepare(new() { Hotkey = new() { Enabled = true, Gesture = "Ctrl+Alt+Y" } }));
        Assert.Single(platform.Callbacks);
        Assert.Equal("current.exe", startup.Command);
    }

    private sealed class StartupRegistration : IStartupRegistration
    {
        public string? Command;
        public bool Fail;
        public string? Read() => Command;
        public void Restore(string? command) => Command = command;
        public void Apply(bool enabled) { if (Fail) throw new IOException("denied"); Command = enabled ? "current.exe" : null; }
    }

    [Fact]
    public void HotkeyConflictAndCancelledSaveKeepPreviousRegistration()
    {
        var platform = new HotkeyPlatform();
        var toggles = 0;
        using var service = new GlobalHotkeyService(platform, () => toggles++);
        using (var initial = service.Prepare(new() { Enabled = true })) initial.Commit();
        platform.Invoke(); Assert.Equal(1, toggles);
        platform.Fail = true;
        Assert.Throws<InvalidOperationException>(() => service.Prepare(new() { Enabled = true, Gesture = "Ctrl+Alt+Y" }));
        platform.Invoke(); Assert.Equal(2, toggles);
        platform.Fail = false;
        using (service.Prepare(new() { Enabled = true, Gesture = "Ctrl+Alt+Y" })) { }
        Assert.Single(platform.Callbacks);
        using (var disable = service.Prepare(new())) disable.Commit();
        Assert.Empty(platform.Callbacks);
    }

    [Fact]
    public void DisposalReleasesHotkeyAndPendingCandidate()
    {
        var platform = new HotkeyPlatform();
        var service = new GlobalHotkeyService(platform, () => { });
        using (var initial = service.Prepare(new() { Enabled = true })) initial.Commit();
        using (var pending = service.Prepare(new() { Enabled = true, Gesture = "Alt+F9" }))
        {
            service.Dispose();
            Assert.Empty(platform.Callbacks);
        }
    }

    [Fact]
    public void SingleInstanceRejectsSecondAndAllowsRestartAfterDispose()
    {
        var name = "Local\\BinanceTicker-test-" + Guid.NewGuid();
        using (var first = new SingleInstanceService(name))
        {
            Assert.True(first.IsFirstInstance);
            using var second = new SingleInstanceService(name);
            Assert.False(second.IsFirstInstance);
        }
        using var restart = new SingleInstanceService(name);
        Assert.True(restart.IsFirstInstance);
    }

    [Fact]
    public void StartupValueCanBeWrittenAndRemovedWithoutTouchingOtherValues()
    {
        var registration = new WindowsStartupService("BinanceTicker-test-" + Guid.NewGuid());
        try
        {
            registration.Restore("\"C:\\My App\\BinanceTicker.exe\"");
            Assert.Equal("\"C:\\My App\\BinanceTicker.exe\"", registration.Read());
            registration.Apply(false);
            Assert.Null(registration.Read());
        }
        finally { registration.Restore(null); }
    }

    private sealed class HotkeyPlatform : IHotkeyPlatform
    {
        public readonly Dictionary<int, Action> Callbacks = [];
        public bool Fail;
        public bool Register(int id, uint modifiers, uint key, Action callback)
        { if (Fail) return false; Callbacks.Add(id, callback); return true; }
        public void Unregister(int id) => Callbacks.Remove(id);
        public void Invoke() { foreach (var callback in Callbacks.Values.ToArray()) callback(); }
        public void Dispose() { }
    }
}
