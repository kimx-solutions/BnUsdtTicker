using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class WatchlistSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    [Fact]
    public void LegacySettingsMigrateMembersAndAlerts()
    {
        var settings = new AppSettings { Symbols = [new() { Symbol="ETHUSDT", Order=1 },
            new() { Symbol="BTCUSDT", Order=2, Enabled=false, Alert=new() { UpperPrice=100, UpperTriggered=true,
                Rise=new() { ThresholdPercent=5, Triggered=true } } }] };
        settings.Ui.ShowSparkline=false;
        WatchlistSettings.Normalize(settings);
        var group=Assert.Single(settings.Watchlists!);
        Assert.Equal("預設分組",group.Name);
        Assert.Equal(new[] { "ETHUSDT","BTCUSDT" },group.Members.Select(m=>m.Symbol));
        Assert.False(group.Members[1].Enabled);
        Assert.False(group.Ui.ShowSparkline);
        Assert.True(settings.Symbols[1].Alert.UpperTriggered);
        Assert.True(settings.Symbols[1].Alert.Rise.Triggered);
        Assert.Empty(settings.Holdings);
    }
    [Fact]
    public void EmptyLegacyListStaysEmpty()
    {
        var settings=new AppSettings { Symbols=[] }; WatchlistSettings.Normalize(settings);
        Assert.Empty(Assert.Single(settings.Watchlists!).Members);
    }
    [Fact]
    public void NewEmptyGroupIsNotRemigrated()
    {
        var settings=new AppSettings { Watchlists=[new() { Id="a",Name="觀察",Members=[] }] };
        WatchlistSettings.Normalize(settings);
        Assert.Empty(Assert.Single(settings.Watchlists!).Members);
        Assert.Equal("a",settings.ActiveWatchlistId);
    }
    [Fact]
    public void CopiesAreIndependent()
    {
        var settings=new AppSettings(); WatchlistSettings.Normalize(settings);
        settings.Holdings.Add(new() { Symbol="BTCUSDT",Quantity=2,AverageCost=10 });
        var copy=settings.Copy(); copy.Watchlists![0].Members[0].Enabled=false;
        copy.Watchlists[0].Ui.ShowSparkline=false; copy.Holdings[0].Quantity=3;
        Assert.True(settings.Watchlists![0].Members[0].Enabled);
        Assert.True(settings.Watchlists[0].Ui.ShowSparkline); Assert.Equal(2,settings.Holdings[0].Quantity);
    }
    [Fact]
    public void GroupsAndHoldingsRoundTrip()
    {
        var settings=new AppSettings(); WatchlistSettings.Normalize(settings);
        settings.Watchlists!.Add(new() { Id="second",Name="短線",Members=[new() { Symbol="BTCUSDT" }] });
        settings.ActiveWatchlistId="second";
        settings.Holdings.Add(new() { Symbol="BTCUSDT",Quantity=0.123456789m,AverageCost=100.75m });
        var service=new SettingsService(Path.Combine(directory,"settings.json"));service.Save(settings);
        var loaded=service.Load();Assert.Equal(2,loaded.Watchlists!.Count);Assert.Equal("second",loaded.ActiveWatchlistId);
        Assert.Equal(0.123456789m,Assert.Single(loaded.Holdings).Quantity);Assert.Null(service.LoadWarning);
    }
    [Fact]
    public void InvalidActiveIdFallsBack()
    {
        var settings=new AppSettings { ActiveWatchlistId="missing" }; WatchlistSettings.Normalize(settings);
        Assert.Equal(settings.Watchlists![0].Id,settings.ActiveWatchlistId);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("預設分組")]
    public void RejectsInvalidGroupNames(string name)
    {
        var settings=new AppSettings();WatchlistSettings.Normalize(settings);
        settings.Watchlists!.Add(new() { Id="b",Name=name });
        Assert.Throws<ArgumentException>(()=>WatchlistSettings.Normalize(settings));
    }
    [Fact]
    public void RejectsDuplicateMembersAndNegativeHoldings()
    {
        var settings=new AppSettings();WatchlistSettings.Normalize(settings);
        settings.Watchlists![0].Members.Add(new() { Symbol="BTCUSDT" });
        Assert.Throws<ArgumentException>(()=>WatchlistSettings.Normalize(settings));
        settings.Watchlists[0].Members.RemoveAt(3);
        settings.Holdings.Add(new() { Symbol="BTCUSDT",Quantity=-1 });
        Assert.Throws<ArgumentException>(()=>WatchlistSettings.Normalize(settings));
    }
    public void Dispose() { if(Directory.Exists(directory))Directory.Delete(directory,true); }
}
