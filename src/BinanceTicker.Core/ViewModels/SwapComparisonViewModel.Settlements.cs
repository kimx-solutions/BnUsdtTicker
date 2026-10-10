using System.Collections.ObjectModel;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public sealed partial class SwapComparisonViewModel
{
    private int viewIndex;
    private string? settlingId;
    public ObservableCollection<SwapComparisonRowViewModel> ActiveRecords { get; }=[];
    public ObservableCollection<SwapComparisonRowViewModel> HistoryRecords { get; }=[];
    public ObservableCollection<SwapComparisonRowViewModel> VisibleRecords => ViewIndex==0 ? ActiveRecords : HistoryRecords;
    public int ViewIndex
    {
        get=>viewIndex;
        set
        {
            if(value is not (0 or 1) || !Set(ref viewIndex,value))return;
            Notify(nameof(VisibleRecords));Notify(nameof(IsEmpty));Notify(nameof(EmptyText));Notify(nameof(ResultHeading));
            Selected=VisibleRecords.FirstOrDefault();
        }
    }
    public string ActiveHeader => $"追蹤中（{ActiveRecords.Count}）";
    public string HistoryHeader => $"歷史（{HistoryRecords.Count}）";
    public string EmptyText => ViewIndex==0 ? "尚無待結算紀錄。按「新增」記下你的一次換幣。" : "尚無全部結算的紀錄。部分結算明細可在追蹤中的紀錄查看。";
    public string ResultHeading => ViewIndex==0 ? "剩餘部位的即時比較" : "全部結算的固定結果";
    public SwapSettlementEditorViewModel? SettlementEditor { get; private set; }
    public bool IsSettling => SettlementEditor is not null;
    public RelayCommand SettleCommand { get; private set; }=null!;

    public void BeginSettlement()
    {
        if(busy || editing || IsSettling || disposed || Selected is null || Selected.Setting.InvalidReason is not null || Selected.Setting.IsClosed)return;
        settlingId=Selected.Setting.Id;Error="";
        SettlementEditor=new(Selected.Setting,SaveSettlementAsync,CancelSettlement);
        RefreshSettlementQuotes();
        if(SettlementEditorFreshFromPrice() is { } price)SettlementEditor.FromPriceText=SwapComparisonRowViewModel.Exact(price);
        Notify(nameof(IsSettling));RefreshCommands();
    }
    public void CancelSettlement()
    {
        if(busy)return;
        SettlementEditor=null;settlingId=null;Error="";Notify(nameof(IsSettling));RefreshCommands();
    }
    private decimal? SettlementEditorFreshFromPrice()
    {
        var record=Records.FirstOrDefault(r=>r.Setting.Id==settlingId);
        var quote=record is null ? null : quotes.GetValueOrDefault(record.Setting.FromSymbol);
        return status==ConnectionStatus.Connected && quote is not null && time.GetUtcNow()-new DateTimeOffset(quote.UpdatedAt.ToUniversalTime())<=TimeSpan.FromSeconds(60) ? quote.Price : null;
    }
    private void RefreshSettlementQuotes()
    {
        if(SettlementEditor is null)return;
        var record=Records.FirstOrDefault(r=>r.Setting.Id==settlingId)?.Setting;
        if(record is null) { SettlementEditor.RefreshQuotes(null,null,false);return; }
        var from=quotes.GetValueOrDefault(record.FromSymbol);var to=quotes.GetValueOrDefault(record.ToSymbol);
        var result=SwapComparisonCalculator.Value(record,from,to,status,time.GetUtcNow());
        SettlementEditor.RefreshQuotes(from,to,result.ReturnQuantity is not null && !result.IsStale);
    }
    public async Task<bool> SaveSettlementAsync()
    {
        if(busy || !IsSettling || disposed)return false;
        var draft=SettlementEditor!;draft.Error="";Error="";SetBusy(true);
        try
        {
            RefreshSettlementQuotes();var now=time.GetUtcNow();
            var updated=Records.Select(r=>r.Setting.Copy()).ToList();
            var record=updated.Single(r=>r.Id==settlingId);
            if(record.InvalidReason is not null || record.IsClosed)throw new ArgumentException("此紀錄無法再次結算。");
            record.Settlements.Add(draft.Create(now));record.Validate(now);
            if(record.IsClosed)record.Enabled=false;
            if(!await CommitAsync(updated)) { draft.Error=Error;return false; }
            ViewIndex=record.IsClosed ? 1 : 0;Selected=Records.First(r=>r.Setting.Id==record.Id);
            SettlementEditor=null;settlingId=null;Notify(nameof(IsSettling));return true;
        }
        catch(Exception ex) when(IsExpected(ex) || ex is OverflowException or DivideByZeroException)
        { draft.Error="無法結算："+ex.Message;return false; }
        finally { SetBusy(false);draft.SetBusy(false); }
    }
}
