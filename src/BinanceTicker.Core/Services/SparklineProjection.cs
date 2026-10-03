using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public static class SparklineProjection
{
    public static SparklineSeries Create(IReadOnlyList<CandlePrice> candles, DateTimeOffset end, TimeSpan range, int maxPoints = 300)
    {
        if (range <= TimeSpan.Zero || maxPoints < 4) throw new ArgumentOutOfRangeException(nameof(range));
        var start = end - range;
        var groups = new List<List<SparklinePoint>>();
        DateTimeOffset? previousOpen = null;
        foreach (var candle in candles.GroupBy(c => c.OpenTime).Select(g => g.OrderByDescending(c => c.UpdatedAt).First()).OrderBy(c => c.OpenTime))
        {
            var time = candle.IsClosed ? candle.CloseTime : (candle.UpdatedAt > end ? end : candle.UpdatedAt);
            if (time < start.AddMinutes(-1) || time > end || candle.OpenTime > end) continue;
            if (previousOpen is null || candle.OpenTime - previousOpen != TimeSpan.FromMinutes(1)) groups.Add([]);
            groups[^1].Add(new(time, candle.Close));
            previousOpen = candle.OpenTime;
        }
        foreach (var group in groups)
            while (group.Count > 1 && group[1].Time < start) group.RemoveAt(0);
        groups.RemoveAll(g => g.Count == 0 || g[^1].Time < start);
        var all = groups.SelectMany(g => g).ToArray();
        var complete = groups.Count == 1 && all.Length >= 2 && all[0].Time <= start && all[^1].Time >= end.AddMinutes(-1);
        var positive = all.Length < 2 || all[^1].Price >= all[0].Price;
        if (all.Length > maxPoints)
        {
            var keep = new HashSet<SparklinePoint> { all[0], all[^1], all.MinBy(p => p.Price)!, all.MaxBy(p => p.Price)! };
            var buckets = Math.Max(1, (maxPoints - 4) / 2);
            foreach (var bucket in all.GroupBy(p => Math.Clamp((int)((p.Time - start).TotalSeconds / range.TotalSeconds * buckets), 0, buckets - 1)))
            {
                keep.Add(bucket.MinBy(p => p.Price)!); keep.Add(bucket.MaxBy(p => p.Price)!);
            }
            groups = groups.Select(g => g.Where(keep.Contains).ToList()).Where(g => g.Count > 0).ToList();
        }
        return new(Array.AsReadOnly(groups.Select(g => new SparklineSegment(g.AsReadOnly())).ToArray()), start, end, complete, positive);
    }
}
