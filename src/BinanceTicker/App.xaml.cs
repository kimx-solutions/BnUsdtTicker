using System.Net.Http;
using System.Windows;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;
using BinanceTicker.Views;

namespace BinanceTicker;

public partial class App : Application
{
    private readonly SettingsService settingsService = new();
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly SemaphoreSlim feedGate = new(1, 1);
    private readonly TickerViewModel ticker = new();
    private AppSettings settings = new();
    private TickerWindowManager? manager;
    private TrayIconService? tray;
    private SettingsWindow? settingsWindow;
    private BinanceService binance = null!;
    private CancellationTokenSource? feedCancellation;
    private Task feedTask = Task.CompletedTask;
    private bool exiting;
    private int feedGeneration;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { settings = settingsService.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show("無法讀取設定，將使用預設值。\n" + ex.Message, "Binance Ticker"); }
        binance = new(http);
        ticker.Configure(settings);
        var window = new TickerWindow { DataContext = ticker };
        manager = new(window, settings, SaveSettings);
        window.SettingsRequested += OpenSettings;
        tray = new(Dispatcher, manager.Show, OpenSettings, ChangeMode, () => _ = ExitAsync());
        tray.SetMode(settings.Mode);
        if (settings.ShowOnStartup) manager.Show();
        if (settingsService.LoadWarning is { } warning) tray.ShowWarning(warning);
        await RestartFeedAsync();
    }
    private void SaveSettings()
    {
        try { settingsService.Save(settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { tray?.ShowWarning("無法儲存設定：" + ex.Message); }
    }
    private void ChangeMode(DisplayMode mode)
    {
        if (exiting) return;
        manager!.SetMode(mode); ticker.SetMode(mode); tray!.SetMode(mode); SaveSettings(); manager.Show();
    }
    private void OpenSettings()
    {
        if (exiting) return;
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        settingsWindow = new(new(settings, binance), ApplySettingsAsync);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show(); settingsWindow.Activate();
    }
    private async Task<bool> ApplySettingsAsync(AppSettings updated)
    {
        if (exiting) return false;
        updated.Window.Left = settings.Window.Left; updated.Window.Top = settings.Window.Top;
        try { settingsService.Save(updated); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { if (settingsWindow is not null) settingsWindow.ViewModel.Error = "儲存失敗：" + ex.Message; return false; }
        settings = updated;
        ticker.Configure(settings); manager!.Apply(settings); tray!.SetMode(settings.Mode);
        await RestartFeedAsync();
        if (!exiting) manager.Show();
        return true;
    }
    private async Task RestartFeedAsync()
    {
        await feedGate.WaitAsync();
        try
        {
            await StopFeedAsync();
            if (exiting) return;
            feedCancellation = new();
            var token = feedCancellation.Token;
            var generation = ++feedGeneration;
            void OnUi(Action action) => Dispatcher.BeginInvoke(() =>
            { if (!exiting && !token.IsCancellationRequested && generation == feedGeneration) action(); });
            var symbols = settings.Symbols.Where(s => s.Enabled).OrderBy(s => s.Order).Select(s => s.Symbol).ToArray();
            ticker.SetStatus(ConnectionStatus.Connecting);
            var feed = new MarketFeed(binance, new());
            feedTask = Task.Run(() => feed.RunAsync(symbols, p => OnUi(() => ticker.Update(p)), status => OnUi(() => ticker.SetStatus(status)), token));
        }
        finally { feedGate.Release(); }
    }
    private async Task StopFeedAsync()
    {
        feedCancellation?.Cancel();
        try { await feedTask; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { tray?.ShowWarning("行情連線已停止：" + ex.Message); }
        feedCancellation?.Dispose(); feedCancellation = null;
    }
    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true; manager?.SavePosition(); settingsWindow?.Close();
        await feedGate.WaitAsync();
        try { await StopFeedAsync(); }
        finally { feedGate.Release(); }
        tray?.Dispose(); tray = null;
        manager?.CloseForExit(); manager?.Dispose(); http.Dispose(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        feedCancellation?.Cancel(); tray?.Dispose(); manager?.Dispose(); http.Dispose(); base.OnExit(e);
    }
}
