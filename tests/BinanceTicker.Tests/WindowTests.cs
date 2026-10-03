using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;

namespace BinanceTicker.Tests;

public sealed class WindowTests
{
    [Fact]
    public void WpfLifecycleAndViewsWorkOnStaThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new TestApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                { Source = new Uri("pack://application:,,,/BinanceTicker;component/Themes/Dark.xaml") });
                application.Resources.Add("BoolVisibility", new BooleanToVisibilityConverter());
                var window = new EventWindow { Width = 390, Height = 220, ShowActivated = false };
                var settings = new AppSettings();
                var saves = 0;
                using (var manager = new TickerWindowManager(window, settings, () => saves++))
                {
                    window.Show();
                    Assert.True(window.Topmost);
                    Assert.True(window.ShowInTaskbar);
                    window.DeactivateForTest();
                    Assert.True(window.IsVisible);
                    window.Close();
                    Assert.False(window.IsVisible);
                    window.Show();
                    manager.SetMode(DisplayMode.Float);
                    Assert.False(window.Topmost);
                    Assert.True(window.ShowInTaskbar);
                    window.DeactivateForTest();
                    Assert.False(window.IsVisible);
                    manager.SetMode(DisplayMode.Fix);
                    manager.Show();
                    window.Left = 50000; window.Top = 50000;
                    manager.EnsureVisible();
                    Assert.True(window.Left < 50000);
                    Assert.True(window.Top < 50000);
                    manager.SavePosition();
                    Assert.Equal(window.Left, settings.Window.Left);
                    Assert.True(saves > 0);
                    manager.CloseForExit();
                }

                var ticker = new TickerViewModel(); ticker.Configure(new());
                ticker.Update(new("BTCUSDT", 82351.2m, 2.31m, DateTime.UtcNow));
                ticker.Update(new("ETHUSDT", 3124.5m, 1.82m, DateTime.UtcNow));
                ticker.Update(new("ENAUSDT", 0.582m, -0.61m, DateTime.UtcNow));
                ticker.SetStatus(ConnectionStatus.Connected);
                var tickerWindow = new TickerWindow { DataContext = ticker, ShowActivated = false };
                Render(tickerWindow, "ticker-preview.png");
                Assert.NotNull(tickerWindow.Icon);
                Assert.True(tickerWindow.ActualHeight > 100);
                var priceHeader = Descendants<Button>(tickerWindow).Single(b => ReferenceEquals(b.Command, ticker.SortPriceCommand));
                var invokeSort = (IInvokeProvider)new ButtonAutomationPeer(priceHeader).GetPattern(PatternInterface.Invoke);
                invokeSort.Invoke();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(new[] { "ENA", "ETH", "BTC" }, ticker.Prices.Select(p => p.Asset));
                Render(tickerWindow, "ticker-sort-preview.png");
                invokeSort.Invoke();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(new[] { "BTC", "ETH", "ENA" }, ticker.Prices.Select(p => p.Asset));
                var settingsVm = new SettingsViewModel(new(), new NoNetwork());
                var settingsWindow = new SettingsWindow(settingsVm, _ => Task.FromResult(true)) { ShowActivated = false };
                Assert.Equal(WindowStartupLocation.Manual, settingsWindow.WindowStartupLocation);
                Assert.InRange(settingsWindow.Top + settingsWindow.Height, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom);
                Assert.InRange(settingsWindow.Left + settingsWindow.Width, SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right);
                Render(settingsWindow, "settings-preview.png");
                Assert.NotNull(settingsWindow.Icon);
                Assert.Equal(settingsVm, settingsWindow.DataContext);
                var upperInput = settingsWindow.FindName("UpperPriceInput") as TextBox;
                var lowerInput = settingsWindow.FindName("LowerPriceInput") as TextBox;
                Assert.NotNull(upperInput);
                Assert.NotNull(lowerInput);
                upperInput.SetCurrentValue(TextBox.TextProperty, "invalid");
                var saveAlertButton = Descendants<Button>(settingsWindow).Single(b => Equals(b.Content, "儲存"));
                ((IInvokeProvider)new ButtonAutomationPeer(saveAlertButton).GetPattern(PatternInterface.Invoke)).Invoke();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.True(settingsWindow.IsVisible);
                Assert.NotEmpty(settingsVm.Error);
                var errorLabel = settingsWindow.FindName("SettingsErrorText") as TextBlock;
                Assert.NotNull(errorLabel);
                Assert.True(errorLabel.IsVisible);
                Assert.InRange(errorLabel.TranslatePoint(new Point(0, errorLabel.ActualHeight), settingsWindow).Y, 1, settingsWindow.ActualHeight);
                settingsVm.Error = "";
                upperInput.SetCurrentValue(TextBox.TextProperty, "85000.125");
                lowerInput.SetCurrentValue(TextBox.TextProperty, "80000");
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                Assert.Equal(85000.125m, settingsVm.CreateSettings().Symbols[0].Alert.UpperPrice);
                Assert.Equal(80000m, settingsVm.CreateSettings().Symbols[0].Alert.LowerPrice);
                var resetUpper = Descendants<Button>(settingsWindow).Single(b => Equals(b.Content, "重設上限"));
                ((IInvokeProvider)new ButtonAutomationPeer(resetUpper).GetPattern(PatternInterface.Invoke)).Invoke();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(new AlertResetRequest("BTCUSDT", AlertType.Upper), Assert.Single(settingsVm.GetAlertResets()));
                Render(settingsWindow, "settings-alerts-preview.png");
                var triggeredSettings = new AppSettings();
                triggeredSettings.Symbols[0].Alert.UpperTriggered = true;
                ticker.Configure(triggeredSettings);
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                Assert.True(ticker.Prices.Single(p => p.Symbol == "BTCUSDT").HasTriggeredAlert);
                tickerWindow.UpdateLayout();
                Assert.Contains(Descendants<TextBlock>(tickerWindow), t => t.Text == "▲ 提醒" && t.IsVisible);
                Render(tickerWindow, "ticker-alerts-preview.png");
                ticker.SetAlertState("BTCUSDT", new());
                Assert.False(ticker.Prices.Single(p => p.Symbol == "BTCUSDT").HasTriggeredAlert);
                var themeButton = tickerWindow.FindName("ThemeToggleButton") as Button;
                Assert.NotNull(themeButton);
                var theme = ColorTheme.Dark;
                var switches = 0;
                tickerWindow.ThemeRequested += () =>
                {
                    theme = theme == ColorTheme.Dark ? ColorTheme.Light : ColorTheme.Dark;
                    ThemeService.Apply(theme); ticker.SetTheme(theme); settingsWindow.RefreshTheme(); switches++;
                };
                var toggleTheme = (IInvokeProvider)new ButtonAutomationPeer(themeButton).GetPattern(PatternInterface.Invoke);
                var darkTickerText = tickerWindow.Foreground;
                var darkSettingsBackground = settingsWindow.Background;
                var symbolList = Descendants<ListBox>(settingsWindow).Single();
                var darkListText = symbolList.Foreground;
                toggleTheme.Invoke();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(1, switches);
                Assert.True(ticker.IsLightTheme);
                Assert.Equal("切換為深色模式", themeButton.ToolTip);
                Assert.NotEqual(darkTickerText, tickerWindow.Foreground);
                Assert.NotEqual(darkSettingsBackground, settingsWindow.Background);
                Assert.NotEqual(darkListText, symbolList.Foreground);
                Render(tickerWindow, "ticker-light-preview.png");
                Render(settingsWindow, "settings-light-preview.png");
                using (var lightTray = new TrayIconService(application.Dispatcher, () => { }, () => { }, _ => { }, () => { }))
                    RenderTrayMenu(lightTray, "tray-menu-light-preview.png");
                toggleTheme.Invoke();
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal(2, switches);
                Assert.False(ticker.IsLightTheme);
                Assert.Equal("切換為淺色模式", themeButton.ToolTip);
                Assert.Equal(((SolidColorBrush)darkTickerText).Color, ((SolidColorBrush)tickerWindow.Foreground).Color);
                Assert.Equal(((SolidColorBrush)darkSettingsBackground).Color, ((SolidColorBrush)settingsWindow.Background).Color);
                ticker.Configure(new() { Symbols = Enumerable.Range(1, 25).Select(i => new SymbolSetting { Symbol = $"TEST{i}USDT", Order = i }).ToList() });
                tickerWindow.MaxHeight = 300; tickerWindow.UpdateLayout();
                var priceScroll = Descendants<ScrollViewer>(tickerWindow).Single();
                Assert.True(priceScroll.ViewportHeight < priceScroll.ExtentHeight, "Long watchlists must scroll inside small work areas");
                Assert.True(tickerWindow.ActualHeight <= 300);
                tickerWindow.Close();
                var symbolInput = Descendants<TextBox>(settingsWindow).Single(t => t.DataContext is SettingsViewModel);
                settingsWindow.Activate();
                Assert.True(symbolInput.Focus());
                TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, symbolInput, "SOL"));
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.Equal("SOL", symbolInput.Text);
                Assert.Equal("SOL", settingsVm.NewSymbol);
                settingsWindow.UpdateLayout();
                var inputHost = (ScrollViewer)symbolInput.Template.FindName("PART_ContentHost", symbolInput);
                var characterBounds = symbolInput.GetRectFromCharacterIndex(0);
                Assert.True(inputHost.ViewportHeight >= characterBounds.Height,
                    $"Typed symbols must be visible: viewport={inputHost.ViewportHeight}, character={characterBounds}, host padding={inputHost.Padding}, host margin={inputHost.Margin}");
                Render(settingsWindow, "settings-input-preview.png");
                var checkbox = Descendants<CheckBox>(settingsWindow).First(c => c.DataContext is SymbolSetting s && s.Symbol == "ETHUSDT");
                checkbox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
                Assert.NotNull(symbolList.SelectedItem);
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                Assert.Equal("ETHUSDT", settingsVm.SelectedSymbol?.Symbol);
                var modeSelector = Descendants<ComboBox>(settingsWindow).Single();
                modeSelector.IsDropDownOpen = true;
                settingsWindow.UpdateLayout();
                modeSelector.SetCurrentValue(ComboBox.SelectedItemProperty, DisplayMode.Float);
                modeSelector.IsDropDownOpen = false;
                var opacitySlider = Descendants<Slider>(settingsWindow).Single();
                opacitySlider.SetCurrentValue(Slider.ValueProperty, 0.73);
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                var editedSettings = settingsVm.CreateSettings();
                Assert.Equal(DisplayMode.Float, editedSettings.Mode);
                Assert.Equal(0.73, editedSettings.Window.Opacity);
                settingsWindow.Height = 430; settingsWindow.UpdateLayout();
                Assert.True(settingsWindow.ActualHeight <= 430, "Settings must fit a small/high-DPI work area");
                var saveButton = Descendants<Button>(settingsWindow).Single(b => Equals(b.Content, "儲存"));
                Assert.InRange(saveButton.TranslatePoint(new Point(0, saveButton.ActualHeight), settingsWindow).Y, 1, settingsWindow.ActualHeight);
                Render(settingsWindow, "settings-small-preview.png");
                settingsWindow.Width = 400; settingsWindow.UpdateLayout();
                Render(settingsWindow, "settings-narrow-preview.png");
                settingsWindow.Close();
                using var tray = new TrayIconService(application.Dispatcher, () => { }, () => { }, _ => { }, () => { });
                tray.SetMode(DisplayMode.Fix);
                RenderTrayMenu(tray, "tray-menu-preview.png");
                tray.SetMode(DisplayMode.Float);
                RenderTrayMenu(tray, "tray-menu-float-preview.png");
                RenderTrayMenu(tray, "tray-menu-hover-preview.png", 0);
                VerifyRuntimePriceAlerts(application);
                VerifyPendingPriceUpdateDrainsBeforeShutdown(application);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF smoke test timed out");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void VerifyRuntimePriceAlerts(App application)
    {
        var directory = Path.Combine(Path.GetTempPath(), "TickerRuntimeAlerts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var settings = new AppSettings();
            settings.Symbols[0].Alert.UpperPrice = 85000m;
            var store = new SettingsService(Path.Combine(directory, "settings.json"));
            store.Save(settings);
            var history = new AlertHistoryService(Path.Combine(directory, "alerts-history.json"));
            var sent = new List<string>();
            var alerts = new PriceAlertService(settings, store, history, new NotificationService((_, message) => sent.Add(message)));
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(App).GetField("settings", fields)!.SetValue(application, settings);
            typeof(App).GetField("alerts", fields)!.SetValue(application, alerts);
            var ticker = (TickerViewModel)typeof(App).GetField("ticker", fields)!.GetValue(application)!;
            ticker.Configure(settings);
            var update = typeof(App).GetMethod("UpdatePriceAsync", fields)!;
            void Receive(TickerPrice price) => ((Task)update.Invoke(application, [price])!).GetAwaiter().GetResult();
            var now = DateTime.UtcNow;
            Receive(new("BTCUSDT", 85120.5m, 1m, now));
            Receive(new("BTCUSDT", 86000m, 1m, now.AddSeconds(1)));
            Assert.Single(sent);
            Assert.Single(history.Load());
            Assert.True(ticker.Prices[0].HasTriggeredAlert);
            alerts.ResetAsync("BTCUSDT", AlertType.Upper).GetAwaiter().GetResult();
            Receive(new("BTCUSDT", 90000m, 1m, now.AddSeconds(-1)));
            Assert.Single(sent); // A stale reconnect quote must not re-trigger after reset.
            Receive(new("BTCUSDT", 87000m, 1m, now.AddSeconds(2)));
            Assert.Equal(2, sent.Count);
            Assert.Equal(87000m, history.Load()[1].TriggeredPrice);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static void VerifyPendingPriceUpdateDrainsBeforeShutdown(App application)
    {
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var alerts = new PendingAlerts();
        var tray = new TrayIconService(application.Dispatcher, () => { }, () => { }, _ => { }, () => { });
        var nativeTray = (System.Windows.Forms.NotifyIcon)typeof(TrayIconService).GetField("tray", fields)!.GetValue(tray)!;
        typeof(App).GetField("alerts", fields)!.SetValue(application, alerts);
        typeof(App).GetField("tray", fields)!.SetValue(application, tray);
        var queue = typeof(App).GetMethod("QueuePriceUpdate", fields)!;
        var shutdown = typeof(App).GetMethod("ExitAsync", fields)!;
        application.Dispatcher.Invoke(() => queue.Invoke(application, [new TickerPrice("BTCUSDT", 90000m, 0m, DateTime.UtcNow.AddMinutes(1))]));
        Assert.True(alerts.Entered);
        Task? exit = null;
        application.Dispatcher.Invoke(() => exit = (Task)shutdown.Invoke(application, null)!);
        Assert.NotNull(exit);
        Assert.False(exit.IsCompleted);
        Assert.True(nativeTray.Visible, "The notification icon must survive until pending alert work completes.");
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = exit.ContinueWith(_ => frame.Continue = false, CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        alerts.Release();
        if (!exit.IsCompleted)
        {
            var timeout = new System.Windows.Threading.DispatcherTimer(TimeSpan.FromSeconds(5),
                System.Windows.Threading.DispatcherPriority.Send, (_, _) => frame.Continue = false, application.Dispatcher);
            try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
            finally { timeout.Stop(); }
        }
        Assert.True(exit.IsCompletedSuccessfully);
        Assert.False(nativeTray.Visible);
    }

    private sealed class PendingAlerts : IPriceAlertService
    {
        private readonly TaskCompletionSource completion = new();
        public bool Entered { get; private set; }
        public Task CheckAsync(string symbol, decimal currentPrice) { Entered = true; return completion.Task; }
        public Task ResetAsync(string symbol, AlertType? type = null) => Task.CompletedTask;
        public Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets) => Task.CompletedTask;
        public void Release() => completion.SetResult();
    }

    private sealed class TestApp : App
    {
        // WPF schedules startup on its dispatcher even without Application.Run.
        // Keep view/runtime tests independent of Binance and the user's real settings.
        protected override void OnStartup(StartupEventArgs e) { }
    }

    private static void Render(Window window, string filename)
    {
        window.Show(); window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        var width = content.ActualWidth + content.Margin.Left + content.Margin.Right;
        var height = content.ActualHeight + content.Margin.Top + content.Margin.Bottom;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var directory = Environment.GetEnvironmentVariable("TICKER_TEST_ARTIFACTS");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            using var file = File.Create(Path.Combine(directory, filename)); encoder.Save(file);
        }
    }

    private static void RenderTrayMenu(TrayIconService service, string filename, int selectedIndex = -1)
    {
        var directory = Environment.GetEnvironmentVariable("TICKER_TEST_ARTIFACTS");
        if (directory is null) return;
        var field = typeof(TrayIconService).GetField("menu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var menu = (System.Windows.Forms.ContextMenuStrip)field.GetValue(service)!;
        menu.CreateControl();
        menu.Size = menu.GetPreferredSize(System.Drawing.Size.Empty);
        menu.PerformLayout();
        if (selectedIndex >= 0) menu.Items[selectedIndex].Select();
        using var bitmap = new System.Drawing.Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, menu.Size));
        Directory.CreateDirectory(directory);
        bitmap.Save(Path.Combine(directory, filename), System.Drawing.Imaging.ImageFormat.Png);
    }

    private sealed class EventWindow : Window
    {
        public void DeactivateForTest() => OnDeactivated(EventArgs.Empty);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private sealed class NoNetwork : IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
