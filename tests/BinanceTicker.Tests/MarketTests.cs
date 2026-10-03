using System.Net;
using System.Net.WebSockets;
using System.Text;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class MarketTests
{
    [Fact]
    public async Task RestRequestsSpecificSymbolsAndParsesInvariantDecimals()
    {
        var handler = new ResponseHandler(request =>
        {
            Assert.Equal("/api/v3/ticker/24hr", request.RequestUri!.AbsolutePath);
            Assert.Contains("[\"BTCUSDT\",\"ENAUSDT\"]", Uri.UnescapeDataString(request.RequestUri.Query));
            return new(HttpStatusCode.OK) { Content = new StringContent("""
                [{"symbol":"BTCUSDT","lastPrice":"82351.20","priceChangePercent":"2.31","closeTime":1700000000000},
                 {"symbol":"ENAUSDT","lastPrice":"0.582","priceChangePercent":"-0.61","closeTime":1700000000000}]
                """) };
        });
        var prices = await new BinanceService(new HttpClient(handler)).GetPricesAsync(["BTCUSDT", "ENAUSDT"], default);
        Assert.Equal(2, prices.Count);
        Assert.Equal(0.582m, prices[1].Price);
        Assert.Equal(-0.61m, prices[1].ChangePercent24h);
        Assert.Equal(DateTimeKind.Utc, prices[0].UpdatedAt.Kind);
    }

    [Theory]
    [InlineData("TRADING", "USDT", true)]
    [InlineData("BREAK", "USDT", false)]
    [InlineData("TRADING", "BTC", false)]
    public async Task ValidationRequiresTradingUsdtPair(string status, string quote, bool expected)
    {
        var handler = new ResponseHandler(_ => new(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""{"symbols":[{"symbol":"BTCUSDT","status":"{{status}}","quoteAsset":"{{quote}}","isSpotTradingAllowed":true}]}""")
        });
        Assert.Equal(expected, await new BinanceService(new HttpClient(handler)).IsValidSymbolAsync("BTCUSDT", default));
    }

    [Fact]
    public async Task InvalidSymbolErrorReturnsFalseButNetworkErrorsPropagate()
    {
        var invalid = new BinanceService(new HttpClient(new ResponseHandler(_ => new(HttpStatusCode.BadRequest)
        { Content = new StringContent("{\"code\":-1121,\"msg\":\"Invalid symbol.\"}") })));
        Assert.False(await invalid.IsValidSymbolAsync("ZZZUSDT", default));
        var offline = new BinanceService(new HttpClient(new ResponseHandler(_ => new(HttpStatusCode.ServiceUnavailable))));
        await Assert.ThrowsAsync<HttpRequestException>(() => offline.IsValidSymbolAsync("BTCUSDT", default));
    }

    [Fact]
    public async Task SocketCombinesStreamsAndAssemblesFragments()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var socket = new ScriptedSocket([
            ("{\"stream\":\"btcusdt@ticker\",\"data\":{\"s\":\"BTCUSDT\",", false),
            ("\"c\":\"82351.20\",\"P\":\"2.31\",\"E\":1700000000000}}", true)
        ]);
        TickerPrice? received = null;
        var service = new BinanceWebSocketService(() => socket);
        await service.RunAsync(["BTCUSDT", "ETHUSDT"], price => { received = price; cts.Cancel(); }, _ => { }, cts.Token);
        Assert.Equal("/stream?streams=btcusdt@ticker/ethusdt@ticker", socket.Uri!.PathAndQuery);
        Assert.Equal(82351.20m, received!.Price);
        Assert.Equal(2.31m, received.ChangePercent24h);
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task SocketRetriesWithBoundedDelaysAndResetsAfterData()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var attempts = 0;
        var delays = new List<double>();
        var service = new BinanceWebSocketService(
            () => ++attempts == 7 ? new ScriptedSocket([("{\"s\":\"BTCUSDT\",\"c\":\"1\",\"P\":\"0\",\"E\":1700000000000}", true)]) : new ScriptedSocket([]),
            (delay, _) => { delays.Add(delay.TotalSeconds); if (delays.Count == 7) cts.Cancel(); return Task.CompletedTask; });
        await service.RunAsync(["BTCUSDT"], _ => { }, _ => { }, cts.Token);
        Assert.Equal(new double[] { 1, 2, 5, 10, 30, 30, 1 }, delays);
    }

    [Fact]
    public async Task EmptyWatchlistDoesNotOpenSocket()
    {
        var service = new BinanceWebSocketService(() => throw new InvalidOperationException("Must not connect"));
        await service.RunAsync([], _ => throw new Exception(), _ => { }, default);
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    [Theory]
    [InlineData("{\"symbol\":\"BTCUSDT\",\"lastPrice\":null,\"priceChangePercent\":\"2\",\"closeTime\":1700000000000}")]
    [InlineData("{\"symbol\":\"BTCUSDT\",\"lastPrice\":\"1\",\"priceChangePercent\":\"2\",\"closeTime\":9999999999999999}")]
    [InlineData("{\"symbol\":\"BTCUSDT\",\"lastPrice\":-1,\"priceChangePercent\":\"2\",\"closeTime\":1700000000000}")]
    public void MalformedTickerFieldsProduceRecoverableJsonError(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Throws<System.Text.Json.JsonException>(() => TickerParser.ParseRest(doc.RootElement));
    }

    [Fact]
    public async Task MalformedRestSnapshotStillStartsStreaming()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var rest = new BinanceService(new HttpClient(new ResponseHandler(_ => new(HttpStatusCode.OK)
        { Content = new StringContent("null") })));
        var socket = new ScriptedSocket([("{\"s\":\"BTCUSDT\",\"c\":\"1\",\"P\":\"0\",\"E\":1700000000000}", true)]);
        var feed = new MarketFeed(rest, new(() => socket));
        TickerPrice? price = null;
        await feed.RunAsync(["BTCUSDT"], p => { price = p; cts.Cancel(); }, _ => { }, cts.Token);
        Assert.Equal(1, price!.Price);
    }

    [Fact]
    public async Task CancellationInterruptsPendingReceiveAndDisposesSocket()
    {
        using var cts = new CancellationTokenSource();
        var socket = new WaitingSocket();
        var task = new BinanceWebSocketService(() => socket).RunAsync(["BTCUSDT"], _ => { }, _ => { }, cts.Token);
        await socket.Receiving.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel();
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task CancellationInterruptsReconnectDelay()
    {
        using var cts = new CancellationTokenSource();
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = new BinanceWebSocketService(() => new ScriptedSocket([]), (delay, token) =>
        { waiting.SetResult(); return Task.Delay(delay, token); });
        var task = stream.RunAsync(["BTCUSDT"], _ => { }, _ => { }, cts.Token);
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(2));
        cts.Cancel(); await task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class WaitingSocket : IMarketSocket
    {
        public TaskCompletionSource Receiving { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => Task.CompletedTask;
        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        { Receiving.SetResult(); await Task.Delay(Timeout.Infinite, cancellationToken); return default; }
        public void Dispose() => Disposed = true;
    }

    private sealed class ScriptedSocket(IEnumerable<(string Text, bool End)> messages) : IMarketSocket
    {
        private readonly Queue<(string Text, bool End)> queue = new(messages);
        public Uri? Uri { get; private set; }
        public bool Disposed { get; private set; }
        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) { Uri = uri; return Task.CompletedTask; }
        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!queue.TryDequeue(out var frame)) throw new WebSocketException("Offline");
            var bytes = Encoding.UTF8.GetBytes(frame.Text);
            bytes.CopyTo(buffer);
            return ValueTask.FromResult(new ValueWebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, frame.End));
        }
        public void Dispose() => Disposed = true;
    }
}
