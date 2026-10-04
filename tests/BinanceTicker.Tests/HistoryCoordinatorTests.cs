using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class HistoryCoordinatorTests
{
    [Fact]
    public async Task ConcurrentDemandUsesOneHistoryTask()
    {
        var release = new TaskCompletionSource<IReadOnlyList<CandlePrice>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var history = new History((_, _, _, token) => release.Task.WaitAsync(token));
        dynamic coordinator = NewCoordinator(history, new());
        await (Task)coordinator.ConfigureAsync(new[] { "BTCUSDT" });
        coordinator.SetDemand(new[] { "BTCUSDT" });
        var first = (Task)coordinator.EnsureLoadedAsync("BTCUSDT", false);
        var second = (Task)coordinator.EnsureLoadedAsync("BTCUSDT", false);
        Assert.Same(first, second);
        await history.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        release.SetResult([CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1))]);
        await first;
        Assert.Equal(1, history.Calls); Assert.Equal("Loaded", (string)coordinator.GetState("BTCUSDT").Status.ToString());
        await (Task)coordinator.StopAsync();
    }
    [Fact]
    public async Task ReconnectFillsGapsOnlyForDemandedSymbols()
    {
        var starts = new List<DateTimeOffset>();
        var history = new History((_, start, _, _) => { starts.Add(start); return Task.FromResult<IReadOnlyList<CandlePrice>>([CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1))]); });
        dynamic coordinator = NewCoordinator(history, new());
        await (Task)coordinator.ConfigureAsync(new[] { "BTCUSDT" }); coordinator.SetDemand(new[] { "BTCUSDT" });
        await (Task)coordinator.EnsureLoadedAsync("BTCUSDT", false);
        coordinator.SetDemand(Array.Empty<string>());
        coordinator.OnConnectionStatus(ConnectionStatus.Disconnected); coordinator.OnConnectionStatus(ConnectionStatus.Connecting); coordinator.OnConnectionStatus(ConnectionStatus.Connected);
        Assert.Equal(1, history.Calls);
        coordinator.SetDemand(new[] { "BTCUSDT" }); await (Task)coordinator.EnsureLoadedAsync("BTCUSDT", false);
        Assert.Equal(2, history.Calls); Assert.Equal(CandleFixtures.Now.AddMinutes(-1), starts[1]);
        await (Task)coordinator.StopAsync();
    }
    [Fact]
    public async Task LateGenerationCannotRecreateRemovedSymbol()
    {
        var release = new TaskCompletionSource<IReadOnlyList<CandlePrice>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new CandleCache(); var history = new History((_, _, _, _) => release.Task);
        dynamic coordinator = NewCoordinator(history, cache);
        await (Task)coordinator.ConfigureAsync(new[] { "BTCUSDT" }); coordinator.SetDemand(new[] { "BTCUSDT" });
        await history.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var reset = (Task)coordinator.ConfigureAsync(Array.Empty<string>());
        Assert.False(reset.IsCompleted);
        release.SetResult([CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1))]); await reset;
        Assert.Empty(cache.GetSnapshot("BTCUSDT", CandleFixtures.Now));
        await (Task)coordinator.StopAsync();
    }
    [Fact]
    public async Task ClosingOneViewKeepsSharedWorkAlive()
    {
        var release = new TaskCompletionSource<IReadOnlyList<CandlePrice>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken runningToken = default;
        var history = new History((_, _, _, token) => { runningToken = token; return release.Task.WaitAsync(token); });
        dynamic coordinator = NewCoordinator(history, new());
        await (Task)coordinator.ConfigureAsync(new[] { "BTCUSDT" }); coordinator.SetDemand(new[] { "BTCUSDT", "BTCUSDT" });
        await history.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        coordinator.SetDemand(new[] { "BTCUSDT" });
        Assert.False(runningToken.IsCancellationRequested); Assert.Equal(1, history.Calls);
        release.SetResult([]); await (Task)coordinator.EnsureLoadedAsync("BTCUSDT", false);
        await (Task)coordinator.StopAsync();
    }
    [Fact]
    public async Task StopCancelsAndDrainsOutstandingWork()
    {
        var drained = false; CancellationToken runningToken = default;
        var history = new History(async (_, _, _, token) => { runningToken = token; try { await Task.Delay(Timeout.Infinite, token); return []; } finally { drained = true; } });
        dynamic coordinator = NewCoordinator(history, new());
        await (Task)coordinator.ConfigureAsync(new[] { "BTCUSDT" }); coordinator.SetDemand(new[] { "BTCUSDT" });
        await history.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await ((Task)coordinator.StopAsync()).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(runningToken.IsCancellationRequested); Assert.True(drained);
    }
    [Fact]
    public async Task RecoverableHistoryFailureDoesNotEscapeOrDiscardCache()
    {
        var cache = new CandleCache(); var history = new History((_, _, _, _) => throw new HttpRequestException("offline"));
        dynamic coordinator = NewCoordinator(history, cache);
        await (Task)coordinator.ConfigureAsync(new[] { "BTCUSDT" });
        cache.Merge(CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1)), CandleFixtures.Now);
        coordinator.SetDemand(new[] { "BTCUSDT" }); await (Task)coordinator.EnsureLoadedAsync("BTCUSDT", false);
        Assert.Equal("Failed", (string)coordinator.GetState("BTCUSDT").Status.ToString());
        Assert.Single(cache.GetSnapshot("BTCUSDT", CandleFixtures.Now));
        await (Task)coordinator.StopAsync();
    }
    private static object NewCoordinator(IBinanceHistoryService history, CandleCache cache)
    {
        var type = typeof(TickerParser).Assembly.GetType("BinanceTicker.Core.Services.HistoryCoordinator");
        Assert.NotNull(type); return Activator.CreateInstance(type, history, cache, new TestTimeProvider(CandleFixtures.Now))!;
    }
    private sealed class History(Func<string, DateTimeOffset, DateTimeOffset, CancellationToken, Task<IReadOnlyList<CandlePrice>>> load) : IBinanceHistoryService
    {
        public int Calls;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<CandlePrice>> GetCandlesAsync(string symbol, DateTimeOffset start, DateTimeOffset end, CancellationToken token)
        { Interlocked.Increment(ref Calls); Entered.TrySetResult(); return load(symbol, start, end, token); }
    }
}
