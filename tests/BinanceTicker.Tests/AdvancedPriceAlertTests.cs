using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class AdvancedPriceAlertTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private DateTimeOffset now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private readonly AppSettings settings = new() { Symbols = [new() { Symbol = "BTCUSDT", Alert = new() { UpperPrice = 100 } }] };
    private readonly Sink sink = new();
    private SettingsService Store => new(Path.Combine(directory, "settings.json"));
    private AlertHistoryService History => new(Path.Combine(directory, "history.json"));
    private PriceAlertService Create() => new(settings, Store, History, sink, () => now);
    private TickerPrice Quote(decimal price) => new("BTCUSDT", price, 0, now.UtcDateTime, Source: QuoteSource.Stream);

    [Fact]
    public async Task RepeatRequiresReturningOutsideAndAnotherCrossing()
    {
        settings.Symbols[0].Alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var service = Create();
        foreach (var price in new[] { 101m, 102m, 100m, 99m, 100m, 101m }) await service.CheckAsync("BTCUSDT", price);
        Assert.Equal(2, History.Load().Count);
        Assert.Equal(2, sink.Entries.Count);
        Assert.Equal(now, Store.Load().Symbols[0].Alert.UpperPolicy.LastTriggeredAt);
    }

    [Fact]
    public async Task CooldownCrossingIsConsumedAndNeverDeliveredLate()
    {
        var policy = settings.Symbols[0].Alert.UpperPolicy;
        policy.Strategy = AlertStrategy.Repeat; policy.CooldownMinutes = 2;
        var service = Create();
        await service.CheckAsync("BTCUSDT", 101);
        now = now.AddMinutes(1);
        await service.CheckAsync("BTCUSDT", 99); await service.CheckAsync("BTCUSDT", 101);
        now = now.AddMinutes(1);
        await service.CheckAsync("BTCUSDT", 101);
        Assert.Single(History.Load());
        await service.CheckAsync("BTCUSDT", 99); await service.CheckAsync("BTCUSDT", 100);
        Assert.Equal(2, History.Load().Count);
    }

    [Fact]
    public async Task RestartRequiresOutsideObservationEvenIfArmedWasSaved()
    {
        settings.Symbols[0].Alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var service = Create();
        await service.CheckAsync("BTCUSDT", 101); await service.CheckAsync("BTCUSDT", 99);
        var restarted = new PriceAlertService(Store.Load(), Store, History, sink, () => now);
        await restarted.CheckAsync("BTCUSDT", 101);
        Assert.Single(History.Load());
        await restarted.CheckAsync("BTCUSDT", 99); await restarted.CheckAsync("BTCUSDT", 101);
        Assert.Equal(2, History.Load().Count);
    }

    [Theory]
    [InlineData(AlertType.Rise, 102)]
    [InlineData(AlertType.Fall, 98)]
    public async Task ShortTermBoundaryRecordsCompleteComparison(AlertType type, decimal price)
    {
        settings.Symbols[0].Alert.UpperPrice = null;
        var condition = type == AlertType.Rise ? settings.Symbols[0].Alert.Rise : settings.Symbols[0].Alert.Fall;
        condition.ThresholdPercent = 2; condition.WindowMinutes = 1;
        var service = Create();
        for (var i = 0; i <= 60; i++)
        {
            await service.CheckQuoteAsync(Quote(i == 60 ? price : 100));
            if (i < 60) Assert.Empty(History.Load());
            now = now.AddSeconds(1);
        }
        var entry = Assert.Single(History.Load());
        Assert.Equal(type, entry.AlertType); Assert.Null(entry.TargetPrice);
        Assert.Equal(100m, entry.BaselinePrice); Assert.Equal(price - 100, entry.ChangePercent);
        Assert.Equal(1, entry.WindowMinutes); Assert.Equal(2m, entry.ThresholdPercent);
        Assert.Equal(TimeSpan.FromMinutes(1), entry.QuoteAt - entry.BaselineAt);
    }

    [Fact]
    public async Task RestAndDisconnectCannotSupplyShortTermBaseline()
    {
        settings.Symbols[0].Alert.UpperPrice = null;
        settings.Symbols[0].Alert.Fall = new() { ThresholdPercent = 2, WindowMinutes = 1 };
        var service = Create();
        await service.CheckQuoteAsync(Quote(100) with { Source = QuoteSource.Rest });
        for (var i = 1; i <= 60; i++) { now = now.AddSeconds(1); await service.CheckQuoteAsync(Quote(98)); }
        Assert.Empty(History.Load());
        await service.OnConnectionStatusAsync(ConnectionStatus.Disconnected);
        now = now.AddSeconds(1); await service.CheckQuoteAsync(Quote(95));
        Assert.Empty(History.Load());
    }

    [Fact]
    public async Task FailureRestoresCooldownAndArmingThenRetries()
    {
        settings.Symbols[0].Alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var service = Create();
        await service.CheckAsync("BTCUSDT", 101);
        var first = now;
        now = now.AddMinutes(3); await service.CheckAsync("BTCUSDT", 99);
        sink.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckAsync("BTCUSDT", 101));
        var restored = Store.Load().Symbols[0].Alert;
        Assert.True(restored.UpperTriggered); Assert.True(restored.UpperPolicy.Armed);
        Assert.Equal(first, restored.UpperPolicy.LastTriggeredAt); Assert.Single(History.Load());
        sink.Fail = false; await service.CheckAsync("BTCUSDT", 101);
        Assert.Equal(2, History.Load().Count);
    }

    [Fact]
    public async Task HighFrequencyConcurrentUpdatesProduceOneSubmission()
    {
        settings.Symbols[0].Alert.UpperPolicy.Strategy = AlertStrategy.Repeat;
        var service = Create();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => service.CheckAsync("BTCUSDT", 101)));
        Assert.Single(History.Load());
    }

    private sealed class Sink : INotificationService
    {
        public bool Fail;
        public List<AlertHistoryEntry> Entries { get; } = [];
        public void Show(AlertHistoryEntry entry) { if (Fail) throw new InvalidOperationException("rejected"); Entries.Add(entry); }
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
