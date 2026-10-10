using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.ViewModels;

public sealed class SwapSettlementRowViewModel(SwapSettlement settlement,string fromAsset,string toAsset)
{
    public string TimeText => settlement.SettledAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string ModeText => settlement.Mode==SwapSettlementMode.Actual ? "實際成交" : "報價結算";
    public string AmountText => $"{SwapComparisonRowViewModel.Exact(settlement.ToQuantity)} {toAsset} → {SwapComparisonRowViewModel.Exact(settlement.ReturnedQuantity)} {fromAsset}";
    private decimal Difference => settlement.ReturnedQuantity-settlement.FromQuantity;
    public int Direction => Math.Sign(Difference);
    public string ResultText => $"{SwapComparisonRowViewModel.Signed(Difference)} {fromAsset} · {SwapComparisonRowViewModel.Percent((settlement.ReturnedQuantity/settlement.FromQuantity-1)*100)}";
    public string ReferenceText => settlement.FromPrice is { } price ?
        $"原幣基準 {SwapComparisonRowViewModel.Exact(settlement.FromQuantity)} {fromAsset} · {fromAsset} 參考價 {SwapComparisonRowViewModel.Exact(price)} USDT\nUSDT 差額 {SwapComparisonRowViewModel.Signed(Difference*price)}" :
        $"原幣基準 {SwapComparisonRowViewModel.Exact(settlement.FromQuantity)} {fromAsset} · 未填 USDT 參考價";
    public string NoteText => settlement.Note;
    public string SnapshotText => settlement.Mode==SwapSettlementMode.MarketQuote ?
        $"{fromAsset}：{settlement.FromPrice} USDT（{settlement.FromQuoteAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}）\n{toAsset}：{settlement.ToPrice} USDT（{settlement.ToQuoteAt?.ToLocalTime():yyyy-MM-dd HH:mm:ss}）" : ReferenceText;
}
