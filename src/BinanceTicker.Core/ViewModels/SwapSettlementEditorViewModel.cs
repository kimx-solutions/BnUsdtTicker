using System.Globalization;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public sealed class SwapSettlementEditorViewModel : ObservableObject
{
    private readonly SwapComparisonSetting setting;
    private bool settleAll=true,useMarketQuote,busy;
    private string quantity="",actualReturn="",fromPrice="",note="",error="";
    private TickerPrice? fromQuote,toQuote;
    private bool freshQuotes;
    public SwapSettlementEditorViewModel(SwapComparisonSetting setting,Func<Task<bool>> save,Action cancel)
    {
        this.setting=setting.Copy();quantity=SwapComparisonRowViewModel.Exact(setting.RemainingToQuantity);
        SaveCommand=new(async()=>await save(),()=>!busy);
        CancelCommand=new(cancel,()=>!busy);
    }
    public string Title => $"結算 · {SwapComparisonCalculator.Asset(setting.ToSymbol)} → {SwapComparisonCalculator.Asset(setting.FromSymbol)}";
    public string RemainingText => $"剩餘 {SwapComparisonRowViewModel.Exact(setting.RemainingToQuantity)} {SwapComparisonCalculator.Asset(setting.ToSymbol)} · 原幣基準 {SwapComparisonRowViewModel.Exact(setting.RemainingFromQuantity)} {SwapComparisonCalculator.Asset(setting.FromSymbol)}";
    public string QuantityLabel => $"本次結算數量（{SwapComparisonCalculator.Asset(setting.ToSymbol)}）";
    public string ReturnLabel => $"實際換回數量（{SwapComparisonCalculator.Asset(setting.FromSymbol)}，以到帳數量為準）";
    public string PriceLabel => $"結算時 {SwapComparisonCalculator.Asset(setting.FromSymbol)} 的 USDT 參考價（選填）";
    public bool SettleAll
    {
        get=>settleAll;
        set { if(Set(ref settleAll,value)) { if(value)QuantityText=SwapComparisonRowViewModel.Exact(setting.RemainingToQuantity);Notify(nameof(CanEditQuantity));Notify(nameof(PreviewText)); } }
    }
    public bool UseMarketQuote
    {
        get=>useMarketQuote;
        set { if(Set(ref useMarketQuote,value)) { Notify(nameof(IsActual));Notify(nameof(PreviewText)); } }
    }
    public bool IsActual { get=>!UseMarketQuote;set { if(value)UseMarketQuote=false; } }
    public bool CanEditQuantity => !SettleAll;
    public bool EditorEnabled => !busy;
    public string QuantityText { get=>quantity;set { if(Set(ref quantity,value))Notify(nameof(PreviewText)); } }
    public string ActualReturnText { get=>actualReturn;set { if(Set(ref actualReturn,value))Notify(nameof(PreviewText)); } }
    public string FromPriceText { get=>fromPrice;set=>Set(ref fromPrice,value); }
    public string NoteText { get=>note;set=>Set(ref note,value); }
    public string Error { get=>error;set=>Set(ref error,value); }
    public AsyncCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public string PreviewText
    {
        get
        {
            if(UseMarketQuote && !freshQuotes)return "需要兩邊的即時報價。斷線或報價超過 60 秒時無法報價結算。";
            try
            {
                var q=ParseQuantity(SettleAll ? SwapComparisonRowViewModel.Exact(setting.RemainingToQuantity) : QuantityText);
                if(q>setting.RemainingToQuantity)return "結算數量不可超過剩餘數量。";
                var returned=UseMarketQuote ? q*toQuote!.Price/fromQuote!.Price : ParseQuantity(ActualReturnText);
                var baseline=Allocate(q);
                return $"{(UseMarketQuote ? "依當下報價可換回" : "實際換回")} {SwapComparisonRowViewModel.Exact(returned)} {SwapComparisonCalculator.Asset(setting.FromSymbol)}\n原幣差額 {SwapComparisonRowViewModel.Signed(returned-baseline)} · 相對報酬率 {SwapComparisonRowViewModel.Percent((returned/baseline-1)*100)}\n結算後剩餘 {SwapComparisonRowViewModel.Exact(setting.RemainingToQuantity-q)} {SwapComparisonCalculator.Asset(setting.ToSymbol)}";
            }
            catch(Exception ex) when(ex is ArgumentException or OverflowException or DivideByZeroException) { return "請填寫有效數量，確認後會固定本次結算結果。"; }
        }
    }
    internal void RefreshQuotes(TickerPrice? from,TickerPrice? to,bool fresh)
    { fromQuote=from;toQuote=to;freshQuotes=fresh;Notify(nameof(PreviewText)); }
    internal void SetBusy(bool value)
    { busy=value;Notify(nameof(EditorEnabled));SaveCommand.Refresh();CancelCommand.Refresh(); }
    internal SwapSettlement Create(DateTimeOffset now)
    {
        var q=SettleAll ? setting.RemainingToQuantity : ParseQuantity(QuantityText);
        if(q>setting.RemainingToQuantity)throw new ArgumentException("結算數量不可超過剩餘數量。");
        if(UseMarketQuote && !freshQuotes)throw new ArgumentException("當下報價結算需要兩邊有效且未過期的即時報價。");
        var reference=UseMarketQuote ? fromQuote!.Price : string.IsNullOrWhiteSpace(FromPriceText) ? (decimal?)null : ParseQuantity(FromPriceText);
        var returned=UseMarketQuote ? q*toQuote!.Price/fromQuote!.Price : ParseQuantity(ActualReturnText);
        return new()
        {
            Mode=UseMarketQuote ? SwapSettlementMode.MarketQuote : SwapSettlementMode.Actual,
            ToQuantity=q,FromQuantity=Allocate(q),ReturnedQuantity=returned,SettledAt=now,
            FromPrice=reference,ToPrice=UseMarketQuote ? toQuote!.Price : null,
            FromQuoteAt=UseMarketQuote ? new DateTimeOffset(fromQuote!.UpdatedAt.ToUniversalTime()) : null,
            ToQuoteAt=UseMarketQuote ? new DateTimeOffset(toQuote!.UpdatedAt.ToUniversalTime()) : null,Note=NoteText.Trim()
        };
    }
    private decimal Allocate(decimal q) => q==setting.RemainingToQuantity ? setting.RemainingFromQuantity : setting.RemainingFromQuantity*(q/setting.RemainingToQuantity);
    private static decimal ParseQuantity(string text)
    {
        if(!decimal.TryParse(text.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value) || value<=0)
            throw new ArgumentException("數量與參考價必須大於零，小數點使用 .，不使用千分位逗號。");
        return value;
    }
}
