using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class MarketDemandTests
{
    [Fact] public void DemandUnionsGroupsHoldingsAndAlerts()
    {
        var s=new AppSettings { Symbols=[new() { Symbol="ENAUSDT",Alert=new() { UpperPrice=1 } },new() { Symbol="BNBUSDT",Alert=new() { Rise=new() { ThresholdPercent=5 } } }],
            Watchlists=[new() { Name="A",Members=[new() { Symbol="BTCUSDT" },new() { Symbol="ETHUSDT",Enabled=false }] },new() { Name="B",Members=[new() { Symbol="BTCUSDT" }] }],
            Holdings=[new() { Symbol="SOLUSDT",Quantity=1 },new() { Symbol="ETHUSDT",Quantity=0 }] };
        WatchlistSettings.Normalize(s);
        Assert.Equal(new[] { "BNBUSDT","BTCUSDT","ENAUSDT","SOLUSDT" },MarketDemand.Quotes(s));
    }
    [Fact] public void OrphanHoldingStillSubscribes()
    {
        var s=new AppSettings { Symbols=[],Holdings=[new() { Symbol="BTCUSDT",Quantity=2 }] };WatchlistSettings.Normalize(s);Assert.Equal(new[] { "BTCUSDT" },MarketDemand.Quotes(s));
    }
    [Fact] public void LegacyDisabledAlertDoesNotSubscribe()
    {
        var s=new AppSettings { Symbols=[new() { Symbol="BTCUSDT",Enabled=false,Alert=new() { UpperPrice=10 } }] };WatchlistSettings.Normalize(s);Assert.Empty(MarketDemand.Quotes(s));
    }
    [Fact] public void EmptyDemandIsEmpty() { var s=new AppSettings { Symbols=[] };WatchlistSettings.Normalize(s);Assert.Empty(MarketDemand.Quotes(s)); }
}
