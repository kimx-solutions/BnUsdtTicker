namespace BinanceTicker.Core.Models;

public sealed record TickerPrice(string Symbol, decimal Price, decimal ChangePercent24h, DateTime UpdatedAt);
