using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

/// <summary>Shares history work across views and drains each feed generation before reconfiguration.</summary>
public sealed class HistoryCoordinator(IBinanceHistoryService history, CandleCache cache, TimeProvider? timeProvider = null)
{
    private readonly object gate = new();
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private HashSet<string> enabled = [];
    private HashSet<string> demand = [];
    private readonly HashSet<string> refreshNeeded = [];
    private readonly Dictionary<string, DateTimeOffset> recoveryStarts = new();
    private readonly Dictionary<string, HistoryLoadState> states = new();
    private readonly Dictionary<string, Task> tasks = new();
    private CancellationTokenSource lifetime = new();
    private long generation;
    private long connectionRevision;
    private bool accepting;
    private ConnectionStatus connection = ConnectionStatus.Connecting;
    public event Action<string>? Changed;

    public Task ConfigureAsync(IReadOnlyCollection<string> symbols) => ResetAsync(symbols, false);
    public Task StopAsync() => ResetAsync([], true);

    private async Task ResetAsync(IReadOnlyCollection<string> symbols, bool stop)
    {
        var normalized = symbols.Select(SymbolNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);
        await lifecycle.WaitAsync();
        try
        {
            Task[] pending;
            CancellationTokenSource previous;
            lock (gate)
            {
                accepting = false;
                generation++;
                demand.Clear();
                previous = lifetime;
                pending = tasks.Values.ToArray();
            }
            previous.Cancel();
            await Task.WhenAll(pending);
            lock (gate)
            {
                enabled = normalized;
                cache.Configure(normalized);
                foreach (var symbol in states.Keys.Except(normalized).ToArray()) states.Remove(symbol);
                foreach (var symbol in recoveryStarts.Keys.Except(normalized).ToArray()) recoveryStarts.Remove(symbol);
                foreach (var symbol in normalized)
                {
                    var state = states.GetValueOrDefault(symbol);
                    states[symbol] = state is { Status: HistoryLoadStatus.Loaded } ? state : new(HistoryLoadStatus.NotRequested);
                }
                tasks.Clear();
                refreshNeeded.Clear();
                foreach (var symbol in normalized.Where(s => states[s].LastLoadedAt is not null))
                {
                    refreshNeeded.Add(symbol);
                    RememberRecoveryStart(symbol, clock.GetUtcNow());
                }
                lifetime = new();
                accepting = !stop;
                connection = ConnectionStatus.Connecting;
            }
            previous.Dispose();
        }
        finally { lifecycle.Release(); }
    }

    public void SetDemand(IReadOnlyCollection<string> symbols)
    {
        string[] newlyDemanded;
        lock (gate)
        {
            var next = symbols.Select(SymbolNormalizer.Normalize).Where(enabled.Contains).ToHashSet(StringComparer.Ordinal);
            newlyDemanded = next.Except(demand).ToArray();
            demand = next;
        }
        foreach (var symbol in newlyDemanded) _ = EnsureLoadedAsync(symbol);
    }

    public Task EnsureLoadedAsync(string symbol, bool refresh = false)
    {
        symbol = SymbolNormalizer.Normalize(symbol);
        Task task;
        lock (gate)
        {
            if (!accepting || !enabled.Contains(symbol) || !demand.Contains(symbol)) return Task.CompletedTask;
            if (tasks.TryGetValue(symbol, out var current)) return current;
            var state = states.GetValueOrDefault(symbol) ?? new(HistoryLoadStatus.NotRequested);
            if (!refresh && state.Status == HistoryLoadStatus.Loaded && !refreshNeeded.Contains(symbol)) return Task.CompletedTask;
            states[symbol] = state with { Status = HistoryLoadStatus.Loading, Error = null };
            var version = generation;
            var revision = connectionRevision;
            var token = lifetime.Token;
            task = Task.Run(() => LoadAsync(symbol, version, revision, token, refresh));
            tasks[symbol] = task;
        }
        Changed?.Invoke(symbol);
        return task;
    }

    public HistoryLoadState GetState(string symbol)
    {
        lock (gate) return states.GetValueOrDefault(SymbolNormalizer.Normalize(symbol)) ?? new(HistoryLoadStatus.NotRequested);
    }

    public void OnConnectionStatus(ConnectionStatus status)
    {
        string[] needed = [];
        lock (gate)
        {
            if (!accepting) return;
            if (status == ConnectionStatus.Disconnected && connection != status)
            {
                connectionRevision++;
                refreshNeeded.UnionWith(enabled);
                foreach (var symbol in enabled) RememberRecoveryStart(symbol, clock.GetUtcNow());
            }
            connection = status;
            if (status == ConnectionStatus.Connected) needed = demand.Where(refreshNeeded.Contains).ToArray();
        }
        foreach (var symbol in needed) _ = EnsureLoadedAsync(symbol);
    }

    // Called under gate before resumed streaming can move the recovery boundary forward.
    private void RememberRecoveryStart(string symbol, DateTimeOffset now)
    {
        var lastClosed = cache.GetSnapshot(symbol, now).LastOrDefault(c => c.IsClosed);
        var checkpoint = lastClosed?.OpenTime ??
            (states.GetValueOrDefault(symbol)?.LastLoadedAt is { } loaded
                ? DateTimeOffset.FromUnixTimeMilliseconds(loaded.ToUnixTimeMilliseconds() / 60000 * 60000).AddMinutes(-1)
                : (DateTimeOffset?)null);
        if (checkpoint is { } start && (!recoveryStarts.TryGetValue(symbol, out var previous) || start < previous))
            recoveryStarts[symbol] = start;
    }

    private async Task LoadAsync(string symbol, long version, long revision, CancellationToken token, bool fullRefresh)
    {
        var changed = false;
        try
        {
            var end = clock.GetUtcNow();
            var start = DateTimeOffset.FromUnixTimeMilliseconds(end.AddHours(-24).ToUnixTimeMilliseconds() / 60000 * 60000).AddMinutes(-1);
            lock (gate)
            {
                if (version != generation || token.IsCancellationRequested) return;
                if (!fullRefresh && states[symbol].LastLoadedAt is not null && recoveryStarts.TryGetValue(symbol, out var recovery))
                {
                    if (recovery > start) start = recovery;
                }
            }
            var candles = await history.GetCandlesAsync(symbol, start, end, token);
            lock (gate)
            {
                if (version != generation || token.IsCancellationRequested || !enabled.Contains(symbol)) return;
                foreach (var candle in candles) cache.Merge(candle, clock.GetUtcNow());
                states[symbol] = new(HistoryLoadStatus.Loaded, LastLoadedAt: end);
                if (revision == connectionRevision)
                {
                    refreshNeeded.Remove(symbol);
                    recoveryStarts.Remove(symbol);
                }
                changed = true;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or IOException or FormatException)
        {
            lock (gate)
            {
                if (version != generation || token.IsCancellationRequested) return;
                states[symbol] = new(HistoryLoadStatus.Failed, "歷史資料載入失敗，可稍後重試。", states[symbol].LastLoadedAt);
                changed = true;
            }
        }
        finally
        {
            var reload = false;
            lock (gate)
            {
                if (version == generation)
                {
                    tasks.Remove(symbol);
                    reload = accepting && revision != connectionRevision && connection == ConnectionStatus.Connected &&
                        demand.Contains(symbol) && refreshNeeded.Contains(symbol);
                }
            }
            if (changed) Changed?.Invoke(symbol);
            if (reload) _ = EnsureLoadedAsync(symbol);
        }
    }
}
