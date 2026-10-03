using System.Text.Json;
using BinanceTicker.Core.Models;
namespace BinanceTicker.Tests;
internal static class CandleFixtures
{
    public static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1800000000000);
    public static CandlePrice Minute(DateTimeOffset open, decimal price = 100m, bool closed = true, DateTimeOffset? updated = null, string symbol = "BTCUSDT") =>
        new(symbol, open, open.AddMinutes(1).AddMilliseconds(-1), price, price, price, price, 1m, closed,
            updated ?? (closed ? open.AddMinutes(1) : open.AddSeconds(30)));
    public static string Rows(DateTimeOffset start, int count) => JsonSerializer.Serialize(Enumerable.Range(0, count).Select(i =>
        new object[] { start.AddMinutes(i).ToUnixTimeMilliseconds(), "100", "110", "90", "105", "1",
            start.AddMinutes(i + 1).AddMilliseconds(-1).ToUnixTimeMilliseconds(), "105", 1, "1", "105", "0" }));
    public static Dictionary<string, string> Query(Uri uri) => uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
        .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
}
