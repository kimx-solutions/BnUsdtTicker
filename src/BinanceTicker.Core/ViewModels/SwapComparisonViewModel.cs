using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Core.ViewModels;

public sealed partial class SwapComparisonViewModel : ObservableObject, IDisposable
{
    private readonly IBinanceService binance;
    private readonly Func<IReadOnlyList<SwapComparisonSetting>,Task<bool>> save;
    private readonly TimeProvider time;
    private readonly CancellationTokenSource lifetime=new();
    private readonly Dictionary<string,TickerPrice> quotes=new(StringComparer.Ordinal);
    private ConnectionStatus status=ConnectionStatus.Connecting;
    private bool busy,editing,disposed;
    private string? editingId;
    private SwapComparisonRowViewModel? selected;
    private string fromSymbol="",fromQuantity="",toSymbol="",toQuantity="",swappedAt="",note="",error="";
    private bool enabled=true;
    public ObservableCollection<SwapComparisonRowViewModel> Records { get; }=[];
    public SwapComparisonRowViewModel? Selected
    {
        get=>selected;
        set { if(Set(ref selected,value))RefreshCommands(); }
    }
    public string FromSymbolText { get=>fromSymbol;set=>Set(ref fromSymbol,value); }
    public string FromQuantityText { get=>fromQuantity;set=>Set(ref fromQuantity,value); }
    public string ToSymbolText { get=>toSymbol;set=>Set(ref toSymbol,value); }
    public string ToQuantityText { get=>toQuantity;set=>Set(ref toQuantity,value); }
    public string SwappedAtText { get=>swappedAt;set=>Set(ref swappedAt,value); }
    public string NoteText { get=>note;set=>Set(ref note,value); }
    public bool Enabled { get=>enabled;set=>Set(ref enabled,value); }
    public string Error { get=>error;set=>Set(ref error,value); }
    public bool IsBusy => busy;
    public bool IsEditing => editing;
    public bool EditorEnabled => editing && !busy;
    public bool IsEmpty => VisibleRecords.Count==0;
    public bool HasInvalid => Records.Any(r=>r.Setting.InvalidReason is not null);
    public string EditorTitle => editingId is null ? "新增換幣紀錄" : "編輯換幣紀錄";
    public RelayCommand NewCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand DeleteCommand { get; }
    public AsyncCommand ToggleCommand { get; }
    public AsyncCommand RemoveInvalidCommand { get; }
    public SwapComparisonViewModel(AppSettings settings,IBinanceService binance,
        Func<IReadOnlyList<SwapComparisonSetting>,Task<bool>> save,TimeProvider? time=null)
    {
        this.binance=binance;this.save=save;this.time=time ?? TimeProvider.System;
        NewCommand=new(New,()=>!busy && !editing && !IsSettling);
        EditCommand=new(Edit,()=>!busy && !editing && !IsSettling && Selected is not null && Selected.Setting.Settlements.Count==0);
        CancelCommand=new(Cancel,()=>!busy && editing);
        SaveCommand=new(async()=>await SaveAsync(),()=>!busy && editing);
        DeleteCommand=new(async()=>await DeleteAsync(),()=>!busy && !editing && !IsSettling && Selected is not null);
        ToggleCommand=new(async()=>await ToggleAsync(),()=>!busy && !editing && !IsSettling && Selected?.Setting.InvalidReason is null && Selected is not null && !Selected.Setting.IsClosed);
        RemoveInvalidCommand=new(async()=>await RemoveInvalidAsync(),()=>!busy && !editing && !IsSettling && HasInvalid);
        SettleCommand=new(BeginSettlement,()=>!busy && !editing && !IsSettling && Selected is not null && Selected.Setting.InvalidReason is null && !Selected.Setting.IsClosed);
        Configure(settings);
    }
    public void Configure(AppSettings settings)
    {
        var selectedId=Selected?.Setting.Id;
        Records.Clear();
        foreach(var setting in settings.SwapComparisons.OrderByDescending(s=>s.SwappedAt).ThenBy(s=>s.Id,StringComparer.Ordinal))
            Records.Add(new(setting));
        ActiveRecords.Clear();HistoryRecords.Clear();
        foreach(var row in Records.Where(r=>!r.Setting.IsClosed))ActiveRecords.Add(row);
        foreach(var row in Records.Where(r=>r.Setting.IsClosed).OrderByDescending(r=>r.Setting.Settlements.Last().SettledAt))HistoryRecords.Add(row);
        Selected=VisibleRecords.FirstOrDefault(r=>r.Setting.Id==selectedId) ?? VisibleRecords.FirstOrDefault();
        Notify(nameof(ActiveHeader));Notify(nameof(HistoryHeader));
        var demanded=Records.Where(r=>r.Setting.Enabled && r.Setting.InvalidReason is null && !r.Setting.IsClosed)
            .SelectMany(r=>new[]{r.Setting.FromSymbol,r.Setting.ToSymbol}).ToHashSet(StringComparer.Ordinal);
        foreach(var symbol in quotes.Keys.Where(s=>!demanded.Contains(s)).ToArray())quotes.Remove(symbol);
        Notify(nameof(IsEmpty));Notify(nameof(HasInvalid));RefreshCommands();Refresh();
    }
    public bool Update(TickerPrice quote)
    {
        var now=time.GetUtcNow();
        if(disposed || quote.Price<=0 || new DateTimeOffset(quote.UpdatedAt.ToUniversalTime())>now ||
            !Records.Any(r=>r.Setting.Enabled && r.Setting.InvalidReason is null && !r.Setting.IsClosed &&
                (r.Setting.FromSymbol==quote.Symbol || r.Setting.ToSymbol==quote.Symbol)) ||
            quotes.TryGetValue(quote.Symbol,out var previous) && quote.UpdatedAt<previous.UpdatedAt)return false;
        quotes[quote.Symbol]=quote;Refresh();return true;
    }
    public void SetStatus(ConnectionStatus value) { status=value;Refresh(); }
    public void Refresh()
    {
        if(disposed)return;
        var now=time.GetUtcNow();
        foreach(var row in Records)row.Refresh(quotes,status,now);
        RefreshSettlementQuotes();
    }
    public void New()
    {
        if(busy || editing || IsSettling || disposed)return;
        editingId=null;FromSymbolText="";ToSymbolText="";FromQuantityText="";ToQuantityText="";
        SwappedAtText=time.GetUtcNow().ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        NoteText="";Enabled=true;Error="";SetEditing(true);
    }
    public void Edit()
    {
        if(busy || editing || IsSettling || disposed || Selected is null || Selected.Setting.Settlements.Count>0)return;
        var record=Selected.Setting;editingId=record.Id;
        FromSymbolText=record.FromSymbol;ToSymbolText=record.ToSymbol;
        FromQuantityText=SwapComparisonRowViewModel.Exact(record.FromQuantity);
        ToQuantityText=SwapComparisonRowViewModel.Exact(record.ToQuantity);
        SwappedAtText=record.SwappedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        NoteText=record.Note;Enabled=record.InvalidReason is null ? record.Enabled : true;
        Error="";SetEditing(true);
    }
    public void Cancel() { if(busy || disposed)return;Error="";editingId=null;SetEditing(false); }
    private SwapComparisonSetting CreateRecord()
    {
        decimal Quantity(string input)
        {
            if(!decimal.TryParse(input.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var value) || value<=0)
                throw new ArgumentException("數量必須大於零，小數點使用 .，不使用千分位逗號。");
            return value;
        }
        if(!DateTime.TryParseExact(SwappedAtText.Trim(),"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out var local) ||
            TimeZoneInfo.Local.IsInvalidTime(local) || TimeZoneInfo.Local.IsAmbiguousTime(local))
            throw new ArgumentException("換幣時間請使用 yyyy-MM-dd HH:mm:ss，並填寫有效的本機時間。");
        var record=new SwapComparisonSetting
        {
            Id=editingId ?? Guid.NewGuid().ToString("N"),FromSymbol=FromSymbolText,FromQuantity=Quantity(FromQuantityText),
            ToSymbol=ToSymbolText,ToQuantity=Quantity(ToQuantityText),SwappedAt=new DateTimeOffset(local),
            Note=NoteText.Trim(),Enabled=Enabled
        };
        record.Validate(time.GetUtcNow());return record;
    }
    public async Task<bool> SaveAsync()
    {
        if(busy || !editing || disposed)return false;
        Error="";SetBusy(true);
        try
        {
            var record=CreateRecord();
            var previous=Records.FirstOrDefault(r=>r.Setting.Id==editingId)?.Setting;
            if(previous?.FromSymbol!=record.FromSymbol || previous?.ToSymbol!=record.ToSymbol || previous.InvalidReason is not null)
            {
                foreach(var symbol in new[]{record.FromSymbol,record.ToSymbol})
                    if(!await binance.IsValidSymbolAsync(symbol,lifetime.Token))
                    { Error=$"Binance 沒有可交易的 {symbol} 現貨交易對。";return false; }
            }
            if(disposed)return false;
            var updated=Records.Select(r=>r.Setting.Copy()).ToList();
            var index=updated.FindIndex(r=>r.Id==record.Id);
            if(index>=0)updated[index]=record;else updated.Add(record);
            if(!await CommitAsync(updated))return false;
            ViewIndex=0;Selected=Records.First(r=>r.Setting.Id==record.Id);editingId=null;SetEditing(false);return true;
        }
        catch(OperationCanceledException) when(disposed) { return false; }
        catch(Exception ex) when(IsExpected(ex))
        { Error="無法驗證或儲存，請確認輸入／網路後重試："+ex.Message;return false; }
        finally { SetBusy(false); }
    }
    public Task<bool> DeleteAsync() => ChangeAsync(records=>records.RemoveAll(r=>r.Id==Selected!.Setting.Id));
    public Task<bool> ToggleAsync() => ChangeAsync(records=>
    {
        var row=records.First(r=>r.Id==Selected!.Setting.Id);
        if(row.InvalidReason is not null)throw new ArgumentException("請先修正損壞的紀錄。");
        row.Enabled=!row.Enabled;
    });
    public Task<bool> RemoveInvalidAsync() => ChangeAsync(records=>records.RemoveAll(r=>r.InvalidReason is not null),requiresSelection:false);
    private async Task<bool> ChangeAsync(Action<List<SwapComparisonSetting>> change,bool requiresSelection=true)
    {
        if(busy || editing || IsSettling || disposed || requiresSelection && Selected is null)return false;
        Error="";SetBusy(true);
        try
        {
            var updated=Records.Select(r=>r.Setting.Copy()).ToList();change(updated);
            return await CommitAsync(updated);
        }
        catch(Exception ex) when(IsExpected(ex)) { Error="儲存失敗："+ex.Message;return false; }
        finally { SetBusy(false); }
    }
    private async Task<bool> CommitAsync(List<SwapComparisonSetting> updated)
    {
        if(!await save(updated)) { if(Error.Length==0)Error="儲存失敗，原紀錄仍保留。";return false; }
        if(disposed)return false;
        Configure(new(){SwapComparisons=updated});return true;
    }
    private static bool IsExpected(Exception ex) => ex is ArgumentException or IOException or UnauthorizedAccessException or
        HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or AggregateException or System.Security.SecurityException;
    private void SetBusy(bool value) { busy=value;Notify(nameof(IsBusy));Notify(nameof(EditorEnabled));SettlementEditor?.SetBusy(value);RefreshCommands(); }
    private void SetEditing(bool value) { editing=value;Notify(nameof(IsEditing));Notify(nameof(EditorEnabled));Notify(nameof(EditorTitle));RefreshCommands(); }
    private void RefreshCommands()
    {
        NewCommand.Refresh();EditCommand.Refresh();CancelCommand.Refresh();SaveCommand.Refresh();
        DeleteCommand.Refresh();ToggleCommand.Refresh();RemoveInvalidCommand.Refresh();
        SettleCommand.Refresh();
    }
    public void Dispose() { if(disposed)return;disposed=true;lifetime.Cancel();lifetime.Dispose(); }
}
