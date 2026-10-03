using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class SparklineProjectionTests
{
    [Fact]
    public void RangeUsesActualTimesAndDoesNotStretchPartialData()
    {
        var series = Project(Enumerable.Range(-10, 10).Select(i => CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(i))).ToArray(), TimeSpan.FromHours(24));
        Assert.Equal(CandleFixtures.Now.AddHours(-24), (DateTimeOffset)series.Start);
        Assert.Equal(CandleFixtures.Now, (DateTimeOffset)series.End);
        Assert.False((bool)series.IsComplete);
        Assert.True(Points(series).Min(p => p.Time) > (DateTimeOffset)series.Start + TimeSpan.FromHours(23));
    }
    [Fact]
    public void GapsProduceSeparateSegments()
    {
        var series = Project(new[] { -6, -5, -2, -1 }.Select(i => CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(i))).ToArray(), TimeSpan.FromHours(1));
        Assert.Equal(2, (int)series.Segments.Count);
        Assert.False((bool)series.IsComplete);
    }
    [Fact]
    public void DownsamplingRetainsEndpointsAndExtrema()
    {
        var candles = Enumerable.Range(-1441, 1441).Select(i => CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(i), i == -900 ? 999m : i == -500 ? 1m : 100m)).ToArray();
        var series = Project(candles, TimeSpan.FromHours(24));
        var points = Points(series);
        Assert.True(points.Count <= 300); Assert.Equal(999m, points.Max(p => p.Price)); Assert.Equal(1m, points.Min(p => p.Price));
        Assert.Equal(candles[0].CloseTime, points[0].Time); Assert.Equal(candles[^1].CloseTime, points[^1].Time);
        Assert.True((bool)series.IsComplete);
    }
    [Fact]
    public void FutureLiveTimestampDoesNotDrawPastEnd()
    {
        var series = Project([CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1)), CandleFixtures.Minute(CandleFixtures.Now, 200, false, CandleFixtures.Now.AddSeconds(30))], TimeSpan.FromHours(1));
        Assert.All(Points(series), point => Assert.True(point.Time <= CandleFixtures.Now));
    }
    [Fact]
    public void FlatSeriesAndZeroPricesRemainValid()
    {
        var series = Project([CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-2), 0), CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1), 0)], TimeSpan.FromHours(1));
        Assert.Equal(2, Points(series).Count); Assert.True((bool)series.IsPositive);
    }
    private static SparklineSeries Project(IReadOnlyList<CandlePrice> candles, TimeSpan range) =>
        SparklineProjection.Create(candles, CandleFixtures.Now, range);
    private static List<(DateTimeOffset Time, decimal Price)> Points(SparklineSeries series) =>
        series.Segments.SelectMany(segment => segment.Points).Select(point => (point.Time, point.Price)).ToList();
}
