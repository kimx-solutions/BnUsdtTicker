using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public sealed record QuoteComparison(decimal BaselinePrice, DateTimeOffset BaselineAt,
    decimal Price, DateTimeOffset QuoteAt, decimal ChangePercent);

public sealed class QuoteWindow
{
    private readonly Dictionary<string, List<(DateTimeOffset At, decimal Price)>> samples = new(StringComparer.Ordinal);
    public bool Add(TickerPrice quote, DateTimeOffset receivedAt, TimeSpan retention)
    {
        var at = new DateTimeOffset(quote.UpdatedAt.ToUniversalTime());
        if (quote.Source != QuoteSource.Stream || quote.Price <= 0 || quote.UpdatedAt == default ||
            (receivedAt - at).Duration() > TimeSpan.FromSeconds(5)) return false;
        if (!samples.TryGetValue(quote.Symbol, out var series)) samples[quote.Symbol] = series = [];
        if (series.Count > 0)
        {
            if (at <= series[^1].At) return false;
            if (at - series[^1].At > TimeSpan.FromSeconds(5)) series.Clear();
        }
        series.Add((at, quote.Price));
        var cutoff = at - retention - TimeSpan.FromSeconds(5);
        var count = series.FindIndex(s => s.At >= cutoff);
        if (count > 0) series.RemoveRange(0, count);
        return true;
    }
    public QuoteComparison? Compare(string symbol, DateTimeOffset quoteAt, int windowMinutes)
    {
        if (!samples.TryGetValue(symbol, out var series) || series.Count == 0 || series[^1].At != quoteAt) return null;
        var target = quoteAt.AddMinutes(-windowMinutes);
        var index = series.FindLastIndex(s => s.At <= target);
        if (index < 0 || target - series[index].At > TimeSpan.FromSeconds(5)) return null;
        var baseline = series[index];
        try { return new(baseline.Price, baseline.At, series[^1].Price, quoteAt, (series[^1].Price / baseline.Price - 1) * 100); }
        catch (OverflowException) { return null; }
    }
    public void Clear(string? symbol = null)
    {
        if (symbol is null) samples.Clear(); else samples.Remove(symbol);
    }
}
