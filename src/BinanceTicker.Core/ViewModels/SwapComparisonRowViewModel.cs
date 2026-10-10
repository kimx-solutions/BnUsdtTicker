using System.Globalization;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Core.ViewModels;

public sealed class SwapComparisonRowViewModel(SwapComparisonSetting setting) : ObservableObject
{
    public SwapComparisonSetting Setting { get; } = setting.Copy();
    private SwapComparisonResult result=new(StateText:"等待報價");
    private TickerPrice? fromQuote,toQuote;
    public SwapComparisonResult Result => result;
    public string FromAsset => SwapComparisonCalculator.Asset(Setting.FromSymbol);
    public string ToAsset => SwapComparisonCalculator.Asset(Setting.ToSymbol);
    public string Title => Setting.InvalidReason is not null ? "損壞的換幣紀錄" :
        Setting.IsClosed ? OriginalTitle : $"{Exact(Setting.RemainingFromQuantity)} {FromAsset} → {Exact(Setting.RemainingToQuantity)} {ToAsset}";
    public string OriginalTitle => $"{Exact(Setting.FromQuantity)} {FromAsset} → {Exact(Setting.ToQuantity)} {ToAsset}";
    public string TimeText => Setting.InvalidReason is not null ? "請修正或刪除，原檔備份已保留" :
        (Setting.IsClosed ? Setting.Settlements.Last().SettledAt : Setting.SwappedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public bool IsOpen => !Setting.IsClosed;
    public bool HasSettlements => Setting.Settlements.Count>0;
    public string ReturnLabel => Setting.IsClosed ? "累計結算換回" : "剩餘現在可換回";
    public string FromValueLabel => Setting.IsClosed ? "原幣基準市值 · 各次結算參考價" : $"剩餘原幣若持有至今 · {FromAsset}";
    public string ToValueLabel => Setting.IsClosed ? "結算換回市值 · 各次結算參考價" : $"剩餘換入幣現在市值 · {ToAsset}";
    public string SettlementSummary
    {
        get
        {
            if(!HasSettlements)return "";
            var total=SwapComparisonCalculator.SettledValue(Setting);
            return $"原始換幣：{OriginalTitle} · {Setting.SwappedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n已結算 {Exact(Setting.Settlements.Sum(s=>s.ToQuantity))} {ToAsset}，累計換回 {Quantity(total.ReturnQuantity)} {FromAsset} · 原幣差額 {Signed(total.QuantityDifference)} {FromAsset}\nUSDT 差額 {SignedMoney(total.UsdtDifference)} · 相對報酬率 {Percent(total.ReturnPercent)}（未填參考價時 USDT 差額留空）";
        }
    }
    public IReadOnlyList<SwapSettlementRowViewModel> SettlementRows => Setting.Settlements.Select(s=>new SwapSettlementRowViewModel(s,FromAsset,ToAsset)).ToArray();
    public string ReturnText => $"{Quantity(result.ReturnQuantity)} {FromAsset}";
    public string QuantityDifferenceText => $"{Signed(result.QuantityDifference)} {FromAsset}";
    public string UsdtDifferenceText => SignedMoney(result.UsdtDifference);
    public string ReturnPercentText => Percent(result.ReturnPercent);
    public string DifferenceText => $"{QuantityDifferenceText} · {UsdtDifferenceText} USDT · {ReturnPercentText}";
    public string FromValueText => Money(result.FromValue) + " USDT";
    public string ToValueText => Money(result.ToValue) + " USDT";
    public string FromQuoteText => fromQuote is null ? "—" : PriceFormatter.Format(fromQuote.Price) + " USDT";
    public string ToQuoteText => toQuote is null ? "—" : PriceFormatter.Format(toQuote.Price) + " USDT";
    public string FromQuoteTimeText => QuoteTime(fromQuote);
    public string ToQuoteTimeText => QuoteTime(toQuote);
    public string ExactResultsText => $"可換回：{Full(result.ReturnQuantity)} {FromAsset}\n原幣差額：{Full(result.QuantityDifference)} {FromAsset}\nUSDT 差額：{Full(result.UsdtDifference)}\n相對報酬率：{Full(result.ReturnPercent)}%";
    public string StateText => result.StateText;
    public string ValuesText => $"原幣若持有至今：{Money(result.FromValue)} USDT\n換入幣現在市值：{Money(result.ToValue)} USDT";
    public string QuotesText => QuoteText(fromQuote,FromAsset)+"\n"+QuoteText(toQuote,ToAsset);
    public string FormulaText => Setting.IsClosed ? "已結算結果依各次保存的換回數量與參考價加總，不再隨即時報價更新。實際成交與報價結算分別標示於下方明細。" :
        $"可換回 {FromAsset} = {Exact(Setting.RemainingToQuantity)} × {ToAsset} 現價 ÷ {FromAsset} 現價\n幣數差額 = 可換回數量 − {Exact(Setting.RemainingFromQuantity)}\nUSDT 差額 = 剩餘換入幣市值 − 剩餘原幣若持有至今的市值";
    public string ProblemText => Setting.InvalidReason ?? "";
    public string ToggleText => Setting.Enabled ? "停用追蹤" : "啟用追蹤";
    public int Direction => result.QuantityDifference is null ? 0 : Math.Sign(decimal.Round(result.QuantityDifference.Value,10));
    public void Refresh(IReadOnlyDictionary<string,TickerPrice> quotes,ConnectionStatus status,DateTimeOffset now)
    {
        fromQuote=quotes.GetValueOrDefault(Setting.FromSymbol);toQuote=quotes.GetValueOrDefault(Setting.ToSymbol);
        result=SwapComparisonCalculator.Value(Setting,fromQuote,toQuote,status,now);
        foreach(var name in new[]{nameof(Result),nameof(ReturnText),nameof(DifferenceText),nameof(StateText),nameof(ValuesText),nameof(QuotesText),nameof(Direction),
            nameof(QuantityDifferenceText),nameof(UsdtDifferenceText),nameof(ReturnPercentText),nameof(FromValueText),nameof(ToValueText),
            nameof(FromQuoteText),nameof(ToQuoteText),nameof(FromQuoteTimeText),nameof(ToQuoteTimeText),nameof(ExactResultsText)})Notify(name);
    }
    private static string QuoteText(TickerPrice? quote,string asset) => quote is null ? $"{asset}：等待報價" :
        $"{asset}：{Exact(quote.Price)} USDT · {quote.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
    public static string Exact(decimal value) => value.ToString("0.############################",CultureInfo.InvariantCulture);
    private static string Full(decimal? value) => value is null ? "—" : Exact(value.Value);
    private static string QuoteTime(TickerPrice? quote) => quote is null ? "等待報價" : quote.UpdatedAt.ToLocalTime().ToString("MM/dd HH:mm:ss");
    private static string Quantity(decimal? value) => value?.ToString("#,##0.00##",CultureInfo.InvariantCulture) ?? "—";
    public static string Money(decimal? value) => value?.ToString("N2",CultureInfo.InvariantCulture) ?? "—";
    private static string SignedMoney(decimal? value)
    {
        if(value is null)return "—";
        var rounded=decimal.Round(value.Value,2);
        return (rounded>0 ? "+" : "")+Money(rounded);
    }
    public static string Signed(decimal? value)
    {
        if(value is null)return "—";
        var rounded=decimal.Round(value.Value,4);
        return (rounded>0 ? "+" : "")+Quantity(rounded);
    }
    public static string Percent(decimal? value)
    {
        if(value is null)return "—";
        return decimal.Round(value.Value,2).ToString("+0.00;-0.00;0.00",CultureInfo.InvariantCulture)+"%";
    }
}
