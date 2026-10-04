using System.Diagnostics;
using System.Net;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class MarketRecoveryRegressionTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(1500)]
    public async Task HiddenReconnectRemembersGapBeforeNewStreamBars(int outageMinutes)
    {
        var initial=CandleFixtures.Now;
        var clock=new TestTimeProvider(initial); var cache=new CandleCache(); var starts=new List<DateTimeOffset>();
        var history=new History((start,end)=>
        {
            starts.Add(start);
            if(starts.Count==1) return [CandleFixtures.Minute(initial.AddMinutes(-1))];
            var count=(int)((end-start).TotalMinutes);
            return Enumerable.Range(0,count).Select(i=>CandleFixtures.Minute(start.AddMinutes(i))).ToArray();
        });
        var coordinator=new HistoryCoordinator(history,cache,clock);
        await coordinator.ConfigureAsync(["BTCUSDT"]); coordinator.SetDemand(["BTCUSDT"]);
        await coordinator.EnsureLoadedAsync("BTCUSDT");
        coordinator.SetDemand([]); coordinator.OnConnectionStatus(ConnectionStatus.Disconnected);
        clock.Advance(TimeSpan.FromMinutes(outageMinutes));
        var now=clock.GetUtcNow();
        cache.Merge(CandleFixtures.Minute(now.AddMinutes(-1)),now);
        coordinator.OnConnectionStatus(ConnectionStatus.Connected);
        coordinator.SetDemand(["BTCUSDT"]); await coordinator.EnsureLoadedAsync("BTCUSDT");
        var expected=outageMinutes>1440 ? now.AddHours(-24).AddMinutes(-1) : initial.AddMinutes(-1);
        Assert.Equal(expected,starts[1]);
        Assert.Contains(cache.GetSnapshot("BTCUSDT",now),c=>c.OpenTime==(outageMinutes>1440 ? now.AddHours(-24) : initial));
        await coordinator.StopAsync();
    }
    [Fact]
    public async Task ManualRetryRebuildsPartialHistoryInsteadOfOnlyTail()
    {
        var starts=new List<DateTimeOffset>(); var history=new History((start,end)=> {starts.Add(start); return [CandleFixtures.Minute(end.AddMinutes(-1))];});
        var coordinator=new HistoryCoordinator(history,new(),new TestTimeProvider(CandleFixtures.Now));
        await coordinator.ConfigureAsync(["BTCUSDT"]); coordinator.SetDemand(["BTCUSDT"]); await coordinator.EnsureLoadedAsync("BTCUSDT");
        await coordinator.EnsureLoadedAsync("BTCUSDT",true);
        Assert.Equal(starts[0],starts[1]); await coordinator.StopAsync();
    }
    [Fact]
    public async Task RateLimitIsPublishedBeforeAQueuedRequestCanClaimReleasedSlot()
    {
        var clock=new PauseBarrierClock(); var firstResponse=new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thirdEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count=0;
        using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var http=new HttpClient(new Handler(async token=>
        {
            var call=Interlocked.Increment(ref count);
            if(call==1) return await firstResponse.Task.WaitAsync(token);
            if(call==2) secondEntered.SetResult(); else thirdEntered.TrySetResult();
            await Task.Delay(Timeout.Infinite,token); return new(HttpStatusCode.OK);
        }));
        var scheduler=new HistoryRequestScheduler(http,clock); var uri=new Uri("https://example.invalid/history");
        var tasks=Enumerable.Range(0,3).Select(_=>scheduler.GetAsync(uri,cts.Token)).ToArray();
        try
        {
            await clock.Inner.UntilAsync(()=>secondEntered.Task.IsCompleted); clock.Inner.Advance(TimeSpan.FromSeconds(1));
            var limited=new HttpResponseMessage(HttpStatusCode.TooManyRequests); limited.Headers.RetryAfter=new(TimeSpan.FromSeconds(60));
            firstResponse.SetResult(limited);
            await clock.PauseEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.WhenAny(thirdEntered.Task,Task.Delay(500));
            Assert.False(thirdEntered.Task.IsCompleted,"A released HTTP slot must not let queued work bypass an unpublished global pause.");
        }
        finally
        {
            clock.ReleasePause.Set(); cts.Cancel();
            try { foreach(var response in await Task.WhenAll(tasks)) response.Dispose(); } catch(OperationCanceledException) {}
        }
    }
    private sealed class History(Func<DateTimeOffset,DateTimeOffset,IReadOnlyList<CandlePrice>> load):IBinanceHistoryService
    {
        public Task<IReadOnlyList<CandlePrice>> GetCandlesAsync(string symbol,DateTimeOffset start,DateTimeOffset end,CancellationToken token)=>Task.FromResult(load(start,end));
    }
    private sealed class Handler(Func<CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(token);
    }
    private sealed class PauseBarrierClock:TimeProvider
    {
        public TestTimeProvider Inner {get;}=new(CandleFixtures.Now);
        public TaskCompletionSource PauseEntered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleasePause {get;}=new();
        public override DateTimeOffset GetUtcNow()
        {
            // A legal preemption immediately before pause publication, without altering scheduler state.
            if(new StackTrace().GetFrames().Any(f=>f.GetMethod() is {Name:"Pause",DeclaringType:var type} && type==typeof(HistoryRequestScheduler)))
            {PauseEntered.TrySetResult(); if(!ReleasePause.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Pause barrier timed out");}
            return Inner.GetUtcNow();
        }
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan due,TimeSpan period)=>Inner.CreateTimer(callback,state,due,period);
    }
}
