namespace BinanceTicker.Core.Models;

public sealed record AlertHistoryEntry(string Symbol, AlertType AlertType, decimal? TargetPrice,
    decimal TriggeredPrice, DateTimeOffset TriggeredAt, int? WindowMinutes = null,
    decimal? ThresholdPercent = null, decimal? BaselinePrice = null,
    DateTimeOffset? BaselineAt = null, DateTimeOffset? QuoteAt = null, decimal? ChangePercent = null);
