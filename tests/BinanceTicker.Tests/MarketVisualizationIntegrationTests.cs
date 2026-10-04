using System.Reflection;
using System.Windows;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Views;
using BinanceTicker.Services;
namespace BinanceTicker.Tests;
internal static class MarketVisualizationIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Verify(App app)
    {
        var initialize=typeof(App).GetMethod("InitializeMarketVisualization",Private); Assert.NotNull(initialize);
        var settings=new AppSettings(); settings.Ui.ShowSparkline=false;
        typeof(App).GetField("settings",Private)!.SetValue(app,settings);
        var ticker=(TickerViewModel)typeof(App).GetField("ticker",Private)!.GetValue(app)!; ticker.Configure(settings);
        var cache=(CandleCache)typeof(App).GetField("candleCache",Private)!.GetValue(app)!;
        var source=new ControlledHistory(); var coordinator=new HistoryCoordinator(source,cache);
        coordinator.ConfigureAsync(["BTCUSDT","ETHUSDT","ENAUSDT"]).GetAwaiter().GetResult();
        typeof(App).GetField("marketHistory",Private)!.SetValue(app,coordinator);
        var window=new TickerWindow { DataContext=ticker, ShowActivated=false };
        using var floatManager=new TickerWindowManager(window,settings,()=>{});
        floatManager.SetMode(DisplayMode.Float);
        initialize.Invoke(app,[window]);
        var demand=typeof(App).GetMethod("UpdateHistoryDemand",Private)!;
        demand.Invoke(app,null); Assert.Equal(0,source.Calls);
        window.Show(); demand.Invoke(app,null); Assert.Equal(0,source.Calls);
        var open=typeof(App).GetMethod("OpenMarketDetails",Private)!;
        open.Invoke(app,["BTCUSDT"]);
        var first=app.Windows.OfType<Window>().Single(w=>w.DataContext is MarketDetailsViewModel d && d.Row.Symbol=="BTCUSDT");
        source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        open.Invoke(app,["BTCUSDT"]); Assert.Same(first,app.Windows.OfType<Window>().Single(w=>w.DataContext is MarketDetailsViewModel d && d.Row.Symbol=="BTCUSDT"));
        Assert.Equal(1,source.Calls);
        typeof(Window).GetMethod("OnDeactivated",Private)!.Invoke(window,[EventArgs.Empty]); demand.Invoke(app,null);
        Assert.False(window.IsVisible); Assert.True(first.IsVisible);
        open.Invoke(app,["ETHUSDT"]); Assert.Equal(2,app.Windows.OfType<Window>().Count(w=>w.DataContext is MarketDetailsViewModel));
        dynamic manager=typeof(App).GetField("marketDetails",Private)!.GetValue(app)!;
        manager.CloseUnavailable(new[]{"ETHUSDT"});
        Assert.False(first.IsVisible); Assert.Equal(1,app.Windows.OfType<Window>().Count(w=>w.DataContext is MarketDetailsViewModel));
        manager.CloseAll();
        coordinator.StopAsync().GetAwaiter().GetResult();
        floatManager.CloseForExit(); typeof(App).GetField("marketHistory",Private)!.SetValue(app,null);
    }
    public static Action AttachPendingHistory(App app)
    {
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<IReadOnlyList<CandlePrice>>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token=default;
        var source=new ControlledHistory(async ct=> {token=ct; entered.SetResult(); return await release.Task;});
        var cache=new CandleCache(); var coordinator=new HistoryCoordinator(source,cache);
        coordinator.ConfigureAsync(["BTCUSDT"]).GetAwaiter().GetResult(); coordinator.SetDemand(["BTCUSDT"]);
        entered.Task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
        typeof(App).GetField("marketHistory",Private)!.SetValue(app,coordinator);
        return ()=>{ Assert.True(token.IsCancellationRequested); release.SetResult([]); };
    }
    private sealed class ControlledHistory(Func<CancellationToken,Task<IReadOnlyList<CandlePrice>>>? load=null):IBinanceHistoryService
    {
        public int Calls;
        public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<CandlePrice>> GetCandlesAsync(string symbol,DateTimeOffset start,DateTimeOffset end,CancellationToken token)
        {Interlocked.Increment(ref Calls); Entered.TrySetResult(); return load?.Invoke(token) ?? Task.FromResult<IReadOnlyList<CandlePrice>>([]);}
    }
}
