using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;

public sealed class SwapComparisonSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(),"swap-tests-"+Guid.NewGuid());
    private SettingsService Service => new(Path.Combine(directory,"settings.json"));
    public SwapComparisonSettingsTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory,true);

    [Fact]
    public void SettingsRoundTripDeepCopyAndLegacyLoadKeepExistingData()
    {
        var settings=new AppSettings { SwapComparisons=[SwapComparisonCalculatorTests.Example()] };
        settings.SwapComparisons[0].SwappedAt=DateTimeOffset.UtcNow.AddDays(-1);
        Service.Save(settings);
        var copy=Service.Load().Copy();copy.SwapComparisons[0].ToQuantity=10;
        Assert.Equal(20,Service.Load().SwapComparisons[0].ToQuantity);
        Assert.Equal("BTCUSDT",Service.Load().Symbols[0].Symbol);
        File.WriteAllText(Service.FilePath,"{}");
        Assert.Empty(Service.Load().SwapComparisons);
    }

    [Fact]
    public void InvalidTypedRecordIsIsolatedBackedUpAndCannotBeSilentlyOverwritten()
    {
        var settings=new AppSettings { SwapComparisons=[SwapComparisonCalculatorTests.Example()] };
        settings.SwapComparisons[0].SwappedAt=DateTimeOffset.UtcNow.AddDays(-1);
        Service.Save(settings);
        var json=File.ReadAllText(Service.FilePath);
        using var doc=JsonDocument.Parse(json);
        var good=doc.RootElement.GetProperty("swapComparisons")[0].GetRawText();
        var original="{\"symbols\":[{\"symbol\":\"ETHUSDT\",\"enabled\":true,\"order\":1}],\"swapComparisons\":["+good+",{\"id\":\"bad\",\"fromQuantity\":\"oops\"}]}";
        File.WriteAllText(Service.FilePath,original);
        var service=Service;var loaded=service.Load();
        Assert.Equal("ETHUSDT",loaded.Symbols[0].Symbol);Assert.Equal(2,loaded.SwapComparisons.Count);
        Assert.NotEmpty(loaded.SwapComparisons[1].InvalidReason!);Assert.NotNull(service.LoadWarning);
        Assert.Equal(original,File.ReadAllText(Service.FilePath+".swap-invalid.bak"));
        Assert.Throws<ArgumentException>(()=>service.Save(loaded));Assert.Equal(original,File.ReadAllText(Service.FilePath));
        loaded.SwapComparisons.RemoveAt(1);service.Save(loaded);
        Assert.Single(service.Load().SwapComparisons);
    }

    [Fact]
    public void SameCoinAndNonPositiveQuantitiesAreRejected()
    {
        var setting=SwapComparisonCalculatorTests.Example();
        setting.FromSymbol="QNTUSDT";Assert.Throws<ArgumentException>(()=>setting.Validate(DateTimeOffset.UtcNow));
        setting.FromSymbol="NEARUSDT";setting.ToQuantity=0;
        Assert.Throws<ArgumentException>(()=>setting.Validate(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void DuplicateQuarantineGetsItsOwnIdentitySoDeletingItKeepsTheValidSibling()
    {
        var record=SwapComparisonCalculatorTests.Example();record.SwappedAt=DateTimeOffset.UtcNow.AddDays(-1);
        Service.Save(new(){SwapComparisons=[record]});
        using var doc=JsonDocument.Parse(File.ReadAllText(Service.FilePath));
        var raw=doc.RootElement.GetProperty("swapComparisons")[0].GetRawText();
        File.WriteAllText(Service.FilePath,"{\"swapComparisons\":["+raw+","+raw+"]}");
        var loaded=Service.Load();
        Assert.Equal(2,loaded.SwapComparisons.Select(s=>s.Id).Distinct().Count());
        Assert.NotNull(loaded.SwapComparisons[1].InvalidReason);
        var invalidId=loaded.SwapComparisons[1].Id;
        loaded.SwapComparisons.RemoveAll(s=>s.Id==invalidId);Service.Save(loaded);
        Assert.Single(Service.Load().SwapComparisons);Assert.Equal(20,Service.Load().SwapComparisons[0].ToQuantity);
    }

    [Fact]
    public async Task ExplicitRepairCanRemoveOneBadRecordWhileKeepingAnotherOriginalRawRecord()
    {
        File.WriteAllText(Service.FilePath,"{\"swapComparisons\":[{\"id\":\"broken-one\",\"fromQuantity\":\"bad-one\"},{\"id\":\"broken-two\",\"swappedAt\":\"bad-two\"}]}");
        var service=Service;var current=service.Load();
        var remaining=current.SwapComparisons[1].Copy();var originalRaw=remaining.InvalidJson;
        var alerts=new PriceAlertService(current,service,new AlertHistoryService(Path.Combine(directory,"history.json")),new SilentNotification());
        var updated=current.Copy();updated.SwapComparisons.RemoveAt(0);
        await alerts.ApplySettingsAsync(updated,[],preserveAlerts:true);
        var loaded=service.Load();Assert.Single(loaded.SwapComparisons);
        Assert.Equal(originalRaw,loaded.SwapComparisons[0].InvalidJson);
        Assert.Throws<ArgumentException>(()=>service.Save(loaded));
        var completed=updated.Copy();completed.SwapComparisons.Clear();
        await alerts.ApplySettingsAsync(completed,[],preserveAlerts:true);
        Assert.Empty(service.Load().SwapComparisons);
    }

    [Fact]
    public void EachCorruptionIncidentKeepsItsOwnOriginalBackup()
    {
        var first="{\"swapComparisons\":[{\"fromQuantity\":\"first\"}]}";
        var second="{\"swapComparisons\":[{\"fromQuantity\":\"second\"}]}";
        File.WriteAllText(Service.FilePath,first);var loaded=Service.Load();loaded.SwapComparisons.Clear();Service.Save(loaded);
        File.WriteAllText(Service.FilePath,second);Service.Load();
        Assert.Equal(second,File.ReadAllText(Service.FilePath+".swap-invalid.bak"));
        var originals=Directory.GetFiles(directory,"*.swap-invalid.*.bak").Select(File.ReadAllText).ToList();
        Assert.Contains(first,originals);Assert.Contains(second,originals);
    }

    private sealed class SilentNotification:INotificationService { public void Show(AlertHistoryEntry entry){} }

    [Fact]
    public void ComparisonRecordsHonorCaseInsensitiveSettingsAndStandardSerializerRoundTrip()
    {
        var setting=SwapComparisonCalculatorTests.Example();setting.SwappedAt=DateTimeOffset.UtcNow.AddDays(-1);
        var json=JsonSerializer.Serialize(setting);
        var roundTrip=JsonSerializer.Deserialize<SwapComparisonSetting>(json)!;
        Assert.Null(roundTrip.InvalidReason);Assert.Equal(100,roundTrip.FromQuantity);
        File.WriteAllText(Service.FilePath,"{\"SwapComparisons\":["+json+"]}");
        var loaded=Service.Load();Assert.Null(loaded.SwapComparisons[0].InvalidReason);
    }

    [Fact]
    public void DemandIncludesBothSidesOutsideWatchlistsAndRetainsSharedDemand()
    {
        var swap=SwapComparisonCalculatorTests.Example();
        var settings=new AppSettings { SwapComparisons=[swap,swap.Copy()],Holdings=[new(){Symbol="NEARUSDT",Quantity=1}] };
        Assert.Equal(1,MarketDemand.Quotes(settings).Count(s=>s=="QNTUSDT"));
        settings.SwapComparisons.ForEach(s=>s.Enabled=false);
        Assert.DoesNotContain("QNTUSDT",MarketDemand.Quotes(settings));Assert.Contains("NEARUSDT",MarketDemand.Quotes(settings));
    }
}
