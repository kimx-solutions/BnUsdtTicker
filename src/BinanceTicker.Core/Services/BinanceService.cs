using System.Net;
using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public interface IBinanceService
{
    Task<bool> IsValidSymbolAsync(string symbol, CancellationToken cancellationToken);
    Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken);
}

public sealed class BinanceService(HttpClient client) : IBinanceService
{
    private const string BaseUrl = "https://api.binance.com/api/v3/";

    public async Task<bool> IsValidSymbolAsync(string symbol, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(BaseUrl + "exchangeInfo?symbol=" + Uri.EscapeDataString(symbol), cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (error.RootElement.TryGetProperty("code", out var code) && code.GetInt32() == -1121) return false;
        }
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return doc.RootElement.GetProperty("symbols").EnumerateArray().Any(s =>
            s.GetProperty("symbol").GetString() == symbol && s.GetProperty("quoteAsset").GetString() == "USDT" &&
            s.GetProperty("status").GetString() == "TRADING" && s.GetProperty("isSpotTradingAllowed").GetBoolean());
    }

    public async Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken)
    {
        var prices = new List<TickerPrice>();
        foreach (var batch in symbols.Chunk(100))
        {
            var query = Uri.EscapeDataString(JsonSerializer.Serialize(batch));
            using var response = await client.GetAsync(BaseUrl + "ticker/24hr?symbols=" + query, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Expected ticker array");
            prices.AddRange(doc.RootElement.EnumerateArray().Select(TickerParser.ParseRest));
        }
        return prices;
    }
}
