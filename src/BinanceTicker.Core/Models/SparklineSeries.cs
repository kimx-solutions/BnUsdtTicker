namespace BinanceTicker.Core.Models;

public sealed record SparklinePoint(DateTimeOffset Time, decimal Price);
public sealed record SparklineSegment(IReadOnlyList<SparklinePoint> Points);
public sealed record SparklineSeries(IReadOnlyList<SparklineSegment> Segments,
    DateTimeOffset Start, DateTimeOffset End, bool IsComplete, bool IsPositive)
{
    public bool HasData => Segments.Any(segment => segment.Points.Count >= 2);
}
