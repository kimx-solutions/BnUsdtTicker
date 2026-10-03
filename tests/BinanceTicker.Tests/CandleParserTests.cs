using System.Text.Json;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Tests;
public sealed class CandleParserTests
{
    [Fact]
    public void OptionalCurrentCandleIsNotClosedEarly()
    {
        using var doc = JsonDocument.Parse(CandleFixtures.Rows(CandleFixtures.Now, 1));
        var candle = CandleParser.ParseRest(doc.RootElement[0], "BTCUSDT", CandleFixtures.Now.AddSeconds(30));
        Assert.Equal(105m, candle.Close); Assert.False(candle.IsClosed);
        Assert.Equal(TimeSpan.FromMilliseconds(59999), candle.CloseTime - candle.OpenTime);
    }
    [Fact]
    public void ParsesCombinedStreamAndRejectsWrongIntervalOrSymbol()
    {
        var start = CandleFixtures.Now.ToUnixTimeMilliseconds();
        var json = JsonSerializer.Serialize(new Dictionary<string, object> { ["data"] = new Dictionary<string, object>
        {
            ["e"] = "kline", ["E"] = start + 30000, ["s"] = "BTCUSDT",
            ["k"] = new Dictionary<string, object> { ["s"] = "BTCUSDT", ["i"] = "1m", ["t"] = start,
                ["T"] = start + 59999, ["o"] = "100", ["h"] = "110", ["l"] = "90", ["c"] = "105", ["v"] = "2", ["x"] = false }
        } });
        using var doc = JsonDocument.Parse(json);
        var candle = CandleParser.ParseStream(doc.RootElement);
        Assert.Equal(2m, candle.Volume); Assert.Equal(CandleFixtures.Now.AddSeconds(30), candle.UpdatedAt);
        foreach (var invalid in new[] { json.Replace("1m", "5m"), json.Replace("\"h\":\"110\"", "\"h\":\"80\"") })
        { using var bad = JsonDocument.Parse(invalid); Assert.Throws<JsonException>(() => CandleParser.ParseStream(bad.RootElement)); }
    }
}
