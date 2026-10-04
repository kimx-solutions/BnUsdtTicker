using System.Collections.ObjectModel;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
namespace BinanceTicker.Core.ViewModels;

public sealed partial class TickerViewModel
{
    private AppSettings? watchlistSettings;
    private readonly Dictionary<string,TickerRowViewModel> rows=new(StringComparer.Ordinal);
    private readonly Dictionary<string,TickerPrice> quotes=new(StringComparer.Ordinal);
    private string watchlistError="";
    private bool showHoldings;
    public bool ShowHoldings { get=>showHoldings;set=>Set(ref showHoldings,value); }
    public string WatchlistError { get=>watchlistError;set=>Set(ref watchlistError,value); }
    public ObservableCollection<WatchlistGroup> Watchlists { get; private set; }=[];
    public string? ActiveWatchlistId => watchlistSettings?.ActiveWatchlistId;
    public WatchlistGroup? SelectedWatchlist
    {
        get=>Watchlists.FirstOrDefault(g=>g.Id==ActiveWatchlistId);
        set { if(value is not null)SelectWatchlist(value.Id); }
    }
    public PortfolioSummary CurrentPortfolio { get; private set; }=new(0,0,0,null,0,0,0,0,false);
    public PortfolioSummary TotalPortfolio { get; private set; }=new(0,0,0,null,0,0,0,0,false);
    public event Action? WatchlistPreferencesChanged;
    public void SelectWatchlist(string id)
    {
        if(watchlistSettings is null || id==ActiveWatchlistId || !Watchlists.Any(g=>g.Id==id))return;
        CaptureActivePreferences();watchlistSettings.ActiveWatchlistId=id;ShowActiveWatchlist();
        WatchlistPreferencesChanged?.Invoke();
    }
    private void PreferencesChanged()
    {
        CaptureActivePreferences();WatchlistPreferencesChanged?.Invoke();
    }
    private void CaptureActivePreferences()
    {
        if(SelectedWatchlist is not { } group)return;
        group.Ui.ShowSparkline=ShowSparkline;group.Ui.SparklineRange=SparklineRange;
        group.Ui.ShowChangePercent=ShowChangePercent;group.Ui.CompactMode=CompactMode;
        group.SortColumn=sortColumn;group.SortDescending=sortDescending;
    }
    private void ShowActiveWatchlist()
    {
        if(SelectedWatchlist is not { } group)return;
        Prices.Clear();
        foreach(var member in group.Members.Where(m=>m.Enabled).OrderBy(m=>m.Order))Prices.Add(rows[member.Symbol]);
        ShowSparkline=group.Ui.ShowSparkline;SparklineRange=group.Ui.SparklineRange;
        ShowChangePercent=group.Ui.ShowChangePercent;CompactMode=group.Ui.CompactMode;
        sortColumn=group.SortColumn;sortDescending=group.SortDescending;ApplySort();
        Notify(nameof(ActiveWatchlistId));Notify(nameof(SelectedWatchlist));Notify(nameof(SparklineToggleText));
        Notify(nameof(SortColumn));Notify(nameof(SymbolSortHeader));Notify(nameof(PriceSortHeader));Notify(nameof(ChangeSortHeader));Notify(nameof(SortDescription));
        Notify(nameof(IsEmpty));Notify(nameof(StatusText));RefreshPortfolio(DateTimeOffset.UtcNow);
    }
    public void WriteWatchlistPreferences(AppSettings settings)
    {
        CaptureActivePreferences();settings.ActiveWatchlistId=ActiveWatchlistId;
        foreach(var group in settings.Watchlists!)
        {
            var view=Watchlists.FirstOrDefault(g=>g.Id==group.Id);if(view is null)continue;
            group.Ui=view.Ui.Copy();group.SortColumn=view.SortColumn;group.SortDescending=view.SortDescending;
        }
        settings.Ui.ShowSparkline=ShowSparkline;settings.Ui.SparklineRange=SparklineRange;
        settings.Ui.ShowChangePercent=ShowChangePercent;settings.Ui.CompactMode=CompactMode;
    }
    public void RefreshPortfolio(DateTimeOffset now)
    {
        if(watchlistSettings is null)return;
        foreach(var row in rows.Values)row.SetHolding(watchlistSettings.Holdings.FirstOrDefault(h=>h.Symbol==row.Symbol),Status,now);
        var members=SelectedWatchlist!.Members.Select(m=>m.Symbol).ToHashSet(StringComparer.Ordinal);
        CurrentPortfolio=PortfolioCalculator.Summarize(watchlistSettings.Holdings.Where(h=>members.Contains(h.Symbol)),quotes,Status,now);
        TotalPortfolio=PortfolioCalculator.Summarize(watchlistSettings.Holdings,quotes,Status,now);
        Notify(nameof(CurrentPortfolio));Notify(nameof(TotalPortfolio));
    }
}

public sealed partial class TickerRowViewModel
{
    private string holdingText="尚未設定持倉";
    private string holdingToolTip="手動持倉 · 在設定中輸入數量與平均成本";
    public string HoldingText { get=>holdingText;private set=>Set(ref holdingText,value); }
    public string HoldingToolTip { get=>holdingToolTip;private set=>Set(ref holdingToolTip,value); }
    public void SetHolding(HoldingSetting? holding,ConnectionStatus status,DateTimeOffset now)
    {
        if(holding is null) { HoldingText="尚未設定持倉";HoldingToolTip="手動持倉 · 在設定中輸入數量與平均成本";return; }
        var value=PortfolioCalculator.Value(holding,price,status,now);
        var state=holding.Quantity==0 ? "無持倉" : value.Overflowed ? "超出可計算範圍" :
            value.MissingQuote ? "等待報價" : value.IsStale ? "使用最後報價" : holding.AverageCost==0 ? "成本為零" : "";
        HoldingText=$"持有 {PortfolioSummary.Money(holding.Quantity)} · 市值 {PortfolioSummary.Money(value.MarketValue)} USDT\n損益 {PortfolioSummary.Money(value.Profit)} USDT · {PortfolioSummary.Percent(value.ProfitPercent)}"+(state.Length==0 ? "" : " · "+state);
        HoldingToolTip=$"平均成本 {PortfolioSummary.Money(holding.AverageCost)} USDT／單位\n{HoldingText}\n{UpdatedText}";
    }
}
