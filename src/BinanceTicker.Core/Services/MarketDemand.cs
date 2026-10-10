using BinanceTicker.Core.Models;
namespace BinanceTicker.Core.Services;

public static class MarketDemand
{
    public static bool HasAlert(PriceAlertSettings alert) => alert.UpperPrice is not null || alert.LowerPrice is not null ||
        alert.Rise.ThresholdPercent is not null || alert.Fall.ThresholdPercent is not null;

    public static string[] Quotes(AppSettings settings) => (settings.Watchlists is null
        ? settings.Symbols.Where(s=>s.Enabled).Select(s=>s.Symbol)
        : settings.Watchlists.SelectMany(g=>g.Members).Where(m=>m.Enabled).Select(m=>m.Symbol))
        .Concat(settings.Holdings.Where(h=>h.Quantity>0).Select(h=>h.Symbol))
        .Concat(settings.SwapComparisons.Where(s=>s.Enabled && s.InvalidReason is null && !s.IsClosed).SelectMany(s=>new[] { s.FromSymbol,s.ToSymbol }))
        .Concat(settings.Symbols.Where(s=>s.Enabled && HasAlert(s.Alert)).Select(s=>s.Symbol))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
