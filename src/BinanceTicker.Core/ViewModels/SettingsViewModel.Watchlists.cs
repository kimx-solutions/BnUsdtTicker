using System.Collections.ObjectModel;
using BinanceTicker.Core.Models;
namespace BinanceTicker.Core.ViewModels;

public sealed partial class SettingsViewModel
{
    private bool legacyEditor;
    private WatchlistGroup? selectedWatchlist;
    private string watchlistName="";
    public ObservableCollection<WatchlistGroup> Watchlists { get; private set; }=[];
    public ObservableCollection<HoldingEditorViewModel> Holdings { get; }=[];
    public string WatchlistName { get=>watchlistName;set=>Set(ref watchlistName,value); }
    public const string DeleteWatchlistExplanation="只刪除此分組及其成員檢視；共用持倉、其他分組與價格提醒會保留。按儲存後生效。";
    public RelayCommand AddWatchlistCommand { get; private set; }=null!;
    public RelayCommand RenameWatchlistCommand { get; private set; }=null!;
    public RelayCommand RemoveWatchlistCommand { get; private set; }=null!;
    public WatchlistGroup? SelectedWatchlist
    {
        get=>selectedWatchlist;
        set
        {
            if(value is null || !Watchlists.Contains(value) || ReferenceEquals(value,selectedWatchlist))return;
            FlushWatchlist();Set(ref selectedWatchlist,value);
            Symbols=new(value.Members.OrderBy(m=>m.Order).Select(m=>new SymbolSetting { Symbol=m.Symbol,Enabled=m.Enabled,Order=m.Order,
                Alert=original.Symbols.First(s=>s.Symbol==m.Symbol).Alert.Copy() }));Notify(nameof(Symbols));
            WatchlistName=value.Name;showSparkline=value.Ui.ShowSparkline;sparklineRange=value.Ui.SparklineRange;
            ShowChangePercent=value.Ui.ShowChangePercent;CompactMode=value.Ui.CompactMode;
            Notify(nameof(ShowSparkline));Notify(nameof(SparklineRange));Notify(nameof(ShowChangePercent));Notify(nameof(CompactMode));
            SelectedSymbol=Symbols.FirstOrDefault();RemoveWatchlistCommand.Refresh();
        }
    }
    private void InitializeWatchlists()
    {
        Watchlists=new(original.Watchlists!.Select(g=>g.Copy()));
        foreach(var symbol in original.Symbols)EnsureHoldingEditor(symbol.Symbol);
        AddWatchlistCommand=new(()=>ChangeWatchlistName(add:true));
        RenameWatchlistCommand=new(()=>ChangeWatchlistName(add:false));
        RemoveWatchlistCommand=new(()=>
        {
            if(SelectedWatchlist is not { } group || Watchlists.Count<=1)return;
            FlushWatchlist();Watchlists.Remove(group);selectedWatchlist=null;SelectedWatchlist=Watchlists[0];RemoveWatchlistCommand.Refresh();
        },()=>Watchlists.Count>1 && SelectedWatchlist is not null);
        SelectedWatchlist=Watchlists.First(g=>g.Id==original.ActiveWatchlistId);
    }
    private void EnsureHoldingEditor(string symbol)
    {
        var entry=original.Symbols.FirstOrDefault(s=>s.Symbol==symbol);
        if(entry is null) { entry=new() { Symbol=symbol,Order=original.Symbols.Count+1 };original.Symbols.Add(entry); }
        if(Holdings.All(h=>h.Symbol!=symbol))Holdings.Add(new(symbol,original.Holdings.FirstOrDefault(h=>h.Symbol==symbol)) { AlertEnabled=entry.Enabled });
    }
    private void FlushWatchlist()
    {
        if(selectedWatchlist is not { } group)return;
        group.Members=Symbols.Select((s,i)=>new WatchlistMember { Symbol=s.Symbol,Enabled=s.Enabled,Order=i+1 }).ToList();
        group.Ui.ShowSparkline=ShowSparkline;group.Ui.SparklineRange=SparklineRange;
        group.Ui.ShowChangePercent=ShowChangePercent;group.Ui.CompactMode=CompactMode;
    }
    private void ChangeWatchlistName(bool add)
    {
        var name=WatchlistName.Trim();Error="";
        if(name.Length==0 || Watchlists.Any(g=>(add || g!=SelectedWatchlist) && string.Equals(g.Name,name,StringComparison.OrdinalIgnoreCase)))
        { Error="分組名稱不可空白或重複。";return; }
        if(add) { var group=new WatchlistGroup { Name=name };Watchlists.Add(group);SelectedWatchlist=group; }
        else if(SelectedWatchlist is { } current)current.Name=name;
        RemoveWatchlistCommand.Refresh();
    }
    public void PreserveUneditedWatchlistPreferences(AppSettings edited,AppSettings current)
    {
        if(edited.Watchlists is null || current.Watchlists is null)return;
        if(edited.ActiveWatchlistId==original.ActiveWatchlistId && edited.Watchlists.Any(g=>g.Id==current.ActiveWatchlistId))
            edited.ActiveWatchlistId=current.ActiveWatchlistId;
        foreach(var group in edited.Watchlists)
        {
            var before=original.Watchlists!.FirstOrDefault(g=>g.Id==group.Id);
            var live=current.Watchlists.FirstOrDefault(g=>g.Id==group.Id);
            if(before is null || live is null)continue;
            if(group.Ui.ShowSparkline==before.Ui.ShowSparkline)group.Ui.ShowSparkline=live.Ui.ShowSparkline;
            if(group.Ui.SparklineRange==before.Ui.SparklineRange)group.Ui.SparklineRange=live.Ui.SparklineRange;
            if(group.Ui.ShowChangePercent==before.Ui.ShowChangePercent)group.Ui.ShowChangePercent=live.Ui.ShowChangePercent;
            if(group.Ui.CompactMode==before.Ui.CompactMode)group.Ui.CompactMode=live.Ui.CompactMode;
            group.SortColumn=live.SortColumn;group.SortDescending=live.SortDescending;
        }
    }
}
