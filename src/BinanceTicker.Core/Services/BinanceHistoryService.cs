using System.Globalization;
using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public interface IBinanceHistoryService
{
    Task<IReadOnlyList<CandlePrice>> GetCandlesAsync(string symbol, DateTimeOffset start, DateTimeOffset end, CancellationToken token);
}

public sealed class BinanceHistoryService(HistoryRequestScheduler scheduler) : IBinanceHistoryService
{
    public async Task<IReadOnlyList<CandlePrice>> GetCandlesAsync(string symbol, DateTimeOffset start, DateTimeOffset end, CancellationToken token)
    {
        symbol = SymbolNormalizer.Normalize(symbol);
        if (end < start) throw new ArgumentException("Invalid history range");
        var result = new List<CandlePrice>();
        var cursor = start;
        while (cursor <= end)
        {
            token.ThrowIfCancellationRequested();
            var uri = new Uri("https://api.binance.com/api/v3/klines?symbol=" + Uri.EscapeDataString(symbol) +
                "&interval=1m&limit=1000&startTime=" + cursor.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) +
                "&endTime=" + end.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
            using var response = await scheduler.GetAsync(uri, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected candle array");
            var page = doc.RootElement.EnumerateArray().Select(row => CandleParser.ParseRest(row, symbol, end)).ToArray();
            if (page.Length > 1000) throw new JsonException("Oversized candle page");
            DateTimeOffset? previous = null;
            foreach (var candle in page)
            {
                if (candle.OpenTime < cursor || candle.OpenTime > end || (previous is not null && candle.OpenTime <= previous))
                    throw new JsonException("Non-advancing or out-of-range candle page");
                previous = candle.OpenTime;
            }
            result.AddRange(page);
            if (page.Length < 1000) break;
            cursor = page[^1].OpenTime.AddMinutes(1);
        }
        return result.AsReadOnly();
    }
}
