using System.Net;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class HistoryRequestSchedulerTests
{
    private static readonly Uri Endpoint = new("https://example.invalid/history");
    [Fact]
    public async Task SchedulerLimitsConcurrencyAndStartSpacing()
    {
        var clock = new TestTimeProvider(CandleFixtures.Now); var starts = new List<DateTimeOffset>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0; var maximum = 0;
        using var http = new HttpClient(new Handler(async token =>
        {
            lock (starts) { starts.Add(clock.GetUtcNow()); active++; maximum = Math.Max(active, maximum); }
            await release.Task.WaitAsync(token);
            lock (starts) active--;
            return new(HttpStatusCode.OK);
        }));
        var scheduler = new HistoryRequestScheduler(http, clock);
        var tasks = Enumerable.Range(0, 5).Select(_ => scheduler.GetAsync(Endpoint, default)).ToArray();
        try { await clock.UntilAsync(() => { lock (starts) return starts.Count >= 2; }); Assert.Equal(2, maximum); }
        finally { release.TrySetResult(); }
        var responses = await clock.CompleteAsync(Task.WhenAll(tasks));
        foreach (var response in responses) response.Dispose();
        Assert.Equal(2, maximum);
        Assert.All(starts.Zip(starts.Skip(1)), pair => Assert.True(pair.Second - pair.First >= TimeSpan.FromMilliseconds(250)));
    }
    [Fact]
    public async Task RetriesOnlyTransientFailures()
    {
        var clock = new TestTimeProvider(CandleFixtures.Now); var starts = new List<DateTimeOffset>();
        using var http = new HttpClient(new Handler(_ => { starts.Add(clock.GetUtcNow()); return Task.FromResult(new HttpResponseMessage(starts.Count < 4 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)); }));
        var scheduler = new HistoryRequestScheduler(http, clock);
        using var result = await clock.CompleteAsync(scheduler.GetAsync(Endpoint, default), TimeSpan.FromMilliseconds(100));
        Assert.Equal(HttpStatusCode.OK, result.StatusCode); Assert.Equal(4, starts.Count);
        var gaps = starts.Zip(starts.Skip(1)).Select(p => (p.Second - p.First).TotalSeconds).ToArray();
        Assert.InRange(gaps[0], 1, 1.5); Assert.InRange(gaps[1], 2, 2.5); Assert.InRange(gaps[2], 5, 5.5);
        using var invalidHttp = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest))));
        using var invalid = await new HistoryRequestScheduler(invalidHttp, clock).GetAsync(Endpoint, default);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    [Fact]
    public async Task RateLimitPauseAlsoAppliesToNewAndManualRequests()
    {
        var clock = new TestTimeProvider(CandleFixtures.Now); var starts = new List<DateTimeOffset>();
        using var http = new HttpClient(new Handler(_ =>
        {
            starts.Add(clock.GetUtcNow()); var response = new HttpResponseMessage(starts.Count == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
            if (starts.Count == 1) response.Headers.RetryAfter = new(TimeSpan.FromSeconds(10));
            return Task.FromResult(response);
        }));
        var scheduler = new HistoryRequestScheduler(http, clock);
        var first = scheduler.GetAsync(Endpoint, default); var second = scheduler.GetAsync(Endpoint, default);
        clock.Advance(TimeSpan.FromSeconds(9)); await Task.Yield();
        Assert.Single(starts);
        var results = await clock.CompleteAsync(Task.WhenAll(first, second), TimeSpan.FromMilliseconds(100));
        foreach (var response in results) response.Dispose();
        Assert.All(starts.Skip(1), start => Assert.True(start - starts[0] >= TimeSpan.FromSeconds(10)));
    }
    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(token); }
}
