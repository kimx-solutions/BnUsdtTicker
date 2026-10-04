namespace BinanceTicker.Core.Models;

public enum QuoteSource { Unknown, Rest, Stream }

public sealed record TickerPrice(string Symbol, decimal Price, decimal ChangePercent24h, DateTime UpdatedAt,
    decimal? HighPrice24h = null, decimal? LowPrice24h = null,
    decimal? Volume24h = null, decimal? QuoteVolume24h = null, QuoteSource Source = QuoteSource.Unknown);
