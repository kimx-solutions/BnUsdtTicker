namespace BinanceTicker.Core.Models;

public sealed record AlertHistoryEntry(string Symbol, AlertType AlertType, decimal TargetPrice,
    decimal TriggeredPrice, DateTimeOffset TriggeredAt);
