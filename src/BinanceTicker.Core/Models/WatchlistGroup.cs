using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Core.Models;

public sealed class WatchlistGroup : ObservableObject
{
    private string name = "";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get => name; set => Set(ref name, value); }
    public List<WatchlistMember> Members { get; set; } = [];
    public UiSettings Ui { get; set; } = new();
    public TickerSortColumn? SortColumn { get; set; }
    public bool SortDescending { get; set; }
    public WatchlistGroup Copy() => new() { Id=Id, Name=Name, Members=Members.Select(m=>m.Copy()).ToList(),
        Ui=Ui.Copy(), SortColumn=SortColumn, SortDescending=SortDescending };
}

public sealed class WatchlistMember : ObservableObject
{
    private bool enabled = true;
    public string Symbol { get; set; } = "";
    public bool Enabled { get => enabled; set => Set(ref enabled, value); }
    public int Order { get; set; }
    public WatchlistMember Copy() => new() { Symbol=Symbol, Enabled=Enabled, Order=Order };
}
