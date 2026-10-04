using System.Net.WebSockets;
using System.Text;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class CandleStreamTests
{
    [Fact]
    public async Task RoutesFragmentedTickerAndCandleMessages()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var json = CandleFixtures.StreamJson(CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1)));
        var socket = new Socket([("""{"s":"BTCUSDT","c":"100","P":"0","E":1800000000000}""", true), (json[..20], false), (json[20..], true)]);
        var prices = new List<TickerPrice>(); var candles = new List<CandlePrice>();
        void Stop() { if (prices.Count == 1 && candles.Count == 1) cts.Cancel(); }
        await Run(new(() => socket), ["BTCUSDT"], p => { prices.Add(p); Stop(); }, _ => { }, cts.Token, c => { candles.Add(c); Stop(); });
        Assert.Single(prices); Assert.Single(candles);
        Assert.Contains("btcusdt@ticker", socket.Uri!.Query); Assert.Contains("btcusdt@kline_1m", socket.Uri.Query);
    }
    [Fact]
    public async Task CombinedStreamsStayWithin1024Subscriptions()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var uris = new List<Uri>(); var statuses = new List<ConnectionStatus>();
        var service = new BinanceWebSocketService(() => new Socket([], uri => { lock (uris) { uris.Add(uri); if (uris.Count == 2) cts.Cancel(); } }));
        await Run(service, Enumerable.Range(0, 513).Select(i => $"S{i}USDT").ToArray(), _ => { }, status => { lock (statuses) statuses.Add(status); }, cts.Token, _ => { });
        Assert.Equal(2, uris.Count);
        Assert.All(uris, uri => Assert.InRange(uri.Query.Split('/').Length, 1, 1024));
        Assert.Contains(ConnectionStatus.Connected, statuses);
    }
    private static Task Run(BinanceWebSocketService service, IReadOnlyList<string> symbols, Action<TickerPrice> price, Action<ConnectionStatus> status, CancellationToken token, Action<CandlePrice> candle)
    {
        var method = typeof(BinanceWebSocketService).GetMethods().SingleOrDefault(m => m.Name == "RunAsync" && m.GetParameters().Length == 5);
        Assert.NotNull(method); return (Task)method.Invoke(service, [symbols, price, status, token, candle])!;
    }
    private sealed class Socket(IEnumerable<(string, bool)> frames, Action<Uri>? connected = null) : IMarketSocket
    {
        private readonly Queue<(string, bool)> queue = new(frames);
        public Uri? Uri { get; private set; }
        public Task ConnectAsync(Uri uri, CancellationToken token) { Uri = uri; connected?.Invoke(uri); return Task.CompletedTask; }
        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (queue.TryDequeue(out var frame))
            { var bytes = Encoding.UTF8.GetBytes(frame.Item1); bytes.CopyTo(buffer); return new(bytes.Length, WebSocketMessageType.Text, frame.Item2); }
            await Task.Delay(Timeout.Infinite, token); return default;
        }
        public void Dispose() { }
    }
}
