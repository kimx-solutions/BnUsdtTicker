using System.Globalization;
using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public static class CandleParser
{
    public static CandlePrice ParseRest(JsonElement data, string symbol, DateTimeOffset asOf) => Recover(() =>
    {
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() < 12) throw new JsonException("Invalid candle tuple");
        var open = Timestamp(data[0]); var close = Timestamp(data[6]);
        return Validate(new(symbol, open, close, Number(data[1]), Number(data[2]), Number(data[3]),
            Number(data[4]), Number(data[5]), close <= asOf, asOf));
    });

    public static CandlePrice ParseStream(JsonElement data) => Recover(() =>
    {
        if (data.TryGetProperty("data", out var payload)) data = payload;
        if (data.GetProperty("e").GetString() != "kline") throw new JsonException("Not a candle event");
        var k = data.GetProperty("k"); var symbol = data.GetProperty("s").GetString()!;
        if (k.GetProperty("i").GetString() != "1m" || k.GetProperty("s").GetString() != symbol)
            throw new JsonException("Unexpected candle symbol or interval");
        return Validate(new(symbol, Timestamp(k.GetProperty("t")), Timestamp(k.GetProperty("T")),
            Number(k.GetProperty("o")), Number(k.GetProperty("h")), Number(k.GetProperty("l")),
            Number(k.GetProperty("c")), Number(k.GetProperty("v")), k.GetProperty("x").GetBoolean(),
            Timestamp(data.GetProperty("E"))));
    });

    public static bool IsValid(CandlePrice candle) =>
        candle.Symbol == SymbolNormalizer.Normalize(candle.Symbol) &&
        candle.OpenTime.ToUnixTimeMilliseconds() % 60000 == 0 &&
        candle.CloseTime - candle.OpenTime == TimeSpan.FromMilliseconds(59999) &&
        candle.Open >= 0 && candle.Close >= 0 && candle.Low >= 0 && candle.Volume >= 0 &&
        candle.High >= Math.Max(candle.Open, candle.Close) && candle.Low <= Math.Min(candle.Open, candle.Close) &&
        candle.Low <= candle.High && candle.UpdatedAt >= candle.OpenTime &&
        (!candle.IsClosed || candle.UpdatedAt >= candle.CloseTime);

    private static CandlePrice Validate(CandlePrice candle)
    { if (!IsValid(candle)) throw new JsonException("Invalid candle values"); return candle; }
    private static DateTimeOffset Timestamp(JsonElement field) => DateTimeOffset.FromUnixTimeMilliseconds(field.GetInt64());
    private static decimal Number(JsonElement field) => decimal.Parse(field.GetString()!, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static CandlePrice Recover(Func<CandlePrice> parse)
    {
        try { return parse(); }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or
            InvalidOperationException or KeyNotFoundException)
        { throw new JsonException("Invalid candle payload", ex); }
    }
}
