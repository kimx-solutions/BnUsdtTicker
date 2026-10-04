using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class AdvancedAlertRecoveryTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private DateTimeOffset now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private readonly AppSettings settings = new() { Symbols = [new() { Symbol = "BTCUSDT" }, new() { Symbol = "ETHUSDT" }] };
    private SettingsService Store => new(Path.Combine(directory, "settings.json"));
    private AlertHistoryService History => new(Path.Combine(directory, "history.json"));
    private readonly Sink sink = new();
    private PriceAlertService Create() => new(settings, Store, History, sink, () => now);
    private TickerPrice Quote(string symbol, decimal price) => new(symbol, price, 0, now.UtcDateTime, Source: QuoteSource.Stream);

    [Fact]
    public async Task GeneralAndAlertDraftSavesPreserveLiveCooldownAndExplicitResetClearsIt()
    {
        var alert = settings.Symbols[0].Alert;
        alert.UpperPrice = 100; alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var draft = settings.Copy(); var alertDraft = alert.Copy();
        var service = Create(); await service.CheckAsync("BTCUSDT", 101);
        alertDraft.UpperPrice = 105;
        await service.SaveAlertAsync("BTCUSDT", alertDraft, []);
        Assert.Equal(now, Store.Load().Symbols[0].Alert.UpperPolicy.LastTriggeredAt);
        await service.ApplySettingsAsync(draft, [], preserveAlerts: true);
        var saved = Store.Load().Symbols[0].Alert;
        Assert.Equal(105m, saved.UpperPrice); Assert.True(saved.UpperTriggered);
        Assert.Equal(now, saved.UpperPolicy.LastTriggeredAt);
        await service.ResetAsync("BTCUSDT", AlertType.Upper);
        saved = Store.Load().Symbols[0].Alert;
        Assert.Null(saved.UpperPolicy.LastTriggeredAt); Assert.False(saved.UpperTriggered); Assert.True(saved.UpperPolicy.Armed);
    }

    [Fact]
    public async Task ClockRollbackCannotReleaseCooldown()
    {
        var alert = settings.Symbols[0].Alert;
        alert.UpperPrice = 100; alert.UpperPolicy.Strategy = AlertStrategy.Repeat; alert.UpperPolicy.CooldownMinutes = 2;
        var service = Create(); await service.CheckAsync("BTCUSDT", 101); await service.CheckAsync("BTCUSDT", 99);
        now = now.AddMinutes(-1); await service.CheckAsync("BTCUSDT", 101);
        Assert.Single(History.Load());
        now = now.AddMinutes(4); await service.CheckAsync("BTCUSDT", 101);
        Assert.Single(History.Load());
        await service.CheckAsync("BTCUSDT", 99); await service.CheckAsync("BTCUSDT", 101);
        Assert.Equal(2, History.Load().Count);
    }

    [Fact]
    public async Task FailedRearmingSaveDoesNotClaimItWasPersisted()
    {
        var alert = settings.Symbols[0].Alert;
        alert.UpperPrice = 100; alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var service = Create(); await service.CheckAsync("BTCUSDT", 101);
        File.SetAttributes(Store.FilePath, FileAttributes.ReadOnly);
        try
        {
            Assert.NotNull(await Record.ExceptionAsync(() => service.CheckAsync("BTCUSDT", 99)));
            Assert.False(settings.Symbols[0].Alert.UpperPolicy.Armed);
            await service.CheckAsync("BTCUSDT", 101); Assert.Single(History.Load());
        }
        finally { File.SetAttributes(Store.FilePath, FileAttributes.Normal); }
        await service.CheckAsync("BTCUSDT", 99); await service.CheckAsync("BTCUSDT", 101);
        Assert.Equal(2, History.Load().Count);
    }

    [Fact]
    public async Task GapAndReconnectRequireNewFullWindowButOtherSymbolsKeepWorking()
    {
        foreach (var item in settings.Symbols) item.Alert.Fall = new() { ThresholdPercent = 2, WindowMinutes = 1 };
        var service = Create();
        for (var i = 0; i <= 66; i++)
        {
            if (i == 0 || i >= 6) await service.CheckQuoteAsync(Quote("BTCUSDT", i >= 60 ? 98 : 100));
            await service.CheckQuoteAsync(Quote("ETHUSDT", i >= 60 ? 98 : 100));
            if (i == 60) { Assert.Single(History.Load()); Assert.Equal("ETHUSDT", History.Load()[0].Symbol); }
            now = now.AddSeconds(1);
        }
        Assert.Equal(2, History.Load().Count);
        await service.ResetAsync("BTCUSDT", AlertType.Fall);
        await service.OnConnectionStatusAsync(ConnectionStatus.Connecting);
        await service.CheckQuoteAsync(Quote("BTCUSDT", 90));
        Assert.Equal(2, History.Load().Count);
    }

    [Fact]
    public async Task ChangingWindowWaitsForNewFullInterval()
    {
        settings.Symbols[0].Alert.Rise = new() { ThresholdPercent = 50, WindowMinutes = 1 };
        var service = Create();
        for (var i = 0; i < 60; i++) { await service.CheckQuoteAsync(Quote("BTCUSDT", 100)); now = now.AddSeconds(1); }
        var draft = settings.Symbols[0].Alert.Copy(); draft.Rise.ThresholdPercent = 2;
        await service.SaveAlertAsync("BTCUSDT", draft, []);
        await service.CheckQuoteAsync(Quote("BTCUSDT", 102)); Assert.Empty(History.Load());
        for (var i = 1; i <= 60; i++)
        {
            now = now.AddSeconds(1); await service.CheckQuoteAsync(Quote("BTCUSDT", i == 60 ? 105 : 102));
        }
        Assert.Single(History.Load()); Assert.Equal(102m, History.Load()[0].BaselinePrice);
    }

    [Fact]
    public async Task HistoryFailureAndRetryPreserveRepeatStateAndEventOnlyReportsSuccess()
    {
        var alert = settings.Symbols[0].Alert;
        alert.UpperPrice = 100; alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var service = Create(); var submitted = 0; service.Submitted += _ => submitted++;
        Directory.CreateDirectory(History.FilePath);
        Assert.NotNull(await Record.ExceptionAsync(() => service.CheckAsync("BTCUSDT", 101)));
        Assert.Null(settings.Symbols[0].Alert.UpperPolicy.LastTriggeredAt);
        Assert.True(settings.Symbols[0].Alert.UpperPolicy.Armed); Assert.Equal(0, submitted);
        Directory.Delete(History.FilePath); await service.CheckAsync("BTCUSDT", 101);
        Assert.Single(History.Load()); Assert.Equal(1, submitted);
        service.Submitted += _ => throw new InvalidOperationException("UI failure");
        await service.CheckAsync("BTCUSDT", 99); await service.CheckAsync("BTCUSDT", 101);
        Assert.Equal(2, History.Load().Count);
    }

    private sealed class Sink : INotificationService { public void Show(AlertHistoryEntry entry) { } }
    public void Dispose()
    {
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.GetFiles(directory)) File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(directory, true);
    }
}
