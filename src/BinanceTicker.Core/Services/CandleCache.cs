using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public sealed class CandleCache
{
    private readonly object gate = new();
    private readonly Dictionary<string, SortedDictionary<DateTimeOffset, CandlePrice>> data = new(StringComparer.Ordinal);

    public void Configure(IReadOnlyCollection<string> symbols)
    {
        var active = symbols.Select(SymbolNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);
        lock (gate)
        {
            foreach (var symbol in data.Keys.Where(s => !active.Contains(s)).ToArray()) data.Remove(symbol);
            foreach (var symbol in active) data.TryAdd(symbol, new());
        }
    }

    public bool Merge(CandlePrice candle, DateTimeOffset now)
    {
        try { if (!CandleParser.IsValid(candle) || candle.OpenTime > now) return false; }
        catch (ArgumentException) { return false; }
        lock (gate)
        {
            if (!data.TryGetValue(candle.Symbol, out var rows)) return false;
            if (rows.TryGetValue(candle.OpenTime, out var existing) &&
                (existing.UpdatedAt > candle.UpdatedAt || (existing.IsClosed && !candle.IsClosed))) return false;
            rows[candle.OpenTime] = candle;
            Trim(rows, now);
            return true;
        }
    }

    public IReadOnlyList<CandlePrice> GetSnapshot(string symbol, DateTimeOffset now)
    {
        lock (gate)
        {
            if (!data.TryGetValue(symbol, out var rows)) return Array.Empty<CandlePrice>();
            Trim(rows, now);
            return Array.AsReadOnly(rows.Values.ToArray());
        }
    }

    private static void Trim(SortedDictionary<DateTimeOffset, CandlePrice> rows, DateTimeOffset now)
    {
        var oldest = DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor((now.AddHours(-24).ToUnixTimeMilliseconds()) / 60000d) * 60000).AddMinutes(-1);
        foreach (var key in rows.Keys.TakeWhile(key => key < oldest).ToArray()) rows.Remove(key);
        while (rows.Count > 1500) rows.Remove(rows.Keys.First());
    }
}
