using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
namespace BinanceTicker.Tests;
public sealed class WatchlistTickerTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-04T12:00:00Z");
    [Fact] public void HiddenGroupQuotesUpdatePortfolioAndAlerts()
    {
        var s=WatchlistEditorTests.Groups();s.Watchlists![1].Members.Add(new() { Symbol="SOLUSDT" });s.Holdings.Add(new() { Symbol="SOLUSDT",Quantity=2,AverageCost=10 });
        var vm=new TickerViewModel();vm.Configure(s);Assert.True(vm.Update(new("SOLUSDT",12,0,Now.UtcDateTime)));vm.SetStatus(ConnectionStatus.Connected);vm.RefreshPortfolio(Now);
        Assert.DoesNotContain(vm.Prices,r=>r.Symbol=="SOLUSDT");Assert.Equal(24,vm.TotalPortfolio.PartialMarketValue);
        vm.SelectWatchlist("b");Assert.Equal(12,vm.Prices.Single(r=>r.Symbol=="SOLUSDT").Price);
    }
    [Fact] public void SwitchKeepsQuotesAndIndependentSort()
    {
        var s=WatchlistEditorTests.Groups();var vm=new TickerViewModel();vm.Configure(s);vm.Update(new("BTCUSDT",20,0,Now.UtcDateTime));vm.Update(new("ETHUSDT",10,0,Now.UtcDateTime));
        var row=vm.Prices[0];vm.SortBy(TickerSortColumn.Price);vm.ToggleSparklineCommand.Execute(null);vm.SelectWatchlist("b");
        Assert.Null(vm.SortColumn);Assert.True(vm.ShowSparkline);vm.SelectWatchlist("a");Assert.Equal(TickerSortColumn.Price,vm.SortColumn);Assert.False(vm.ShowSparkline);Assert.Same(row,vm.Prices[1]);
    }
    [Fact] public void SharedHoldingTotalsAreNotDoubled()
    {
        var vm=new TickerViewModel();vm.Configure(WatchlistEditorTests.Groups());vm.Update(new("BTCUSDT",12,0,Now.UtcDateTime));vm.SetStatus(ConnectionStatus.Connected);vm.RefreshPortfolio(Now);
        Assert.Equal(24,vm.CurrentPortfolio.MarketValue);Assert.Equal(24,vm.TotalPortfolio.MarketValue);Assert.Equal(1,vm.TotalPortfolio.HoldingCount);
    }
    [Fact] public void HiddenMembersBelongToCurrentSummary()
    {
        var s=WatchlistEditorTests.Groups();s.Watchlists![0].Members[0].Enabled=false;var vm=new TickerViewModel();vm.Configure(s);Assert.True(vm.Update(new("BTCUSDT",12,0,Now.UtcDateTime)));vm.RefreshPortfolio(Now);
        Assert.DoesNotContain(vm.Prices,r=>r.Symbol=="BTCUSDT");Assert.Equal(24,vm.CurrentPortfolio.MarketValue);
    }
    [Fact] public void OlderQuotesAreRejectedGlobally()
    {
        var vm=new TickerViewModel();vm.Configure(WatchlistEditorTests.Groups());vm.SelectWatchlist("b");Assert.True(vm.Update(new("ETHUSDT",20,0,Now.UtcDateTime)));
        Assert.False(vm.Update(new("ETHUSDT",1,0,Now.AddSeconds(-1).UtcDateTime)));vm.SelectWatchlist("a");Assert.Equal(20,vm.Prices.Single(r=>r.Symbol=="ETHUSDT").Price);
    }
    [Fact] public void StaleStatusRefreshesWithoutVisibleSparkline()
    {
        var vm=new TickerViewModel();vm.Configure(WatchlistEditorTests.Groups());vm.Update(new("BTCUSDT",12,0,Now.UtcDateTime));vm.SetStatus(ConnectionStatus.Connected);vm.RefreshPortfolio(Now);
        Assert.False(vm.TotalPortfolio.IsStale);vm.ToggleSparklineCommand.Execute(null);vm.RefreshPortfolio(Now.AddSeconds(61));Assert.True(vm.TotalPortfolio.IsStale);
    }
}
