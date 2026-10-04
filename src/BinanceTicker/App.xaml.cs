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
    private readonly CandleCache candleCache = new();
    private HistoryCoordinator? marketHistory;
    private MarketDetailsWindowManager? marketDetails;
    private readonly IBrowserLauncher browser = new BrowserLauncher();
    private System.Windows.Threading.DispatcherTimer? graphTimer;
    private TickerWindow? tickerWindow;
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
    private AlertHistoryWindowManager? alertHistory;
    private PriceAlertService? submittingAlerts;
    private readonly HashSet<Task> pendingPriceUpdates = [];
    private DateTimeOffset lastAlertWarning = DateTimeOffset.MinValue;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new();
        if (!instance.IsFirstInstance) { Shutdown(); return; }
        try { settings = settingsService.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show("無法讀取設定，將使用預設值。\n" + ex.Message, "Binance Ticker"); }
        binance = new(http);
        ThemeService.Apply(settings.Ui.Theme);
        ticker.Configure(settings);
        var window = new TickerWindow { DataContext = ticker };
        manager = new(window, settings, SaveSettings);
        InitializeMarketVisualization(window);
        window.SettingsRequested += OpenSettings;
        window.PriceAlertRequested += OpenPriceAlert;
        window.ThemeRequested += ToggleTheme;
        tray = new(Dispatcher, manager.Show, OpenSettings, ChangeMode, () => _ = ExitAsync(), OpenAlertHistory, manager.ResetSize);
        InitializeDesktopPreferences(window);
        var history = new AlertHistoryService();
        alertHistory = new(history);
        submittingAlerts = new PriceAlertService(settings, settingsService, history, new NotificationService(tray));
        submittingAlerts.Submitted += OnAlertSubmitted;
        alerts = submittingAlerts;
        tray.SetMode(settings.Mode);
        if (settings.ShowOnStartup) manager.Show();
        if (settingsService.LoadWarning is { } warning) tray.ShowWarning(warning);
        await RestartFeedAsync();
    }
    private void InitializeMarketVisualization(TickerWindow window)
    {
        tickerWindow = window;
        marketHistory ??= new(new BinanceHistoryService(new HistoryRequestScheduler(http)), candleCache);
        marketDetails = new(symbol =>
        {
            var row = ticker.Prices.FirstOrDefault(r => r.Symbol == symbol);
            return row is null ? null : new(row, () => marketHistory.EnsureLoadedAsync(symbol, true), browser.Open);
        });
        marketDetails.OpenSymbolsChanged += UpdateHistoryDemand;
        window.MarketDetailsRequested += OpenMarketDetails;
        window.IsVisibleChanged += MarketVisibilityChanged;
        ticker.SparklinePreferencesChanged += SaveSparklinePreferences;
        graphTimer = new(TimeSpan.FromSeconds(1), System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => RefreshMarketGraphs(), Dispatcher);
        graphTimer.Stop();
    }
    private void OpenMarketDetails(string symbol) { if (!exiting) marketDetails?.Show(symbol); }
    private void MarketVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateHistoryDemand();
    private void SaveSparklinePreferences()
    {
        if (exiting) return;
        settings.Ui.ShowSparkline = ticker.ShowSparkline;
        settings.Ui.SparklineRange = ticker.SparklineRange;
        SaveSettings();
        UpdateHistoryDemand();
        RefreshMarketGraphs();
    }
    private string[] GetHistoryDemand()
    {
        var main = tickerWindow?.IsVisible == true && ticker.ShowSparkline
            ? ticker.Prices.Select(r => r.Symbol) : Enumerable.Empty<string>();
        var details = marketDetails?.OpenSymbols ?? Array.Empty<string>();
        var enabled = settings.Symbols.Where(s => s.Enabled).Select(s => s.Symbol).ToHashSet(StringComparer.Ordinal);
        return main.Concat(details).Where(enabled.Contains).Distinct(StringComparer.Ordinal).ToArray();
    }
    private void UpdateHistoryDemand()
    {
        if (exiting || marketHistory is null) return;
        var demanded = GetHistoryDemand();
        marketHistory.SetDemand(demanded);
        if (demanded.Length > 0) graphTimer?.Start(); else graphTimer?.Stop();
    }
    private void RefreshMarketGraphs()
    {
        if (exiting || marketHistory is null) return;
        var now = DateTimeOffset.UtcNow;
        var demanded = GetHistoryDemand().ToHashSet(StringComparer.Ordinal);
        foreach (var row in ticker.Prices.Where(r => demanded.Contains(r.Symbol)))
            row.UpdateHistory(candleCache.GetSnapshot(row.Symbol, now), marketHistory.GetState(row.Symbol),
                ticker.Status, ticker.SparklineRange, now);
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
        marketDetails?.RefreshTheme();
        alertHistory?.RefreshTheme();
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
    private void OpenAlertHistory() { if (!exiting) alertHistory?.Show(); }
    private void OnAlertSubmitted(AlertHistoryEntry entry) => Dispatcher.BeginInvoke(() => { if (!exiting) alertHistory?.Refresh(); });
    private async Task<bool> ApplySettingsAsync(AppSettings updated)
    {
        if (exiting) return false;
        manager?.SavePosition();
        updated.Window.Left = settings.Window.Left; updated.Window.Top = settings.Window.Top;
        updated.Window.Width = settings.Window.Width; updated.Window.Height = settings.Window.Height;
        // A settings window opened before a theme switch must preserve the latest theme.
        updated.Ui.Theme = settings.Ui.Theme;
        settingsWindow?.ViewModel.PreserveUneditedSparklinePreferences(updated, settings);
        try
        {
            using var desktopChange = desktopPreferences?.Prepare(updated, updated.StartWithWindows || updated.StartWithWindows != settings.StartWithWindows);
            await alerts.ApplySettingsAsync(updated, [], preserveAlerts: true);
            desktopChange?.Commit();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.Security.SecurityException or AggregateException)
        { if (settingsWindow is not null) settingsWindow.ViewModel.Error = "儲存失敗：" + ex.Message; return false; }
        settings = updated;
        foreach (var symbol in priceAlertWindows.Keys.ToArray())
            if (!settings.Symbols.Any(s => s.Symbol == symbol)) priceAlertWindows[symbol].Close();
        RefreshAlertWindows();
        marketDetails?.CloseUnavailable(settings.Symbols.Where(s => s.Enabled).Select(s => s.Symbol).ToArray());
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
        window.HistoryRequested += OpenAlertHistory;
        priceAlertWindows.Add(symbol, window);
        window.Closed += (_, _) => { window.HistoryRequested -= OpenAlertHistory; priceAlertWindows.Remove(symbol); };
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
            if (marketHistory is not null) await marketHistory.ConfigureAsync(symbols);
            if (exiting) return;
            ticker.SetStatus(ConnectionStatus.Connecting);
            var feed = new MarketFeed(binance, new());
            feedTask = Task.Run(() => feed.RunAsync(symbols, p => OnUi(() => QueuePriceUpdate(p)), status => OnUi(() =>
            {
                ticker.SetStatus(status);
                marketHistory?.OnConnectionStatus(status);
                QueueAlertConnectionStatus(status);
            }), token, candle => OnUi(() => candleCache.Merge(candle, DateTimeOffset.UtcNow))));
            UpdateHistoryDemand();
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
    private async void QueueAlertConnectionStatus(ConnectionStatus status)
    {
        var task = alerts.OnConnectionStatusAsync(status);
        pendingPriceUpdates.Add(task);
        try { await task; RefreshAlertWindows(); }
        finally { pendingPriceUpdates.Remove(task); }
    }

    private async Task UpdatePriceAsync(TickerPrice price)
    {
        // Ignore quotes older than the row's last accepted timestamp, including reconnect snapshots.
        if (!ticker.Update(price)) return;
        try { await alerts.CheckQuoteAsync(price); }
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
        if (alerts is not null) await alerts.OnConnectionStatusAsync(ConnectionStatus.Disconnected);
    }
    private async Task ExitAsync()
    {
        if (exiting) return;
        exiting = true;
        hotkeys?.Dispose();
        graphTimer?.Stop();
        ticker.SparklinePreferencesChanged -= SaveSparklinePreferences;
        if (tickerWindow is not null)
        {
            tickerWindow.MarketDetailsRequested -= OpenMarketDetails;
            tickerWindow.IsVisibleChanged -= MarketVisibilityChanged;
        }
        if (marketDetails is not null) marketDetails.OpenSymbolsChanged -= UpdateHistoryDemand;
        marketDetails?.CloseAll();
        alertHistory?.Close();
        if (submittingAlerts is not null) submittingAlerts.Submitted -= OnAlertSubmitted;
        manager?.SavePosition(); settingsWindow?.Close();
        foreach (var window in priceAlertWindows.Values.ToArray()) window.Close();
        await feedGate.WaitAsync();
        try
        {
            await StopFeedAsync();
            if (marketHistory is not null) await marketHistory.StopAsync();
        }
        finally { feedGate.Release(); }
        await Task.WhenAll(pendingPriceUpdates.ToArray());
        tray?.Dispose(); tray = null;
        manager?.CloseForExit(); manager?.Dispose(); http.Dispose(); Shutdown();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        graphTimer?.Stop(); feedCancellation?.Cancel(); hotkeys?.Dispose(); instance?.Dispose(); tray?.Dispose(); manager?.Dispose(); http.Dispose(); base.OnExit(e);
    }
}
