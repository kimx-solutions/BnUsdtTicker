using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class WatchlistAlertTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"WatchlistAlerts-"+Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ConcurrentAlertTriggerSurvivesGroupedSettingsSave()
    {
        var settings=WatchlistEditorTests.Groups();settings.Symbols.Single(s=>s.Symbol=="BTCUSDT").Alert.UpperPrice=12;
        var store=new SettingsService(Path.Combine(directory,"settings.json"));var history=new AlertHistoryService(Path.Combine(directory,"history.json"));
        var notices=new Notices();var alerts=new PriceAlertService(settings,store,history,notices);
        using var editor=new SettingsViewModel(settings,new WatchlistEditorTests.Validation());
        editor.Holdings.Single(h=>h.Symbol=="BTCUSDT").AverageCostText="11";
        await alerts.CheckQuoteAsync(new("BTCUSDT",12,0,DateTime.UtcNow));Assert.Single(notices.Entries);
        var edited=editor.CreateSettings();await alerts.ApplySettingsAsync(edited,[],preserveAlerts:true);
        await alerts.CheckQuoteAsync(new("BTCUSDT",13,0,DateTime.UtcNow.AddSeconds(1)));
        Assert.Single(notices.Entries);Assert.Single(history.Load());
        var restarted=store.Load();Assert.True(restarted.Symbols.Single(s=>s.Symbol=="BTCUSDT").Alert.UpperTriggered);
        Assert.Equal(11,Assert.Single(restarted.Holdings).AverageCost);Assert.Equal(2,restarted.Watchlists!.Count);
    }
    [Fact]
    public async Task DeletedGroupAlertStillReceivesQuotesAndPersistsState()
    {
        var settings=WatchlistEditorTests.Groups();settings.Watchlists![0].Members.Add(new() { Symbol="SOLUSDT" });
        settings.Symbols.Add(new() { Symbol="SOLUSDT",Alert=new() { UpperPrice=12 } });
        var store=new SettingsService(Path.Combine(directory,"settings.json"));var history=new AlertHistoryService(Path.Combine(directory,"history.json"));
        var notices=new Notices();var alerts=new PriceAlertService(settings,store,history,notices);
        using var editor=new SettingsViewModel(settings,new WatchlistEditorTests.Validation());editor.RemoveWatchlistCommand.Execute(null);
        var edited=editor.CreateSettings();await alerts.ApplySettingsAsync(edited,[],preserveAlerts:true);
        Assert.Contains("SOLUSDT",MarketDemand.Quotes(edited));
        var ticker=new TickerViewModel();ticker.Configure(edited);
        var quote=new TickerPrice("SOLUSDT",12,0,DateTime.UtcNow);
        Assert.DoesNotContain(ticker.Prices,r=>r.Symbol==quote.Symbol);Assert.True(ticker.Update(quote));
        await alerts.CheckQuoteAsync(quote);Assert.Equal("SOLUSDT",Assert.Single(notices.Entries).Symbol);
        Assert.True(store.Load().Symbols.Single(s=>s.Symbol==quote.Symbol).Alert.UpperTriggered);
    }
    private sealed class Notices : INotificationService
    {
        public List<AlertHistoryEntry> Entries { get; }=[];
        public void Show(AlertHistoryEntry entry)=>Entries.Add(entry);
    }
    public void Dispose() { if(Directory.Exists(directory))Directory.Delete(directory,true); }
}
