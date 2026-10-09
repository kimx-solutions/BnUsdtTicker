using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;

public sealed class SwapComparisonCalculatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    public static SwapComparisonSetting Example() => new()
    { Id = "swap", FromSymbol = "NEARUSDT", FromQuantity = 100, ToSymbol = "QNTUSDT", ToQuantity = 20, SwappedAt = Now.AddDays(-1) };
    private static TickerPrice Quote(string symbol, decimal price, int age = 0) => new(symbol, price, 0, Now.AddSeconds(-age).UtcDateTime);

    [Theory]
    [InlineData(30,120,20,100,20)]
    [InlineData(20,80,-20,-100,-20)]
    [InlineData(25,100,0,0,0)]
    public void ComparesAgainstHoldingTheOriginalCoin(decimal price, decimal returned, decimal units, decimal usdt, decimal percent)
    {
        var result = SwapComparisonCalculator.Value(Example(), Quote("NEARUSDT",5), Quote("QNTUSDT",price), ConnectionStatus.Connected, Now);
        Assert.Equal(returned,result.ReturnQuantity); Assert.Equal(units,result.QuantityDifference);
        Assert.Equal(usdt,result.UsdtDifference); Assert.Equal(percent,result.ReturnPercent);
        Assert.Equal(500,result.FromValue);
    }

    [Fact]
    public void MissingInvalidAndFutureQuotesNeverBecomeZeroValueResults()
    {
        foreach (var quote in new TickerPrice?[] { null, Quote("NEARUSDT",0), Quote("NEARUSDT",-1), Quote("NEARUSDT",5,-1), Quote("BTCUSDT",5) })
        {
            var result = SwapComparisonCalculator.Value(Example(),quote,Quote("QNTUSDT",30),ConnectionStatus.Connected,Now);
            Assert.Null(result.ReturnQuantity); Assert.Null(result.UsdtDifference);
            Assert.Contains("NEAR",result.StateText);
        }
    }

    [Theory]
    [InlineData(60,false)]
    [InlineData(61,true)]
    public void StalenessUsesBothQuotesAndTheSixtySecondBoundary(int age,bool stale)
    {
        var result = SwapComparisonCalculator.Value(Example(),Quote("NEARUSDT",5),Quote("QNTUSDT",30,age),ConnectionStatus.Connected,Now);
        Assert.Equal(stale,result.IsStale); Assert.Equal(120,result.ReturnQuantity);
    }

    [Fact]
    public void DisconnectionAndDisabledTrackingAreExplicit()
    {
        Assert.True(SwapComparisonCalculator.Value(Example(),Quote("NEARUSDT",5),Quote("QNTUSDT",30),ConnectionStatus.Disconnected,Now).IsStale);
        var setting=Example();setting.Enabled=false;
        var disabled=SwapComparisonCalculator.Value(setting,Quote("NEARUSDT",5),Quote("QNTUSDT",30),ConnectionStatus.Connected,Now);
        Assert.Equal("已停用",disabled.StateText);Assert.Null(disabled.ReturnQuantity);
    }

    [Fact]
    public void OverflowAndUnderflowDoNotProducePartialComparisonOrThrow()
    {
        var setting=Example();setting.ToQuantity=decimal.MaxValue;
        var overflow=SwapComparisonCalculator.Value(setting,Quote("NEARUSDT",5),Quote("QNTUSDT",30),ConnectionStatus.Connected,Now);
        Assert.Null(overflow.ReturnQuantity);Assert.Null(overflow.FromValue);Assert.Contains("範圍",overflow.StateText);
        setting=Example();setting.FromQuantity=0.0000000000000000000000000001m;
        var tiny=SwapComparisonCalculator.Value(setting,Quote("NEARUSDT",0.0000000000000000000000000001m),Quote("QNTUSDT",30),ConnectionStatus.Connected,Now);
        Assert.Null(tiny.ReturnQuantity);Assert.Contains("範圍",tiny.StateText);
    }
}
