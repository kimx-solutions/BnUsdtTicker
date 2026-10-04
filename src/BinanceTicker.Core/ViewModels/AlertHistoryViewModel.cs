using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public sealed class AlertHistoryViewModel : ObservableObject
{
    private readonly AlertHistoryService history;
    private IReadOnlyList<AlertHistoryEntry> all = [];
    private string symbolFilter = "全部幣種";
    private AlertType? typeFilter;
    private DateTime? startDate, endDate;
    private bool newestFirst = true;
    private string error = "", loadError = "";
    public ObservableCollection<AlertHistoryEntry> Entries { get; } = [];
    public ObservableCollection<AlertHistoryRow> Rows { get; } = [];
    public ObservableCollection<string> Symbols { get; } = ["全部幣種"];
    public IReadOnlyList<AlertTypeOption> Types { get; } = [new(null, "全部類型"), new(AlertType.Upper, "上限"), new(AlertType.Lower, "下限"), new(AlertType.Rise, "短期上漲"), new(AlertType.Fall, "短期下跌")];
    public string SymbolFilter { get => symbolFilter; set { if (Set(ref symbolFilter, value ?? "")) Refresh(); } }
    public AlertType? TypeFilter { get => typeFilter; set { if (Set(ref typeFilter, value)) Refresh(); } }
    public DateTime? StartDate { get => startDate; set { if (Set(ref startDate, value)) Refresh(); } }
    public DateTime? EndDate { get => endDate; set { if (Set(ref endDate, value)) Refresh(); } }
    public bool NewestFirst { get => newestFirst; set { if (Set(ref newestFirst, value)) Refresh(); } }
    public string Error { get => error; private set => Set(ref error, value); }
    public string EmptyText => all.Count == 0 ? "尚無提醒紀錄" : "沒有符合篩選的紀錄";
    public bool IsEmpty => Entries.Count == 0;
    public string CountText => $"{Entries.Count} 筆提醒紀錄";
    public RelayCommand ReloadCommand { get; }
    public RelayCommand ClearFiltersCommand { get; }
    public AlertHistoryViewModel(AlertHistoryService history)
    {
        this.history = history;
        ReloadCommand = new(Reload);
        ClearFiltersCommand = new(() => { SymbolFilter = "全部幣種"; TypeFilter = null; StartDate = null; EndDate = null; });
    }
    public void Reload()
    {
        try
        {
            var loaded = history.Load();
            var selected = SymbolFilter;
            all = loaded; loadError = "";
            var symbols = all.Select(e => e.Symbol).Append(selected).Where(s => s.Length > 0 && s != "全部幣種").Distinct().Order(StringComparer.Ordinal).ToArray();
            Symbols.Clear(); Symbols.Add("全部幣種"); foreach (var symbol in symbols) Symbols.Add(symbol);
            SymbolFilter = selected;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { loadError = "無法讀取提醒紀錄，原檔已保留；請修復檔案或權限後重新載入。" + ex.Message; }
        Refresh();
    }
    public void RefreshAfterSubmission() => Reload();
    private void Refresh()
    {
        if (StartDate?.Date > EndDate?.Date) { Error = "起始日期不能晚於結束日期。"; return; }
        Error = loadError;
        IEnumerable<AlertHistoryEntry> result = all.Where(e =>
            (string.IsNullOrWhiteSpace(SymbolFilter) || SymbolFilter == "全部幣種" || e.Symbol == SymbolFilter) &&
            (TypeFilter is null || e.AlertType == TypeFilter) &&
            (StartDate is null || e.TriggeredAt.ToLocalTime().Date >= StartDate.Value.Date) &&
            (EndDate is null || e.TriggeredAt.ToLocalTime().Date <= EndDate.Value.Date));
        result = NewestFirst ? result.OrderByDescending(e => e.TriggeredAt) : result.OrderBy(e => e.TriggeredAt);
        Entries.Clear(); Rows.Clear();
        foreach (var entry in result) { Entries.Add(entry); Rows.Add(new(entry)); }
        Notify(nameof(EmptyText)); Notify(nameof(IsEmpty)); Notify(nameof(CountText));
    }
}

public sealed record AlertTypeOption(AlertType? Value, string Label);
public sealed record AlertHistoryRow(AlertHistoryEntry Entry)
{
    public string Symbol => Entry.Symbol;
    public string TypeText => Entry.AlertType switch { AlertType.Upper => "上限", AlertType.Lower => "下限", AlertType.Rise => "短期上漲", _ => "短期下跌" };
    public string ThresholdText => Entry.AlertType is AlertType.Upper or AlertType.Lower ? Format(Entry.TargetPrice) + " USDT" : $"{Entry.WindowMinutes} 分鐘 · {Format(Entry.ThresholdPercent)}%";
    public string PriceText => Format(Entry.TriggeredPrice);
    public string TimeText => Entry.TriggeredAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss zzz");
    public string DetailsText => Entry.BaselinePrice is null ? "" : $"基準 {Format(Entry.BaselinePrice)} USDT（{Entry.BaselineAt?.ToLocalTime():MM/dd HH:mm:ss}） → {Entry.QuoteAt?.ToLocalTime():HH:mm:ss} · 實際 {Format(Entry.ChangePercent)}%";
    private static string Format(decimal? value) => value?.ToString("0.############################", CultureInfo.InvariantCulture) ?? "—";
}
