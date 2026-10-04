using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Services;

namespace BinanceTicker.Tests;

public sealed class DesktopConvenienceTests
{
    [Fact]
    public void DesktopSettingsCopyDoesNotShareHotkeyOrWindowState()
    {
        var original = new AppSettings();
        var draft = original.Copy();
        draft.Hotkey.Enabled = true; draft.Hotkey.Gesture = "Alt+F9";
        draft.Window.Width = 700; draft.Window.Height = 500;
        Assert.False(original.Hotkey.Enabled); Assert.Equal("Ctrl+Alt+T", original.Hotkey.Gesture);
        Assert.Equal(390, original.Window.Width); Assert.Equal(360, original.Window.Height);
    }

    [Theory]
    [InlineData("{\"hotkey\":null}")]
    [InlineData("{\"hotkey\":{\"enabled\":true,\"gesture\":\"bad\"}}")]
    [InlineData("{\"hotkey\":{\"enabled\":true,\"gesture\":null}}")]
    public void BadHotkeyConfigurationIsDisabledWithoutLosingSymbols(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, json);
            var loaded = new SettingsService(path).Load();
            Assert.False(loaded.Hotkey.Enabled); Assert.Equal("Ctrl+Alt+T", loaded.Hotkey.Gesture);
            Assert.Equal("BTCUSDT", loaded.Symbols[0].Symbol);
            Assert.False(File.Exists(path + ".bak"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OldSettingsAndInvalidSizesUseDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"window\":{\"width\":-2,\"height\":0}}");
            var store = new SettingsService(path);
            var loaded = store.Load();
            Assert.Equal(390, loaded.Window.Width);
            Assert.Equal(360, loaded.Window.Height);
            Assert.False(loaded.StartWithWindows);
            Assert.False(loaded.Hotkey.Enabled);
            loaded.Window.Width = 600; loaded.Window.Height = 500;
            loaded.StartWithWindows = true; loaded.Hotkey.Enabled = true;
            store.Save(loaded);
            Assert.Equal(600, store.Load().Window.Width);
            Assert.Equal(500, store.Load().Window.Height);
            Assert.True(store.Load().StartWithWindows);
            Assert.True(store.Load().Hotkey.Enabled);
            File.WriteAllText(path, "{}");
            Assert.Equal(390, store.Load().Window.Width);
            Assert.Null(store.LoadWarning);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("Ctrl+Alt+T", 3, 84)]
    [InlineData("Shift+Ctrl+F11", 6, 122)]
    [InlineData("Win+1", 8, 49)]
    public void ParsesSupportedHotkeys(string text, uint modifiers, uint key)
    {
        var gesture = HotkeyGesture.Parse(text);
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(key, gesture.Key);
        Assert.Equal(gesture, HotkeyGesture.Parse(gesture.ToString()));
    }

    [Theory]
    [InlineData("T")]
    [InlineData("Ctrl+Ctrl+T")]
    [InlineData("Ctrl+F12")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Alt+T+X")]
    public void RejectsInvalidOrReservedHotkeys(string text) => Assert.Throws<ArgumentException>(() => HotkeyGesture.Parse(text));

    [Fact]
    public void ClampsOversizedWindowToSmallNegativeCoordinateWorkArea()
    {
        var area = new ScreenRect(-800, -100, 300, 180);
        var result = WindowPlacement.Constrain(new(-2000, 900, 600, 800), area);
        Assert.Equal(area, result);
        Assert.True(WindowPlacement.IsVisible(result, [area]));
    }

    [Fact]
    public void LeftTopResizeKeepsOppositeCornerWhenMinimumReached()
    {
        var result = WindowPlacement.Resize(new(100, 100, 400, 300), "NW", 200, 200, 360, 220, 1000, 1000);
        Assert.Equal(new ScreenRect(140, 180, 360, 220), result);
    }

    [Theory]
    [InlineData("C:\\My App\\BinanceTicker.exe", "\"C:\\My App\\BinanceTicker.exe\"")]
    [InlineData("C:\\App\\BinanceTicker.exe", "\"C:\\App\\BinanceTicker.exe\"")]
    public void StartupCommandQuotesExecutable(string path, string expected) => Assert.Equal(expected, WindowsStartupService.CreateCommand(path));

    [Fact]
    public void StartupCommandRejectsUnsupportedLength() => Assert.Throws<ArgumentException>(() => WindowsStartupService.CreateCommand("C:\\" + new string('x', 270) + ".exe"));
}
