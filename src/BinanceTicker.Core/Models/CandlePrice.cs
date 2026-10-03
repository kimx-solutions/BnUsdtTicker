namespace BinanceTicker.Core.Models;

public sealed record CandlePrice(string Symbol, DateTimeOffset OpenTime, DateTimeOffset CloseTime,
    decimal Open, decimal High, decimal Low, decimal Close, decimal Volume, bool IsClosed, DateTimeOffset UpdatedAt);
