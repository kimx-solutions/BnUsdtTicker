using System.Net;

namespace BinanceTicker.Core.Services;

public sealed class HistoryRequestScheduler
{
    private readonly HttpClient client;
    private readonly TimeProvider clock;
    private readonly SemaphoreSlim slots = new(2, 2);
    private readonly SemaphoreSlim starts = new(1, 1);
    private readonly object gate = new();
    private DateTimeOffset nextStart = DateTimeOffset.MinValue;
    private DateTimeOffset pausedUntil = DateTimeOffset.MinValue;
    private static readonly int[] RetrySeconds = [1, 2, 5];

    public HistoryRequestScheduler(HttpClient client, TimeProvider? timeProvider = null)
    { this.client = client; clock = timeProvider ?? TimeProvider.System; }

    public async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await SendOnceAsync(uri, token).ConfigureAwait(false);
                var transient = (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 418;
                if (!transient || attempt == RetrySeconds.Length) return response;
                response.Dispose();
            }
            catch (Exception ex) when ((ex is HttpRequestException or OperationCanceledException) &&
                !token.IsCancellationRequested && attempt < RetrySeconds.Length) { }
            await Task.Delay(TimeSpan.FromSeconds(RetrySeconds[attempt]), clock, token).ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Uri uri, CancellationToken token)
    {
        await slots.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await starts.WaitAsync(token).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    TimeSpan delay;
                    lock (gate)
                    {
                        var now = clock.GetUtcNow();
                        var due = nextStart > pausedUntil ? nextStart : pausedUntil;
                        delay = due - now;
                        if (delay <= TimeSpan.Zero) { nextStart = now.AddMilliseconds(250); break; }
                    }
                    await Task.Delay(delay > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : delay, clock, token).ConfigureAwait(false);
                }
            }
            finally { starts.Release(); }
            var response = await client.GetAsync(uri, token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode == 418)
                Pause(response);
            return response;
        }
        finally { slots.Release(); }
    }

    private void Pause(HttpResponseMessage response)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            var retry = response.Headers.RetryAfter;
            var until = retry?.Date ?? (retry?.Delta is { } delta && delta > TimeSpan.Zero && delta < DateTimeOffset.MaxValue - now
                ? now + delta : now.AddSeconds(60));
            if (until <= now) until = now.AddSeconds(60);
            if (until > pausedUntil) pausedUntil = until;
        }
    }
}
