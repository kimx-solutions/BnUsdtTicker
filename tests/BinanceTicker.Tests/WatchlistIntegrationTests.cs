using System.Reflection;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
namespace BinanceTicker.Tests;

internal static class WatchlistIntegrationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Verify(App app)
    {
        var settings=WatchlistEditorTests.Groups();settings.Watchlists![1].Members.Add(new() { Symbol="SOLUSDT" });
        settings.Symbols.Add(new() { Symbol="SOLUSDT",Alert=new() { UpperPrice=10 } });
        settings.Symbols.Single(s=>s.Symbol=="BTCUSDT").Alert.UpperTriggered=true;
        typeof(App).GetField("settings",Private)!.SetValue(app,settings);
        var ticker=(TickerViewModel)typeof(App).GetField("ticker",Private)!.GetValue(app)!;ticker.Configure(settings);
        var alerts=new RecordingAlerts();typeof(App).GetField("alerts",Private)!.SetValue(app,alerts);
        var update=typeof(App).GetMethod("UpdatePriceAsync",Private)!;
        ((Task)update.Invoke(app,[new TickerPrice("SOLUSDT",12,0,DateTime.UtcNow)])!).GetAwaiter().GetResult();
        Assert.Equal("SOLUSDT",Assert.Single(alerts.Checked));Assert.DoesNotContain(ticker.Prices,r=>r.Symbol=="SOLUSDT");
        var persist=typeof(App).GetMethod("PersistWatchlistPreferences",Private);Assert.NotNull(persist);
        var oldGeneration=(int)typeof(App).GetField("feedGeneration",Private)!.GetValue(app)!;
        // Inject reversible save behavior instead of touching the user's settings.
        typeof(App).GetField("watchlistPreferenceWriter",Private)!.SetValue(app,(Action<AppSettings>)(_=>{}));
        ticker.SelectWatchlist("b");persist.Invoke(app,null);Assert.Equal("b",settings.ActiveWatchlistId);
        Assert.Equal(oldGeneration,typeof(App).GetField("feedGeneration",Private)!.GetValue(app));
        Assert.True(settings.Symbols.Single(s=>s.Symbol=="BTCUSDT").Alert.UpperTriggered);
        typeof(App).GetField("subscribedSymbols",Private)!.SetValue(app,MarketDemand.Quotes(settings));
        var costEdit=settings.Copy();costEdit.Holdings[0].AverageCost=99;
        Assert.False((bool)typeof(App).GetMethod("MarketDemandChanged",Private)!.Invoke(app,[costEdit])!);
        costEdit.Holdings.Add(new() { Symbol="ORPHANUSDT",Quantity=2 });
        Assert.True((bool)typeof(App).GetMethod("MarketDemandChanged",Private)!.Invoke(app,[costEdit])!);
        typeof(App).GetField("watchlistPreferenceWriter",Private)!.SetValue(app,(Action<AppSettings>)(_=>throw new IOException("controlled")));
        ticker.SelectWatchlist("a");persist.Invoke(app,null);Assert.Equal("b",ticker.ActiveWatchlistId);Assert.Equal("b",settings.ActiveWatchlistId);Assert.NotEmpty(ticker.WatchlistError);
        typeof(App).GetField("watchlistPreferenceWriter",Private)!.SetValue(app,null);
    }
    private sealed class RecordingAlerts:IPriceAlertService
    {
        public List<string> Checked { get; }=[];
        public Task CheckQuoteAsync(TickerPrice quote) { Checked.Add(quote.Symbol);return Task.CompletedTask; }
        public Task CheckAsync(string symbol,decimal price)=>Task.CompletedTask;
        public Task ResetAsync(string symbol,AlertType? type=null)=>Task.CompletedTask;
        public Task OnConnectionStatusAsync(ConnectionStatus status)=>Task.CompletedTask;
        public Task ApplySettingsAsync(AppSettings settings,IReadOnlyList<AlertResetRequest> resets,bool preserveAlerts=false)=>Task.CompletedTask;
        public Task SaveAlertAsync(string symbol,PriceAlertSettings updated,IReadOnlyList<AlertResetRequest> resets)=>Task.CompletedTask;
    }
}
