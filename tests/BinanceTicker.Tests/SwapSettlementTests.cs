using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class SwapSettlementTests
{
    private static readonly DateTimeOffset Now=new(2026,10,9,10,0,0,TimeSpan.Zero);

    private static SwapComparisonSetting Read(bool closed=false) => JsonSerializer.Deserialize<SwapComparisonSetting>(
        """
        {"Id":"settled","FromSymbol":"NEARUSDT","FromQuantity":100,"ToSymbol":"QNTUSDT","ToQuantity":20,
         "SwappedAt":"2026-10-08T10:00:00Z","Enabled":true,"Settlements":[
          {"Id":"first","Mode":0,"ToQuantity":5,"FromQuantity":25,"ReturnedQuantity":30,
           "SettledAt":"2026-10-09T09:00:00Z","FromPrice":5}
        """+(closed ? """
          ,{"Id":"second","Mode":0,"ToQuantity":15,"FromQuantity":75,"ReturnedQuantity":80,
           "SettledAt":"2026-10-09T09:30:00Z","FromPrice":6}
        """ : "")+"]}")!;

    [Fact]
    public void PartialSettlementOnlyValuesTheRemainingPosition()
    {
        var result=SwapComparisonCalculator.Value(Read(),new("NEARUSDT",5,0,Now.UtcDateTime),
            new("QNTUSDT",30,0,Now.UtcDateTime),ConnectionStatus.Connected,Now);
        Assert.Equal(375,result.FromValue);Assert.Equal(450,result.ToValue);
        Assert.Equal(90,result.ReturnQuantity);Assert.Equal(15,result.QuantityDifference);
        Assert.Equal(20,result.ReturnPercent);
    }

    [Fact]
    public void ClosedResultKeepsEachSettlementPriceAndNeedsNoLiveQuotes()
    {
        var result=SwapComparisonCalculator.Value(Read(true),null,null,ConnectionStatus.Disconnected,Now);
        Assert.Equal(110,result.ReturnQuantity);Assert.Equal(10,result.QuantityDifference);
        Assert.Equal(55,result.UsdtDifference);Assert.Equal(10,result.ReturnPercent);
        Assert.False(result.IsStale);
        var later=SwapComparisonCalculator.Value(Read(true),new("NEARUSDT",50,0,Now.UtcDateTime),
            new("QNTUSDT",1,0,Now.UtcDateTime),ConnectionStatus.Connected,Now.AddDays(1));
        Assert.Equal(result,later);
    }

    [Fact]
    public void SettlementDataSurvivesSerializationAndCopy()
    {
        var setting=Read(true);
        var json=JsonSerializer.Serialize(setting.Copy());
        Assert.Contains("Settlements",json);
        Assert.Contains("ReturnedQuantity",json);
    }

    [Fact]
    public void FullySettledRecordsDoNotDemandQuoteSubscriptions()
    {
        var demand=MarketDemand.Quotes(new(){SwapComparisons=[Read(true)]});
        Assert.DoesNotContain("QNTUSDT",demand);Assert.DoesNotContain("NEARUSDT",demand);
    }

    [Fact]
    public void SettingsFileRetainsSettlementsAndTheirFixedResultsAcrossReload()
    {
        var directory=Path.Combine(Path.GetTempPath(),"swap-settlement-"+Guid.NewGuid());Directory.CreateDirectory(directory);
        try
        {
            var service=new SettingsService(Path.Combine(directory,"settings.json"));
            service.Save(new(){SwapComparisons=[Read(true)]});
            var loaded=service.Load();Assert.Null(loaded.SwapComparisons[0].InvalidReason);
            Assert.Equal(2,loaded.SwapComparisons[0].Settlements.Count);
            var result=SwapComparisonCalculator.Value(loaded.SwapComparisons[0],null,null,ConnectionStatus.Disconnected,Now);
            Assert.Equal(55,result.UsdtDifference);Assert.Equal(110,result.ReturnQuantity);
            service.Save(loaded.Copy());Assert.Equal(2,service.Load().SwapComparisons[0].Settlements.Count);
        }
        finally { Directory.Delete(directory,true); }
    }

    [Theory]
    [InlineData("\"ToQuantity\":5","\"ToQuantity\":21")]
    [InlineData("\"FromQuantity\":25","\"FromQuantity\":30")]
    [InlineData("\"Mode\":0","\"Mode\":999")]
    [InlineData("\"ReturnedQuantity\":30","\"ReturnedQuantity\":-1")]
    [InlineData("\"FromPrice\":5","\"FromPrice\":79228162514264337593543950335")]
    [InlineData("2026-10-09T09:00:00+00:00","2026-10-07T09:00:00+00:00")]
    public void InvalidSettlementIsQuarantinedInsteadOfBeingDropped(string before,string after)
    {
        var original=JsonSerializer.Serialize(Read());var json=original.Replace(before,after);
        Assert.NotEqual(original,json);
        var setting=JsonSerializer.Deserialize<SwapComparisonSetting>(json)!;
        Assert.NotNull(setting.InvalidReason);Assert.Equal(json,setting.InvalidJson);
    }
}
