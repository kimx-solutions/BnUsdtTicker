using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;

public sealed class PortfolioCalculatorTests
{
    private static readonly DateTimeOffset Now=DateTimeOffset.Parse("2026-10-04T12:00:00Z");
    private static HoldingSetting Holding(decimal quantity=2,decimal cost=10,string symbol="BTCUSDT")=>new() { Symbol=symbol,Quantity=quantity,AverageCost=cost };
    private static TickerPrice Quote(decimal price=12,string symbol="BTCUSDT")=>new(symbol,price,0,Now.UtcDateTime);
    [Theory]
    [InlineData(12,24,4,20)]
    [InlineData(8,16,-4,-20)]
    public void ValuesDecimalProfitWithoutPrematureRounding(decimal price,decimal value,decimal profit,decimal percent)
    {
        var result=PortfolioCalculator.Value(Holding(),Quote(price),ConnectionStatus.Connected,Now);
        Assert.Equal(value,result.MarketValue);Assert.Equal(20m,result.CostBasis);Assert.Equal(profit,result.Profit);Assert.Equal(percent,result.ProfitPercent);
        var precise=PortfolioCalculator.Value(Holding(0.123456789m,0.987654321m),Quote(1.123456789m),ConnectionStatus.Connected,Now);
        Assert.Equal(0.123456789m*1.123456789m,precise.MarketValue);
    }
    [Fact] public void ZeroCostDoesNotDivide() { var r=PortfolioCalculator.Value(Holding(cost:0),Quote(),ConnectionStatus.Connected,Now);Assert.Null(r.ProfitPercent);Assert.Equal(24,r.Profit); }
    [Fact] public void ZeroQuantityNeedsNoQuote() { var r=PortfolioCalculator.Value(Holding(0),null,ConnectionStatus.Disconnected,Now);Assert.Equal(0,r.MarketValue);Assert.Equal(0,r.Profit);Assert.Null(r.ProfitPercent);Assert.False(r.MissingQuote); }
    [Fact] public void MissingQuoteIsNotZero() { var r=PortfolioCalculator.Value(Holding(),null,ConnectionStatus.Connected,Now);Assert.Null(r.MarketValue);Assert.Null(r.Profit);Assert.True(r.MissingQuote); }
    [Fact] public void DisconnectedAndExpiredQuotesAreMarked()
    {
        Assert.True(PortfolioCalculator.Value(Holding(),Quote(),ConnectionStatus.Disconnected,Now).IsStale);
        Assert.True(PortfolioCalculator.Value(Holding(),Quote(),ConnectionStatus.Connected,Now.AddSeconds(61)).IsStale);
        Assert.False(PortfolioCalculator.Value(Holding(),Quote(),ConnectionStatus.Connected,Now.AddSeconds(60)).IsStale);
    }
    [Fact] public void OverflowDoesNotThrow() { var r=PortfolioCalculator.Value(Holding(decimal.MaxValue,1),Quote(2),ConnectionStatus.Connected,Now);Assert.True(r.Overflowed);Assert.Null(r.MarketValue); }
    [Fact] public void SummaryUsesWeightedCostAndDeduplicates()
    {
        var r=PortfolioCalculator.Summarize([Holding(),Holding(),Holding(1,20,"ETHUSDT")],new Dictionary<string,TickerPrice> { ["BTCUSDT"]=Quote(),["ETHUSDT"]=Quote(24,"ETHUSDT") },ConnectionStatus.Connected,Now);
        Assert.Equal(2,r.HoldingCount);Assert.Equal(48,r.MarketValue);Assert.Equal(40,r.CostBasis);Assert.Equal(8,r.Profit);Assert.Equal(20,r.ProfitPercent);
    }
    [Fact] public void IncompleteSummaryReportsOnlySubtotal()
    {
        var r=PortfolioCalculator.Summarize([Holding(),Holding(symbol:"ETHUSDT")],new Dictionary<string,TickerPrice> { ["BTCUSDT"]=Quote() },ConnectionStatus.Connected,Now);
        Assert.Null(r.MarketValue);Assert.Null(r.Profit);Assert.Null(r.ProfitPercent);Assert.Equal(24,r.PartialMarketValue);Assert.Equal(4,r.PartialProfit);Assert.Equal(1,r.MissingCount);
    }
    [Fact] public void ZeroAndUnsetHoldingsAreDistinct()
    {
        Assert.Equal(0,PortfolioCalculator.Summarize([],new Dictionary<string,TickerPrice>(),ConnectionStatus.Connected,Now).HoldingCount);
        var r=PortfolioCalculator.Summarize([Holding(0)],new Dictionary<string,TickerPrice>(),ConnectionStatus.Connected,Now);
        Assert.Equal(1,r.HoldingCount);Assert.Equal(0,r.MarketValue);Assert.Null(r.ProfitPercent);
    }
    [Fact] public void SumOverflowIsIncomplete()
    {
        var r=PortfolioCalculator.Summarize([Holding(1,0),Holding(1,0,"ETHUSDT")],new Dictionary<string,TickerPrice> { ["BTCUSDT"]=Quote(decimal.MaxValue),["ETHUSDT"]=Quote(decimal.MaxValue,"ETHUSDT") },ConnectionStatus.Connected,Now);
        Assert.Null(r.MarketValue);Assert.True(r.MissingCount>0);
    }
}
