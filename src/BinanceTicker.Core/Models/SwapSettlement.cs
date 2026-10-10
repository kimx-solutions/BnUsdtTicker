namespace BinanceTicker.Core.Models;

public enum SwapSettlementMode { Actual, MarketQuote }

public sealed record SwapSettlement
{
    public string Id { get; init; }=Guid.NewGuid().ToString("N");
    public SwapSettlementMode Mode { get; init; }
    public decimal ToQuantity { get; init; }
    public decimal FromQuantity { get; init; }
    public decimal ReturnedQuantity { get; init; }
    public DateTimeOffset SettledAt { get; init; }
    public decimal? FromPrice { get; init; }
    public decimal? ToPrice { get; init; }
    public DateTimeOffset? FromQuoteAt { get; init; }
    public DateTimeOffset? ToQuoteAt { get; init; }
    public string Note { get; init; }="";
}
