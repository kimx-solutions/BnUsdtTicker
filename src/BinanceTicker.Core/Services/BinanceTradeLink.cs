namespace BinanceTicker.Core.Services;

public static class BinanceTradeLink
{
    public static Uri Create(string symbol)
    {
        var normalized = SymbolNormalizer.Normalize(symbol);
        return new Uri($"https://www.binance.com/en/trade/{normalized[..^4]}_USDT?type=spot");
    }
}
