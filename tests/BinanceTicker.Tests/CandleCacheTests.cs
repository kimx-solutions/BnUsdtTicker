using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class CandleCacheTests
{
    [Fact]
    public void NewerStreamWinsOverLateRest()
    {
        var cache = NewCache(); cache.Configure(new[] { "BTCUSDT" });
        var open = CandleFixtures.Now.AddMinutes(-1);
        var newer = CandleFixtures.Minute(open, 200, false, open.AddSeconds(50));
        Assert.True((bool)cache.Merge(newer, CandleFixtures.Now));
        Assert.False((bool)cache.Merge(CandleFixtures.Minute(open, 100, false, open.AddSeconds(20)), CandleFixtures.Now));
        Assert.Equal(200m, ((IReadOnlyList<CandlePrice>)cache.GetSnapshot("BTCUSDT", CandleFixtures.Now)).Single().Close);
    }
    [Fact]
    public void ClosedCandleNeverReopens()
    {
        var cache = NewCache(); cache.Configure(new[] { "BTCUSDT" });
        var candle = CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1));
        cache.Merge(candle, CandleFixtures.Now);
        Assert.False((bool)cache.Merge(candle with { IsClosed = false, UpdatedAt = candle.UpdatedAt.AddSeconds(2), Close = 101, High = 101 }, CandleFixtures.Now));
        Assert.True(((IReadOnlyList<CandlePrice>)cache.GetSnapshot("BTCUSDT", CandleFixtures.Now)).Single().IsClosed);
    }
    [Fact]
    public void CacheKeepsBoundaryAndAtMost1500()
    {
        var cache = NewCache(); cache.Configure(new[] { "BTCUSDT" });
        for (var i = -3000; i <= 0; i++) cache.Merge(CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(i), closed: i < 0), CandleFixtures.Now);
        var values = (IReadOnlyList<CandlePrice>)cache.GetSnapshot("BTCUSDT", CandleFixtures.Now);
        Assert.Equal(1442, values.Count); Assert.True(values.Count <= 1500);
        Assert.Equal(CandleFixtures.Now.AddMinutes(-1441), values[0].OpenTime);
        Assert.Throws<NotSupportedException>(() => ((IList<CandlePrice>)values)[0] = values[^1]);
    }
    [Fact]
    public void UnknownAndRemovedSymbolsStayAbsent()
    {
        var cache = NewCache(); cache.Configure(new[] { "BTCUSDT" });
        var candle = CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1));
        Assert.False((bool)cache.Merge(candle with { Symbol = "ETHUSDT" }, CandleFixtures.Now));
        cache.Merge(candle, CandleFixtures.Now); cache.Configure(Array.Empty<string>());
        Assert.False((bool)cache.Merge(candle, CandleFixtures.Now));
        Assert.Empty((IReadOnlyList<CandlePrice>)cache.GetSnapshot("BTCUSDT", CandleFixtures.Now));
    }
    private static CandleCache NewCache() => new();
}
