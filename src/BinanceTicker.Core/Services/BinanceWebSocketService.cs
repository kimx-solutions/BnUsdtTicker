using System.Net.WebSockets;
using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public interface IMarketSocket : IDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}

public sealed class BinanceWebSocketService
{
    private static readonly int[] RetrySeconds = [1, 2, 5, 10, 30];
    private readonly Func<IMarketSocket> createSocket;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;

    public BinanceWebSocketService(Func<IMarketSocket>? createSocket = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.createSocket = createSocket ?? (() => new MarketSocket());
        this.delay = delay ?? Task.Delay;
    }

    public async Task RunAsync(IReadOnlyList<string> symbols, Action<TickerPrice> onPrice,
        Action<ConnectionStatus> onStatus, CancellationToken cancellationToken, Action<CandlePrice>? onCandle = null)
    {
        var normalized = symbols.Select(SymbolNormalizer.Normalize).Distinct(StringComparer.Ordinal).ToArray();
        if (normalized.Length == 0) { onStatus(ConnectionStatus.Disconnected); return; }
        var batches = normalized.Chunk(onCandle is null ? 1024 : 512).ToArray();
        var states = Enumerable.Repeat(ConnectionStatus.Connecting, batches.Length).ToArray();
        var gate = new object();
        await Task.WhenAll(batches.Select((batch, index) => RunSingleAsync(batch, onPrice, status =>
        {
            ConnectionStatus aggregate;
            lock (gate)
            {
                states[index] = status;
                aggregate = states.Contains(ConnectionStatus.Disconnected) ? ConnectionStatus.Disconnected :
                    states.Contains(ConnectionStatus.Connecting) ? ConnectionStatus.Connecting : ConnectionStatus.Connected;
            }
            onStatus(aggregate);
        }, cancellationToken, onCandle)));
    }

    private async Task RunSingleAsync(IReadOnlyList<string> symbols, Action<TickerPrice> onPrice,
        Action<ConnectionStatus> onStatus, CancellationToken cancellationToken, Action<CandlePrice>? onCandle)
    {
        if (symbols.Count == 0) { onStatus(ConnectionStatus.Disconnected); return; }
        var streams = string.Join('/', symbols.SelectMany(s => onCandle is null ? new[] { s.ToLowerInvariant() + "@ticker" } : new[] { s.ToLowerInvariant() + "@ticker", s.ToLowerInvariant() + "@kline_1m" }));
        var uri = new Uri("wss://stream.binance.com:9443/stream?streams=" + streams);
        var retry = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            onStatus(ConnectionStatus.Connecting);
            try
            {
                using var socket = createSocket();
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await socket.ConnectAsync(uri, timeout.Token);
                }
                onStatus(ConnectionStatus.Connected);
                var buffer = new byte[8192];
                using var message = new MemoryStream();
                while (!cancellationToken.IsCancellationRequested)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(45));
                    var frame = await socket.ReceiveAsync(buffer, timeout.Token);
                    if (frame.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Stream closed");
                    if (frame.MessageType != WebSocketMessageType.Text) continue;
                    message.Write(buffer, 0, frame.Count);
                    if (message.Length > 256 * 1024) throw new WebSocketException("Ticker message too large");
                    if (!frame.EndOfMessage) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(message.GetBuffer().AsMemory(0, (int)message.Length));
                        var data = doc.RootElement.TryGetProperty("data", out var wrapped) ? wrapped : doc.RootElement;
                        if (data.TryGetProperty("e", out var eventType) && eventType.GetString() == "kline")
                        {
                            var candle = CandleParser.ParseStream(data);
                            if (onCandle is not null && symbols.Contains(candle.Symbol, StringComparer.Ordinal))
                            { onCandle(candle); retry = 0; }
                        }
                        else
                        {
                            var price = TickerParser.ParseStream(data);
                            if (symbols.Contains(price.Symbol, StringComparer.Ordinal))
                            { onPrice(price); retry = 0; }
                        }
                    }
                    catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or
                                               InvalidOperationException or OverflowException or ArgumentOutOfRangeException)
                    { /* Ignore non-ticker or malformed events; the next tick can recover. */ }
                    finally { message.SetLength(0); }
                }
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException or OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested) break;
                onStatus(ConnectionStatus.Disconnected);
                try
                {
                    await delay(TimeSpan.FromSeconds(RetrySeconds[Math.Min(retry++, RetrySeconds.Length - 1)]), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            }
        }
    }

    private sealed class MarketSocket : IMarketSocket
    {
        private readonly ClientWebSocket socket = new();
        public MarketSocket()
        {
            socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            socket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(20);
        }
        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => socket.ConnectAsync(uri, cancellationToken);
        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            socket.ReceiveAsync(buffer, cancellationToken);
        public void Dispose() => socket.Dispose();
    }
}
