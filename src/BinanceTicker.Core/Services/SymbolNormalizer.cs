namespace BinanceTicker.Core.Services;

public static class SymbolNormalizer
{
    public static string Normalize(string input)
    {
        var value = (input ?? "").Trim().ToUpperInvariant();
        if (value.EndsWith("/USDT", StringComparison.Ordinal)) value = value.Replace("/USDT", "USDT");
        var asset = value.EndsWith("USDT", StringComparison.Ordinal) ? value[..^4] : value;
        if (asset.Length is < 1 or > 20 || asset.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("請輸入幣種代號，例如 BTC、ETH 或 BTCUSDT。");
        return asset + "USDT";
    }
}
