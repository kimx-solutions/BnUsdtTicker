using System.Globalization;
using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public static class TickerParser
{
    public static TickerPrice ParseRest(JsonElement data) => Parse(data, "symbol", "lastPrice", "priceChangePercent", "closeTime");
    public static TickerPrice ParseStream(JsonElement data)
    {
        if (data.TryGetProperty("data", out var payload)) data = payload;
        return Parse(data, "s", "c", "P", "E", true);
    }

    private static TickerPrice Parse(JsonElement data, string symbol, string price, string change, string time, bool stream = false)
    {
        try
        {
            var name = data.GetProperty(symbol).GetString();
            var priceValue = decimal.Parse(data.GetProperty(price).GetString()!, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(name) || priceValue < 0) throw new JsonException("Invalid ticker value");
            return new(name, priceValue,
                decimal.Parse(data.GetProperty(change).GetString()!, CultureInfo.InvariantCulture),
                DateTimeOffset.FromUnixTimeMilliseconds(data.GetProperty(time).GetInt64()).UtcDateTime,
                OptionalDecimal(data, stream ? "h" : "highPrice"),
                OptionalDecimal(data, stream ? "l" : "lowPrice"),
                OptionalDecimal(data, stream ? "v" : "volume"),
                OptionalDecimal(data, stream ? "q" : "quoteVolume"), stream ? QuoteSource.Stream : QuoteSource.Rest);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or
                                   KeyNotFoundException or InvalidOperationException)
        { throw new JsonException("Invalid ticker payload", ex); }
    }

    private static decimal? OptionalDecimal(JsonElement data, string name) =>
        data.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String &&
        decimal.TryParse(field.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
        value >= 0 ? value : null;
}
