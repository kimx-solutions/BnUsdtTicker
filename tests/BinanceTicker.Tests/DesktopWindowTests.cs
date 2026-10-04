using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;

namespace BinanceTicker.Tests;

internal static class DesktopWindowTests
{
    public static void Verify(Application application)
    {
        var settings = new AppSettings { Window = new() { Width = 620, Height = 480 } };
        var ticker = new TickerViewModel();
        settings.Symbols = Enumerable.Range(1, 50).Select(i => new SymbolSetting { Symbol = $"COIN{i}USDT", Order = i }).ToList();
        ticker.Configure(settings);
        var window = new TickerWindow { DataContext = ticker, ShowActivated = false };
        var saves = 0;
        using var manager = new TickerWindowManager(window, settings, () => saves++);
        manager.Show(); window.UpdateLayout();
        VerifyNativeHotkey(application, window, manager);
        Assert.Equal(620, window.ActualWidth);
        Assert.Equal(480, window.ActualHeight);
        var grips = Descendants<Thumb>(window).Where(t => t.Tag is string).ToArray();
        Assert.Equal(8, grips.Length);
        Assert.All(grips, grip => Assert.False(TickerWindowManager.IsDragSource(grip)));
        foreach (var grip in grips)
        {
            var point = grip.TranslatePoint(new Point(grip.ActualWidth / 2, grip.ActualHeight / 2), window);
            var hit = Assert.IsAssignableFrom<DependencyObject>(window.InputHitTest(point));
            Assert.False(TickerWindowManager.IsDragSource(hit));
        }
        // Layered-window pixels with zero alpha let native mouse input through.
        var previousOpacity = window.Opacity;
        window.Opacity = 0.2;
        var cornerBitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        cornerBitmap.Render(window);
        var cornerPixel = new byte[4];
        cornerBitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), cornerPixel, 4, 0);
        Assert.True(cornerPixel[3] > 0, "Resize corners need nonzero alpha even at the minimum window opacity.");
        window.Opacity = previousOpacity;
        Assert.False(TickerWindowManager.IsDragSource(Descendants<Button>(window).First()));
        Assert.True(TickerWindowManager.IsDragSource(new TextBlock()));
        Assert.False(TickerWindowManager.IsDragSource(Descendants<ScrollBar>(window).First()));
        foreach (var grip in grips)
        {
            window.Left = 100; window.Top = 100; window.Width = 620; window.Height = 480;
            var edge = (string)grip.Tag;
            grip.RaiseEvent(new DragDeltaEventArgs(10, 10) { RoutedEvent = Thumb.DragDeltaEvent });
            Assert.Equal(edge.Contains('W') ? 610 : edge.Contains('E') ? 630 : 620, window.Width);
            Assert.Equal(edge.Contains('N') ? 470 : edge.Contains('S') ? 490 : 480, window.Height);
        }
        window.Width = 500; window.Height = 400;
        manager.Hide();
        Assert.Equal(500, settings.Window.Width); Assert.Equal(400, settings.Window.Height);
        Assert.True(saves > 0);
        manager.Show(); manager.SetMode(DisplayMode.Float); manager.SetMode(DisplayMode.Fix);
        Assert.Equal(500, window.Width); Assert.Equal(400, window.Height);
        VerifySaveFailure((App)application, window, manager, settings);
        manager.ResetSize();
        Assert.Equal(WindowSettings.DefaultWidth, window.Width);
        Assert.Equal(WindowSettings.DefaultHeight, window.Height);
        window.Width = WindowSettings.MinimumWidth; window.Height = WindowSettings.MinimumHeight;
        window.UpdateLayout();
        var scroll = Descendants<ScrollViewer>(window).Single();
        Assert.True(scroll.ScrollableHeight > 0);
        Assert.True(scroll.ViewportHeight > 0);
        scroll.ScrollToBottom();
        foreach (var theme in new[] { ColorTheme.Dark, ColorTheme.Light })
        {
            ThemeService.Apply(theme); ticker.SetTheme(theme); window.UpdateLayout();
            var settingsButton = Descendants<Button>(window).Single(b => Equals(b.ToolTip, "設定"));
            var themeButton = (Button)window.FindName("ThemeToggleButton");
            Assert.True(settingsButton.TransformToAncestor(window).Transform(new Point()).Y < window.ActualHeight);
            Assert.True(themeButton.TransformToAncestor(window).Transform(new Point()).Y + themeButton.ActualHeight <= window.ActualHeight);
            var directory = Environment.GetEnvironmentVariable("TICKER_TEST_ARTIFACTS");
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(directory, $"desktop-small-{theme}.png")); encoder.Save(file);
            }
        }
        ThemeService.Apply(ColorTheme.Dark);
        manager.CloseForExit();
    }

    private static void VerifyNativeHotkey(Application application, Window window, TickerWindowManager manager)
    {
        var competitor = new Window();
        using var platform = new WindowsHotkeyPlatform(window);
        using var other = new WindowsHotkeyPlatform(competitor);
        const int id = 0x1001;
        const uint modifiers = 0x4007, key = 134; // Ctrl+Alt+Shift+F23
        Assert.True(platform.Register(id, modifiers, key, manager.Toggle));
        Assert.False(other.Register(id, modifiers, key, () => { }));
        manager.Hide();
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        Assert.True(PostMessage(handle, 0x0312, new IntPtr(id), IntPtr.Zero));
        application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.True(window.IsVisible);
        platform.Unregister(id);
        Assert.True(other.Register(id, modifiers, key, () => { }));
        other.Unregister(id); competitor.Close();
    }

    private static void VerifySaveFailure(App application, Window window, TickerWindowManager manager, AppSettings settings)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var fields = new[] { "manager", "settings", "alerts", "desktopPreferences" }.Select(n => typeof(App).GetField(n, flags)!).ToArray();
        var previous = fields.Select(f => f.GetValue(application)).ToArray();
        var startup = new FakeStartup();
        var platform = new FakeHotkeys();
        using var hotkeys = new GlobalHotkeyService(platform, () => { });
        var draft = settings.Copy(); draft.Window.Width = 620; draft.Window.Height = 480;
        draft.StartWithWindows = true; draft.Hotkey.Enabled = true;
        try
        {
            fields[0].SetValue(application, manager); fields[1].SetValue(application, settings);
            fields[2].SetValue(application, new FailingAlerts());
            fields[3].SetValue(application, new DesktopPreferencesService(hotkeys, startup));
            var task = (Task<bool>)typeof(App).GetMethod("ApplySettingsAsync", flags)!.Invoke(application, [draft])!;
            Assert.False(task.GetAwaiter().GetResult());
            Assert.Equal(500, draft.Window.Width); Assert.Equal(400, draft.Window.Height);
            Assert.Equal(500, window.Width); Assert.Equal(400, window.Height);
            Assert.False(settings.StartWithWindows); Assert.Null(startup.Command); Assert.Empty(platform.Ids);
        }
        finally { for (var i = 0; i < fields.Length; i++) fields[i].SetValue(application, previous[i]); }
    }

    private sealed class FakeStartup : IStartupRegistration
    {
        public string? Command;
        public string? Read() => Command;
        public void Apply(bool enabled) => Command = enabled ? "test.exe" : null;
        public void Restore(string? command) => Command = command;
    }
    private sealed class FakeHotkeys : IHotkeyPlatform
    {
        public HashSet<int> Ids = [];
        public bool Register(int id, uint modifiers, uint key, Action callback) => Ids.Add(id);
        public void Unregister(int id) => Ids.Remove(id);
        public void Dispose() { }
    }
    private sealed class FailingAlerts : BinanceTicker.Core.Services.IPriceAlertService
    {
        public Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets, bool preserveAlerts = false) => throw new IOException("read only settings");
        public Task CheckAsync(string symbol, decimal price) => Task.CompletedTask;
        public Task ResetAsync(string symbol, AlertType? type = null) => Task.CompletedTask;
        public Task SaveAlertAsync(string symbol, PriceAlertSettings alert, IReadOnlyList<AlertResetRequest> resets) => Task.CompletedTask;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
