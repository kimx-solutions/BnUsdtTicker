using System.Windows;
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
                var application = new App();
                application.InitializeComponent();
                var window = new EventWindow { Width = 390, Height = 220, ShowActivated = false };
                var settings = new AppSettings();
                var saves = 0;
                using (var manager = new TickerWindowManager(window, settings, () => saves++))
                {
                    window.Show();
                    Assert.True(window.Topmost);
                    Assert.False(window.ShowInTaskbar);
                    window.DeactivateForTest();
                    Assert.True(window.IsVisible);
                    window.Close();
                    Assert.False(window.IsVisible);
                    window.Show();
                    manager.SetMode(DisplayMode.Float);
                    Assert.False(window.Topmost);
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
                Assert.True(tickerWindow.ActualHeight > 100);
                ticker.Configure(new() { Symbols = Enumerable.Range(1, 25).Select(i => new SymbolSetting { Symbol = $"TEST{i}USDT", Order = i }).ToList() });
                tickerWindow.MaxHeight = 300; tickerWindow.UpdateLayout();
                var priceScroll = Descendants<ScrollViewer>(tickerWindow).Single();
                Assert.True(priceScroll.ViewportHeight < priceScroll.ExtentHeight, "Long watchlists must scroll inside small work areas");
                Assert.True(tickerWindow.ActualHeight <= 300);
                tickerWindow.Close();
                var settingsVm = new SettingsViewModel(new(), new NoNetwork());
                var settingsWindow = new SettingsWindow(settingsVm, _ => Task.FromResult(true)) { ShowActivated = false };
                Assert.Equal(WindowStartupLocation.Manual, settingsWindow.WindowStartupLocation);
                Assert.InRange(settingsWindow.Top + settingsWindow.Height, SystemParameters.WorkArea.Top, SystemParameters.WorkArea.Bottom);
                Assert.InRange(settingsWindow.Left + settingsWindow.Width, SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right);
                Render(settingsWindow, "settings-preview.png");
                Assert.Equal(settingsVm, settingsWindow.DataContext);
                var checkbox = Descendants<CheckBox>(settingsWindow).First(c => c.DataContext is SymbolSetting s && s.Symbol == "ETHUSDT");
                checkbox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = Mouse.PreviewMouseDownEvent });
                var symbolList = Descendants<ListBox>(settingsWindow).Single();
                Assert.NotNull(symbolList.SelectedItem);
                application.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                Assert.Equal("ETHUSDT", settingsVm.SelectedSymbol?.Symbol);
                settingsWindow.Height = 430; settingsWindow.UpdateLayout();
                Assert.True(settingsWindow.ActualHeight <= 430, "Settings must fit a small/high-DPI work area");
                var saveButton = Descendants<Button>(settingsWindow).Single(b => Equals(b.Content, "儲存"));
                Assert.InRange(saveButton.TranslatePoint(new Point(0, saveButton.ActualHeight), settingsWindow).Y, 1, settingsWindow.ActualHeight);
                settingsWindow.Close();
                using var tray = new TrayIconService(application.Dispatcher, () => { }, () => { }, _ => { }, () => { });
                tray.SetMode(DisplayMode.Float);
                application.Shutdown();
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "WPF smoke test timed out");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Render(Window window, string filename)
    {
        window.Show(); window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var directory = Environment.GetEnvironmentVariable("TICKER_TEST_ARTIFACTS");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            using var file = File.Create(Path.Combine(directory, filename)); encoder.Save(file);
        }
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
