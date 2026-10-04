using System.Collections.ObjectModel;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public enum TickerSortColumn { Symbol, Price, ChangePercent }

public sealed class TickerRowViewModel(string symbol) : ObservableObject
{
    private TickerPrice? price;
    private bool upperTriggered;
    private bool lowerTriggered;
    private bool riseTriggered;
    private bool fallTriggered;
    private bool hasConfiguredAlert;
    public string Symbol { get; } = symbol;
    public string Asset => Symbol[..^4];
    public decimal? Price => price?.Price;
    public decimal? ChangePercent24h => price?.ChangePercent24h;
    public string PriceText => price is null ? "—" : PriceFormatter.Format(price.Price);
    public string ChangeText => price is null ? "—" : PriceFormatter.Change(price.ChangePercent24h);
    public bool IsPositive => price?.ChangePercent24h >= 0;
    public bool HasPrice => price is not null;
    public bool HasTriggeredAlert => upperTriggered || lowerTriggered || riseTriggered || fallTriggered;
    public bool HasConfiguredAlert => hasConfiguredAlert;
    public string AlertButtonText => HasTriggeredAlert ? $"{Asset} {AlertStatusText}" :
        HasConfiguredAlert ? $"編輯 {Asset} 價格警示" : $"新增 {Asset} 價格警示";
    public string AlertStatusText => "價格提醒：" + string.Join("、", new[]
    {
        upperTriggered ? "上限已提醒" : null, lowerTriggered ? "下限已提醒" : null,
        riseTriggered ? "短期上漲已提醒" : null, fallTriggered ? "短期下跌已提醒" : null
    }.Where(s => s is not null)) + "；點擊開啟警示視窗重設。";
    public string UpdatedText => price is null ? "等待報價" : "更新於 " + price.UpdatedAt.ToLocalTime().ToString("HH:mm:ss");

    private SparklineSeries? series;
    private string quoteStatusText = "等待報價";
    private string historyStatusText = "等待走勢資料";
    public SparklineSeries? Series { get => series; private set => Set(ref series, value); }
    public string QuoteStatusText { get => quoteStatusText; private set => Set(ref quoteStatusText, value); }
    public string HistoryStatusText { get => historyStatusText; private set => Set(ref historyStatusText, value); }
    private string historyUpdatedText = "等待走勢資料";
    private string historyCoverageText = "尚無有效走勢資料";
    private string sparklineToolTip = "等待走勢資料";
    public string HistoryUpdatedText { get => historyUpdatedText; private set => Set(ref historyUpdatedText, value); }
    public string HistoryCoverageText { get => historyCoverageText; private set => Set(ref historyCoverageText, value); }
    public string SparklineToolTip { get => sparklineToolTip; private set => Set(ref sparklineToolTip, value); }
    public string HighPriceText => FormatOptional(price?.HighPrice24h);
    public string LowPriceText => FormatOptional(price?.LowPrice24h);
    public string VolumeText => FormatOptional(price?.Volume24h, Asset);
    public string QuoteVolumeText => FormatOptional(price?.QuoteVolume24h, "USDT");
    private static string FormatOptional(decimal? value, string? unit = null) =>
        value is null ? "—" : PriceFormatter.Format(value.Value) + (unit is null ? "" : " " + unit);

    public void UpdateHistory(IReadOnlyList<CandlePrice> candles, HistoryLoadState state,
        ConnectionStatus status, string range, DateTimeOffset now)
    {
        Series = SparklineProjection.Create(candles, now, range == "24h" ? TimeSpan.FromHours(24) : TimeSpan.FromHours(1));
        QuoteStatusText = price is null ? "等待報價" :
            status != ConnectionStatus.Connected ? "連線中斷 · 保留最後報價" :
            now - new DateTimeOffset(price.UpdatedAt.ToUniversalTime()) > TimeSpan.FromSeconds(60) ? "報價逾期 · 保留最後報價" : "即時報價";
        var latest = candles.Count == 0 ? (DateTimeOffset?)null : candles.Max(c => c.UpdatedAt);
        HistoryUpdatedText = latest is null ? "等待走勢資料" : "走勢更新於 " + latest.Value.ToLocalTime().ToString("MM/dd HH:mm:ss");
        var points = Series.Segments.SelectMany(s => s.Points).ToArray();
        HistoryCoverageText = points.Length == 0 ? "尚無有效走勢資料" :
            "有效資料 " + points.Min(p => p.Time).ToLocalTime().ToString("MM/dd HH:mm:ss") + " — " +
            points.Max(p => p.Time).ToLocalTime().ToString("MM/dd HH:mm:ss");
        HistoryStatusText = state.Status switch
        {
            HistoryLoadStatus.Loading => "走勢資料載入中…",
            HistoryLoadStatus.Failed => "走勢資料載入失敗 · 可重試",
            _ when latest is null || !Series.HasData => "走勢資料不足",
            _ when status != ConnectionStatus.Connected => "連線中斷 · 保留走勢資料",
            _ when now - latest.Value > TimeSpan.FromSeconds(60) => "走勢資料逾期",
            _ when !Series.IsComplete => "部分走勢資料",
            _ => ""
        };
        SparklineToolTip = range + " 走勢 · " + (HistoryStatusText.Length == 0 ? "完整資料" : HistoryStatusText) +
            "\n" + HistoryCoverageText + "\n" + HistoryUpdatedText +
            "\n曲線顏色依所選時段方向；旁邊數值為 Binance 滾動 24h 漲跌幅。";
    }

    public bool Update(TickerPrice value)
    {
        if (price is not null && value.UpdatedAt < price.UpdatedAt) return false;
        price = value;
        Notify(nameof(Price)); Notify(nameof(ChangePercent24h));
        Notify(nameof(PriceText)); Notify(nameof(ChangeText)); Notify(nameof(IsPositive));
        Notify(nameof(HasPrice)); Notify(nameof(UpdatedText));
        Notify(nameof(HighPriceText)); Notify(nameof(LowPriceText)); Notify(nameof(VolumeText)); Notify(nameof(QuoteVolumeText));
        return true;
    }

    public void SetAlertState(PriceAlertSettings alert)
    {
        upperTriggered = alert.UpperTriggered;
        lowerTriggered = alert.LowerTriggered;
        riseTriggered = alert.Rise.Triggered; fallTriggered = alert.Fall.Triggered;
        hasConfiguredAlert = alert.UpperPrice is not null || alert.LowerPrice is not null || alert.Rise.ThresholdPercent is not null || alert.Fall.ThresholdPercent is not null;
        Notify(nameof(HasTriggeredAlert)); Notify(nameof(AlertStatusText));
        Notify(nameof(HasConfiguredAlert)); Notify(nameof(AlertButtonText));
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
    private bool showSparkline = true;
    private string sparklineRange = "1h";
    public bool ShowSparkline { get => showSparkline; private set => Set(ref showSparkline, value); }
    public string SparklineRange { get => sparklineRange; private set { if (Set(ref sparklineRange, value)) { Notify(nameof(IsHourRange)); Notify(nameof(IsDayRange)); } } }
    public bool IsHourRange => SparklineRange == "1h";
    public bool IsDayRange => SparklineRange == "24h";
    public string SparklineToggleText => ShowSparkline ? "隱藏走勢" : "顯示走勢";
    public RelayCommand SelectHourCommand { get; }
    public RelayCommand SelectDayCommand { get; }
    public RelayCommand ToggleSparklineCommand { get; }
    public event Action? SparklinePreferencesChanged;
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
        SelectHourCommand = new(() => SelectRange("1h"));
        SelectDayCommand = new(() => SelectRange("24h"));
        ToggleSparklineCommand = new(() => { ShowSparkline = !ShowSparkline; Notify(nameof(SparklineToggleText)); SparklinePreferencesChanged?.Invoke(); });
        SortSymbolCommand = new(() => SortBy(TickerSortColumn.Symbol));
        SortPriceCommand = new(() => SortBy(TickerSortColumn.Price));
        SortChangeCommand = new(() => SortBy(TickerSortColumn.ChangePercent));
    }

    private void SelectRange(string range)
    {
        if (SparklineRange == range) return;
        SparklineRange = range;
        SparklinePreferencesChanged?.Invoke();
    }

    public void Configure(AppSettings settings)
    {
        var existing = Prices.ToDictionary(p => p.Symbol);
        Prices.Clear();
        foreach (var symbol in settings.Symbols.Where(s => s.Enabled).OrderBy(s => s.Order))
        {
            var row = existing.TryGetValue(symbol.Symbol, out var existingRow) ? existingRow : new(symbol.Symbol);
            row.SetAlertState(symbol.Alert);
            Prices.Add(row);
        }
        ShowSparkline = settings.Ui.ShowSparkline;
        SparklineRange = settings.Ui.SparklineRange == "24h" ? "24h" : "1h";
        Notify(nameof(SparklineToggleText));
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

    public bool Update(TickerPrice price)
    {
        var row = Prices.FirstOrDefault(p => p.Symbol == price.Symbol);
        if (row is null || !row.Update(price)) return false;
        ApplySort();
        return true;
    }
    public void SetAlertState(string symbol, PriceAlertSettings alert)
    {
        Prices.FirstOrDefault(p => p.Symbol == symbol)?.SetAlertState(alert);
    }
    public void SetStatus(ConnectionStatus value)
    {
        status = value;
        Notify(nameof(Status)); Notify(nameof(StatusText)); Notify(nameof(IsConnected));
    }
}
