using System.Net;
using System.Text.Json;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class HistoryRestTests
{
    [Fact]
    public async Task Loads1442CandlesAcrossTwoFixedRangePages()
    {
        var clock = new TestTimeProvider(CandleFixtures.Now);
        var start = CandleFixtures.Now.AddMinutes(-1441); var end = CandleFixtures.Now;
        var requests = new List<Uri>();
        using var http = new HttpClient(new Handler(uri =>
        {
            requests.Add(uri); var query = CandleFixtures.Query(uri);
            Assert.Equal("1m", query["interval"]); Assert.Equal("1000", query["limit"]);
            Assert.Equal(end.ToUnixTimeMilliseconds().ToString(), query["endTime"]);
            var from = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(query["startTime"]));
            return new(HttpStatusCode.OK) { Content = new StringContent(CandleFixtures.Rows(from, Math.Min(1000, (int)(end - from).TotalMinutes + 1))) };
        }));
        var service = new BinanceHistoryService(new(http, clock));
        var result = await clock.CompleteAsync(service.GetCandlesAsync("BTCUSDT", start, end, default));
        Assert.Equal(1442, result.Count); Assert.Equal(2, requests.Count);
        Assert.Equal(start, result[0].OpenTime); Assert.Equal(end, result[^1].OpenTime);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task RejectsNonAdvancingOrWrongOrderPage(bool duplicate)
    {
        using var doc = JsonDocument.Parse(CandleFixtures.Rows(CandleFixtures.Now.AddMinutes(-2), 3));
        var bad = duplicate ? "[" + doc.RootElement[0] + "," + doc.RootElement[0] + "]" : "[" + doc.RootElement[1] + "," + doc.RootElement[0] + "]";
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(bad) }));
        var service = new BinanceHistoryService(new(http));
        await Assert.ThrowsAsync<JsonException>(() => service.GetCandlesAsync("BTCUSDT", CandleFixtures.Now.AddMinutes(-2), CandleFixtures.Now, default));
    }
    [Fact]
    public async Task CancelsBetweenPages()
    {
        using var cts = new CancellationTokenSource(); var requests = 0;
        using var http = new HttpClient(new Handler(_ => { requests++; cts.Cancel(); return new(HttpStatusCode.OK) { Content = new StringContent(CandleFixtures.Rows(CandleFixtures.Now.AddMinutes(-1441), 1000)) }; }));
        var service = new BinanceHistoryService(new(http));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetCandlesAsync("BTCUSDT", CandleFixtures.Now.AddMinutes(-1441), CandleFixtures.Now, cts.Token));
        Assert.Equal(1, requests);
    }
    [Fact]
    public async Task EmptyResponseDoesNotInventData()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("[]") }));
        Assert.Empty(await new BinanceHistoryService(new(http)).GetCandlesAsync("BTCUSDT", CandleFixtures.Now.AddHours(-24), CandleFixtures.Now, default));
    }
    private sealed class Handler(Func<Uri, HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request.RequestUri!)); }
}
