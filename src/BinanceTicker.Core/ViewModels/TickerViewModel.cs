using System.Collections.ObjectModel;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public enum TickerSortColumn { Symbol, Price, ChangePercent }

public sealed class TickerRowViewModel(string symbol) : ObservableObject
{
    private TickerPrice? price;
    public string Symbol { get; } = symbol;
    public string Asset => Symbol[..^4];
    public decimal? Price => price?.Price;
    public decimal? ChangePercent24h => price?.ChangePercent24h;
    public string PriceText => price is null ? "—" : PriceFormatter.Format(price.Price);
    public string ChangeText => price is null ? "—" : PriceFormatter.Change(price.ChangePercent24h);
    public bool IsPositive => price?.ChangePercent24h >= 0;
    public bool HasPrice => price is not null;
    public string UpdatedText => price is null ? "等待報價" : "更新於 " + price.UpdatedAt.ToLocalTime().ToString("HH:mm:ss");

    public void Update(TickerPrice value)
    {
        if (price is not null && value.UpdatedAt < price.UpdatedAt) return;
        price = value;
        Notify(nameof(Price)); Notify(nameof(ChangePercent24h));
        Notify(nameof(PriceText)); Notify(nameof(ChangeText)); Notify(nameof(IsPositive));
        Notify(nameof(HasPrice)); Notify(nameof(UpdatedText));
    }
}

public sealed class TickerViewModel : ObservableObject
{
    private ConnectionStatus status = ConnectionStatus.Connecting;
    private bool showChangePercent = true;
    private bool compactMode = true;
    private DisplayMode mode;
    private ColorTheme theme;
    public bool IsLightTheme => theme == ColorTheme.Light;
    public string ThemeToggleText => IsLightTheme ? "切換為深色模式" : "切換為淺色模式";
    private TickerSortColumn? sortColumn;
    private bool sortDescending;
    public TickerSortColumn? SortColumn => sortColumn;
    public string SymbolSortHeader => "幣種" + SortIndicator(TickerSortColumn.Symbol);
    public string PriceSortHeader => "價格 · USDT" + SortIndicator(TickerSortColumn.Price);
    public string ChangeSortHeader => "24h" + SortIndicator(TickerSortColumn.ChangePercent);
    public string SortDescription => sortColumn is null ? "點擊欄位標題排序。" :
        $"目前依{sortColumn switch { TickerSortColumn.Symbol => "幣種", TickerSortColumn.Price => "價格", _ => "24 小時漲跌幅" }}{(sortDescending ? "降冪" : "升冪")}排序；再次點擊同一欄位切換方向。";
    public RelayCommand SortSymbolCommand { get; }
    public RelayCommand SortPriceCommand { get; }
    public RelayCommand SortChangeCommand { get; }
    public ObservableCollection<TickerRowViewModel> Prices { get; } = [];
    public bool IsEmpty => Prices.Count == 0;
    public bool ShowChangePercent { get => showChangePercent; private set => Set(ref showChangePercent, value); }
    public bool CompactMode { get => compactMode; private set => Set(ref compactMode, value); }
    public string ModeText => mode == DisplayMode.Fix ? "FIX" : "FLOAT";
    public ConnectionStatus Status => status;
    public bool IsConnected => status == ConnectionStatus.Connected;
    public string StatusText => IsEmpty ? "請在設定中啟用幣種" : status switch
    {
        ConnectionStatus.Connected => "Connected · 即時報價",
        ConnectionStatus.Connecting => "Connecting · 連線中",
        _ => "Disconnected · 正在重連，保留最後報價"
    };

    public TickerViewModel()
    {
        SortSymbolCommand = new(() => SortBy(TickerSortColumn.Symbol));
        SortPriceCommand = new(() => SortBy(TickerSortColumn.Price));
        SortChangeCommand = new(() => SortBy(TickerSortColumn.ChangePercent));
    }

    public void Configure(AppSettings settings)
    {
        var existing = Prices.ToDictionary(p => p.Symbol);
        Prices.Clear();
        foreach (var symbol in settings.Symbols.Where(s => s.Enabled).OrderBy(s => s.Order))
            Prices.Add(existing.TryGetValue(symbol.Symbol, out var row) ? row : new(symbol.Symbol));
        ShowChangePercent = settings.Ui.ShowChangePercent;
        CompactMode = settings.Ui.CompactMode;
        SetMode(settings.Mode);
        SetTheme(settings.Ui.Theme);
        ApplySort();
        Notify(nameof(IsEmpty)); Notify(nameof(StatusText));
    }
    public void SetMode(DisplayMode value) { mode = value; Notify(nameof(ModeText)); }
    public void SetTheme(ColorTheme value)
    {
        theme = value;
        Notify(nameof(IsLightTheme)); Notify(nameof(ThemeToggleText));
    }
    public void SortBy(TickerSortColumn column)
    {
        sortDescending = sortColumn == column && !sortDescending;
        sortColumn = column;
        ApplySort();
        Notify(nameof(SortColumn));
        Notify(nameof(SymbolSortHeader)); Notify(nameof(PriceSortHeader)); Notify(nameof(ChangeSortHeader));
        Notify(nameof(SortDescription));
    }

    private string SortIndicator(TickerSortColumn column) => sortColumn != column ? "" : sortDescending ? " ▼" : " ▲";

    private void ApplySort()
    {
        if (sortColumn is null) return;
        IOrderedEnumerable<TickerRowViewModel> sorted;
        if (sortColumn == TickerSortColumn.Symbol)
            sorted = sortDescending ? Prices.OrderByDescending(p => p.Symbol, StringComparer.Ordinal) : Prices.OrderBy(p => p.Symbol, StringComparer.Ordinal);
        else
        {
            // Missing quotes stay last in either direction; sort the raw decimal values.
            var quotedFirst = Prices.OrderBy(p => !p.HasPrice);
            decimal? Value(TickerRowViewModel row) => sortColumn == TickerSortColumn.Price ? row.Price : row.ChangePercent24h;
            sorted = sortDescending ? quotedFirst.ThenByDescending(Value) : quotedFirst.ThenBy(Value);
        }
        var rows = sorted.ThenBy(p => p.Symbol, StringComparer.Ordinal).ToArray();
        for (var i = 0; i < rows.Length; i++)
        {
            var current = Prices.IndexOf(rows[i]);
            if (current != i) Prices.Move(current, i);
        }
    }

    public void Update(TickerPrice price)
    {
        var row = Prices.FirstOrDefault(p => p.Symbol == price.Symbol);
        if (row is null) return;
        row.Update(price);
        ApplySort();
    }
    public void SetStatus(ConnectionStatus value)
    {
        status = value;
        Notify(nameof(Status)); Notify(nameof(StatusText)); Notify(nameof(IsConnected));
    }
}
