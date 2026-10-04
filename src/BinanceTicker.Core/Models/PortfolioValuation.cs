using System.Globalization;
namespace BinanceTicker.Core.Models;

public sealed record HoldingValuation(decimal? MarketValue, decimal? CostBasis, decimal? Profit, decimal? ProfitPercent,
    bool MissingQuote=false, bool IsStale=false, bool Overflowed=false, DateTime? UpdatedAt=null);

public sealed record PortfolioSummary(decimal? MarketValue, decimal? CostBasis, decimal? Profit, decimal? ProfitPercent,
    decimal PartialMarketValue, decimal PartialProfit, int HoldingCount, int MissingCount, bool IsStale)
{
    public string Text => HoldingCount == 0 ? "尚未設定持倉" :
        MissingCount > 0 ? $"總值 — · 損益 — · {MissingCount} 筆缺資料／超出範圍\n可計算小計 {Money(PartialMarketValue)} USDT · 損益 {Money(PartialProfit)} USDT" + StaleText :
        $"市值 {Money(MarketValue)} USDT\n損益 {Money(Profit)} USDT · {Percent(ProfitPercent)}" + StaleText;
    private string StaleText => IsStale ? "\n使用最後報價" : "";
    public static string Money(decimal? value) => value?.ToString("#,##0.00########",CultureInfo.InvariantCulture) ?? "—";
    public static string Percent(decimal? value) => value?.ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture) + (value is null ? "—" : "%");
}
