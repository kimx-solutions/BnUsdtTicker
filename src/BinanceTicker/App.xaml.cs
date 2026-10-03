using System.Net.Http;
using System.Text.Json;
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
    private readonly Dictionary<string, PriceAlertWindow> priceAlertWindows = new(StringComparer.Ordinal);
    private BinanceService binance = null!;
    private CancellationTokenSource? feedCancellation;
    private Task feedTask = Task.CompletedTask;
    private bool exiting;
    private int feedGeneration;
    private IPriceAlertService alerts = null!;
    private readonly HashSet<Task> pendingPriceUpdates = [];
    private DateTimeOffset lastAlertWarning = DateTimeOffset.MinValue;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try { settings = settingsService.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show("無法讀取設定，將使用預設值。\n" + ex.Message, "Binance Ticker"); }
        binance = new(http);
        ThemeService.Apply(settings.Ui.Theme);
        ticker.Configure(settings);
        var window = new TickerWindow { DataContext = ticker };
        manager = new(window, settings, SaveSettings);
        window.SettingsRequested += OpenSettings;
        window.PriceAlertRequested += OpenPriceAlert;
        window.ThemeRequested += ToggleTheme;
        tray = new(Dispatcher, manager.Show, OpenSettings, ChangeMode, () => _ = ExitAsync());
        alerts = new PriceAlertService(settings, settingsService, new AlertHistoryService(), new NotificationService(tray));
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
    private void ToggleTheme()
    {
        if (exiting) return;
        settings.Ui.Theme = settings.Ui.Theme == ColorTheme.Dark ? ColorTheme.Light : ColorTheme.Dark;
        ThemeService.Apply(settings.Ui.Theme);
        ticker.SetTheme(settings.Ui.Theme);
        settingsWindow?.RefreshTheme();
        foreach (var window in priceAlertWindows.Values) window.RefreshTheme();
        tray?.RefreshTheme();
        SaveSettings();
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
        // A settings window opened before a theme switch must preserve the latest theme.
        updated.Ui.Theme = settings.Ui.Theme;
        try { await alerts.ApplySettingsAsync(updated, [], preserveAlerts: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or AggregateException)
        { if (settingsWindow is not null) settingsWindow.ViewModel.Error = "儲存失敗：" + ex.Message; return false; }
        settings = updated;
        foreach (var symbol in priceAlertWindows.Keys.ToArray())
            if (!settings.Symbols.Any(s => s.Symbol == symbol)) priceAlertWindows[symbol].Close();
        RefreshAlertWindows();
        ticker.Configure(settings); manager!.Apply(settings); tray!.SetMode(settings.Mode);
        await RestartFeedAsync();
        if (!exiting) manager.Show();
        return true;
    }
    private void OpenPriceAlert(string symbol)
    {
        if (exiting) return;
        if (priceAlertWindows.TryGetValue(symbol, out var existing))
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate(); return;
        }
        var item = settings.Symbols.FirstOrDefault(s => s.Symbol == symbol && s.Enabled);
        if (item is null) return;
        var editor = new PriceAlertEditorViewModel(symbol, item.Alert);
        var window = new PriceAlertWindow(editor, (alert, resets) => SavePriceAlertAsync(symbol, editor, alert, resets));
        priceAlertWindows.Add(symbol, window);
        window.Closed += (_, _) => priceAlertWindows.Remove(symbol);
        window.Show(); window.Activate();
    }
    private async Task<bool> SavePriceAlertAsync(string symbol, PriceAlertEditorViewModel editor,
        PriceAlertSettings alert, IReadOnlyList<AlertResetRequest> resets)
    {
        if (exiting) return false;
        try { await alerts.SaveAlertAsync(symbol, alert, resets); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or InvalidOperationException or AggregateException)
        { editor.Error = "儲存失敗：" + ex.Message; return false; }
        var live = settings.Symbols.FirstOrDefault(s => s.Symbol == symbol)?.Alert;
        if (live is not null) ticker.SetAlertState(symbol, live);
        RefreshAlertWindows();
        return true;
    }
    private void RefreshAlertWindows()
    {
        foreach (var (symbol, window) in priceAlertWindows)
            if (settings.Symbols.FirstOrDefault(s => s.Symbol == symbol)?.Alert is { } alert)
                window.ViewModel.RefreshState(alert);
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
            feedTask = Task.Run(() => feed.RunAsync(symbols, p => OnUi(() => QueuePriceUpdate(p)), status => OnUi(() => ticker.SetStatus(status)), token));
        }
        finally { feedGate.Release(); }
    }
    private async void QueuePriceUpdate(TickerPrice price)
    {
        var task = UpdatePriceAsync(price);
        pendingPriceUpdates.Add(task);
        try { await task; }
        finally { pendingPriceUpdates.Remove(task); }
    }

    private async Task UpdatePriceAsync(TickerPrice price)
    {
        // Ignore quotes older than the row's last accepted timestamp, including reconnect snapshots.
        if (!ticker.Update(price)) return;
        try { await alerts.CheckAsync(price.Symbol, price.Price); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException)
        {
            if (DateTimeOffset.Now - lastAlertWarning >= TimeSpan.FromMinutes(1))
            {
                lastAlertWarning = DateTimeOffset.Now;
                tray?.ShowWarning("價格提醒無法送出：" + ex.Message);
            }
        }
        finally
        {
            var alert = settings.Symbols.FirstOrDefault(s => s.Symbol == price.Symbol)?.Alert;
            if (alert is not null) ticker.SetAlertState(price.Symbol, alert);
            RefreshAlertWindows();
        }
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
        foreach (var window in priceAlertWindows.Values.ToArray()) window.Close();
        await feedGate.WaitAsync();
        try { await StopFeedAsync(); }
        finally { feedGate.Release(); }
        await Task.WhenAll(pendingPriceUpdates.ToArray());
        tray?.Dispose(); tray = null;
        manager?.CloseForExit(); manager?.Dispose(); http.Dispose(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        feedCancellation?.Cancel(); tray?.Dispose(); manager?.Dispose(); http.Dispose(); base.OnExit(e);
    }
}
