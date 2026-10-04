using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;

namespace BinanceTicker.Tests;

internal static class AlertHistoryWindowTests
{
    public static void VerifyQuoteRouting(BinanceTicker.App app)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var sink = new QuoteSink();
        var previous = typeof(BinanceTicker.App).GetField("alerts", flags)!.GetValue(app);
        typeof(BinanceTicker.App).GetField("alerts", flags)!.SetValue(app, sink);
        var ticker = (TickerViewModel)typeof(BinanceTicker.App).GetField("ticker", flags)!.GetValue(app)!;
        ticker.Configure(new());
        ticker.ToggleSparklineCommand.Execute(null);
        Assert.False(ticker.ShowSparkline);
        try
        {
            var quote = new TickerPrice("BTCUSDT", 100, 0, DateTime.UtcNow, Source: QuoteSource.Stream);
            ((Task)typeof(BinanceTicker.App).GetMethod("UpdatePriceAsync", flags)!.Invoke(app, [quote])!).GetAwaiter().GetResult();
            Assert.Same(quote, sink.Received);
            typeof(BinanceTicker.App).GetMethod("QueueAlertConnectionStatus", flags)!.Invoke(app, [ConnectionStatus.Disconnected]);
            Assert.Equal(ConnectionStatus.Disconnected, sink.Status);
        }
        finally { typeof(BinanceTicker.App).GetField("alerts", flags)!.SetValue(app, previous); }
    }
    private sealed class QuoteSink : IPriceAlertService
    {
        public TickerPrice? Received;
        public ConnectionStatus? Status;
        public Task CheckAsync(string symbol, decimal currentPrice) => Task.CompletedTask;
        public Task CheckQuoteAsync(TickerPrice quote) { Received = quote; return Task.CompletedTask; }
        public Task OnConnectionStatusAsync(ConnectionStatus status) { Status = status; return Task.CompletedTask; }
        public Task ResetAsync(string symbol, AlertType? type = null) => Task.CompletedTask;
        public Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets, bool preserveAlerts = false) => Task.CompletedTask;
        public Task SaveAlertAsync(string symbol, PriceAlertSettings updated, IReadOnlyList<AlertResetRequest> resets) => Task.CompletedTask;
    }
    public static void Verify(Application app)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var history = new AlertHistoryService(Path.Combine(directory, "history.json"));
        var managerType = typeof(TickerWindow).Assembly.GetType("BinanceTicker.Services.AlertHistoryWindowManager");
        Assert.NotNull(managerType);
        var manager = Activator.CreateInstance(managerType, history)!;
        var show = managerType.GetMethod("Show")!;
        var window = (Window)show.Invoke(manager, null)!;
        try
        {
            var vm = Assert.IsType<AlertHistoryViewModel>(window.DataContext);
            Assert.True(vm.IsEmpty);
            history.Save([new("BTCUSDT", AlertType.Upper, 100, 101, DateTimeOffset.UtcNow),
                new("OLDUSDT", AlertType.Fall, null, 98, DateTimeOffset.UtcNow, 5, 2, 100, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow, -2)]);
            managerType.GetMethod("Refresh")!.Invoke(manager, null);
            Assert.Equal(2, vm.Entries.Count);
            window.WindowState = WindowState.Minimized;
            Assert.Same(window, show.Invoke(manager, null));
            Assert.Equal(WindowState.Normal, window.WindowState);
            Render(window, "history-dark.png");
            Assert.Contains(Descendants<TextBlock>(window), text => text.Text == "全部類型");
            Assert.Contains(Descendants<TextBlock>(window), text => text.Text == "全部幣種");
            Assert.Single(Descendants<DataGrid>(window));
            var typeFilter = Descendants<ComboBox>(window).Single(box => AutomationProperties.GetName(box) == "篩選提醒類型");
            typeFilter.SelectedValue = AlertType.Fall;
            Assert.Equal(AlertType.Fall, vm.TypeFilter); Assert.Single(vm.Entries);
            var startDate = Descendants<DatePicker>(window).Single(picker => AutomationProperties.GetName(picker) == "起始日期");
            startDate.SelectedDate = DateTime.Today;
            Assert.Equal(DateTime.Today, vm.StartDate); Assert.Single(vm.Entries);
            var grid = Descendants<DataGrid>(window).Single(); grid.SelectedIndex = 0; window.UpdateLayout();
            Assert.Contains(Descendants<TextBlock>(window), text => text.Text.Contains("基準 100 USDT"));
            ThemeService.Apply(ColorTheme.Light); managerType.GetMethod("RefreshTheme")!.Invoke(manager, null);
            Render(window, "history-light.png");
            File.WriteAllText(history.FilePath, "{"); managerType.GetMethod("Refresh")!.Invoke(manager, null);
            Assert.NotEmpty(vm.Error); Assert.Single(vm.Entries);
            Render(window, "history-error.png");
            var editorVm = new PriceAlertEditorViewModel("BTCUSDT", new());
            var editor = new PriceAlertWindow(editorVm, (_, _) => Task.FromResult(true));
            try
            {
                editor.Show(); editor.UpdateLayout();
                Assert.Contains(Descendants<TextBlock>(editor), text => text.Text == "單次");
                Assert.Contains(Descendants<TextBox>(editor), box => AutomationProperties.GetName(box) == "短期上漲門檻百分比");
                editorVm.Rise.ThresholdText = "2"; editorVm.Rise.Policy.Strategy = AlertStrategy.Repeat;
                var strategy = Descendants<ComboBox>(editor).First(box => box.DataContext == editorVm.UpperPolicy);
                strategy.SelectedValue = AlertStrategy.Repeat;
                Assert.Equal(AlertStrategy.Repeat, editorVm.UpperPolicy.Strategy);
                var cooldown = Descendants<TextBox>(editor).First(box => box.DataContext == editorVm.UpperPolicy && AutomationProperties.GetName(box) == "冷卻分鐘");
                cooldown.Text = "2"; Assert.Equal(2, editorVm.CreateAlert().UpperPolicy.CooldownMinutes);
                Render(editor, "advanced-editor-light.png");
                ThemeService.Apply(ColorTheme.Dark); editor.RefreshTheme(); editor.Width = 360;
                Render(editor, "advanced-editor-dark-narrow.png");
            }
            finally { editor.Close(); }
        }
        finally
        {
            managerType.GetMethod("Close")!.Invoke(manager, null);
            Assert.False(window.IsVisible);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            ThemeService.Apply(ColorTheme.Dark);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var matchChild in Descendants<T>(child)) yield return matchChild;
        }
    }
    private static void Render(Window window, string name)
    {
        window.Show(); window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "artifacts", "issue-7-previews", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!); using var stream = File.Create(path); encoder.Save(stream);
    }
}
