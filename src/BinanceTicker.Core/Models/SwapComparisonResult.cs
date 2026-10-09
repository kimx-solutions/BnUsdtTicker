namespace BinanceTicker.Core.Models;
public sealed record SwapComparisonResult(decimal? FromValue=null, decimal? ToValue=null,
    decimal? ReturnQuantity=null, decimal? QuantityDifference=null, decimal? UsdtDifference=null,
    decimal? ReturnPercent=null, bool IsStale=false, string StateText="");
