using System.Text.Json;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public sealed class MarketFeed(IBinanceService rest, BinanceWebSocketService stream)
{
    public async Task RunAsync(IReadOnlyList<string> symbols, Action<TickerPrice> onPrice,
        Action<ConnectionStatus> onStatus, CancellationToken cancellationToken, Action<CandlePrice>? onCandle = null)
    {
        if (symbols.Count == 0) { onStatus(ConnectionStatus.Disconnected); return; }
        onStatus(ConnectionStatus.Connecting);
        try
        {
            foreach (var price in await rest.GetPricesAsync(symbols, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                onPrice(price);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or
                                   FormatException or KeyNotFoundException)
        { onStatus(ConnectionStatus.Disconnected); }
        if (!cancellationToken.IsCancellationRequested)
            await stream.RunAsync(symbols, onPrice, onStatus, cancellationToken, onCandle);
    }
}
