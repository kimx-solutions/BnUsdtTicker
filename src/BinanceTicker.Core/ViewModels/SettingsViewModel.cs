using System.Collections.ObjectModel;
using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings original;
    private readonly IBinanceService binance;
    private readonly CancellationTokenSource lifetime = new();
    private string newSymbol = "";
    private string error = "";
    private bool isBusy;
    private SymbolSetting? selectedSymbol;
    private bool showSparkline;
    private string sparklineRange = "1h";
    private bool showSparklineEdited;
    private bool sparklineRangeEdited;
    public ObservableCollection<SymbolSetting> Symbols { get; private set; }
    public string NewSymbol { get => newSymbol; set => Set(ref newSymbol, value); }
    public string Error { get => error; set => Set(ref error, value); }
    public bool IsBusy
    {
        get => isBusy;
        private set { Set(ref isBusy, value); Notify(nameof(CanSave)); AddCommand.Refresh(); }
    }
    public bool CanSave => !IsBusy;
    public SymbolSetting? SelectedSymbol
    {
        get => selectedSymbol;
        set { Set(ref selectedSymbol, value); RefreshSelection(); }
    }
    public DisplayMode Mode { get; set; }
    public Array Modes { get; } = Enum.GetValues<DisplayMode>();
    public bool ShowOnStartup { get; set; }
    public bool StartWithWindows { get; set; }
    public bool HotkeyEnabled { get; set; }
    public string HotkeyText { get; set; } = "Ctrl+Alt+T";
    public bool ShowChangePercent { get; set; }
    public bool CompactMode { get; set; }
    public bool ShowSparkline
    {
        get => showSparkline;
        set { if (Set(ref showSparkline, value)) showSparklineEdited = true; }
    }
    public string SparklineRange
    {
        get => sparklineRange;
        set { if (Set(ref sparklineRange, value)) sparklineRangeEdited = true; }
    }
    public IReadOnlyList<string> SparklineRanges { get; } = ["1h", "24h"];
    public double Opacity { get; set; }
    public AsyncCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    public SettingsViewModel(AppSettings settings, IBinanceService binance)
    {
        original = settings.Copy();
        legacyEditor = original.Watchlists is null;
        WatchlistSettings.Normalize(original);
        this.binance = binance;
        Symbols = new(original.Symbols.OrderBy(s => s.Order).Select(s => s.Copy()));
        Mode = original.Mode; ShowOnStartup = original.ShowOnStartup;
        StartWithWindows = original.StartWithWindows;
        HotkeyEnabled = original.Hotkey.Enabled; HotkeyText = original.Hotkey.Gesture;
        ShowChangePercent = original.Ui.ShowChangePercent; CompactMode = original.Ui.CompactMode;
        Opacity = original.Window.Opacity;
        showSparkline = original.Ui.ShowSparkline; sparklineRange = original.Ui.SparklineRange;
        AddCommand = new(AddAsync, () => !IsBusy);
        RemoveCommand = new(Remove, () => SelectedSymbol is not null);
        MoveUpCommand = new(() => Move(-1), () => SelectedSymbol is not null && Symbols.IndexOf(SelectedSymbol) > 0);
        MoveDownCommand = new(() => Move(1), () => SelectedSymbol is not null && Symbols.IndexOf(SelectedSymbol) < Symbols.Count - 1);
        SelectedSymbol = Symbols.FirstOrDefault();
        InitializeWatchlists();
    }

    public async Task AddAsync()
    {
        if (IsBusy || lifetime.IsCancellationRequested) return;
        Error = "";
        try
        {
            var symbol = SymbolNormalizer.Normalize(NewSymbol);
            if (Symbols.Any(s => s.Symbol == symbol)) { Error = "此幣種已在清單中。"; return; }
            IsBusy = true;
            if (!await binance.IsValidSymbolAsync(symbol, lifetime.Token))
            { Error = "Binance 沒有可交易的此 USDT 現貨交易對。"; return; }
            if (lifetime.IsCancellationRequested) return;
            var item = new SymbolSetting { Symbol = symbol, Order = Symbols.Count + 1 };
            EnsureHoldingEditor(symbol);
            Symbols.Add(item); SelectedSymbol = item; NewSymbol = "";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (ArgumentException ex) { Error = ex.Message; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or
                                   KeyNotFoundException or InvalidOperationException)
        { Error = "無法驗證幣種，請確認網路後重試。"; }
        finally { IsBusy = false; }
    }

    public AppSettings CreateSettings()
    {
        FlushWatchlist();
        var settings = original.Copy();
        settings.Mode = Mode; settings.ShowOnStartup = ShowOnStartup;
        settings.StartWithWindows = StartWithWindows;
        settings.Hotkey = new() { Enabled = HotkeyEnabled,
            Gesture = HotkeyEnabled ? HotkeyGesture.Parse(HotkeyText).ToString() : original.Hotkey.Gesture };
        settings.Window.Opacity = Opacity;
        settings.Ui.ShowChangePercent = ShowChangePercent; settings.Ui.CompactMode = CompactMode;
        settings.Ui.ShowSparkline = ShowSparkline; settings.Ui.SparklineRange = SparklineRange;
        settings.Watchlists = Watchlists.Select(g=>g.Copy()).ToList();
        settings.Holdings = Holdings.Select(h=>h.CreateHolding()).OfType<HoldingSetting>().ToList();
        var visibleOrder = Symbols.Select(s=>s.Symbol).ToArray();
        settings.Symbols = settings.Symbols.OrderBy(s=>Array.IndexOf(visibleOrder,s.Symbol) is var index && index>=0 ? index : int.MaxValue).ToList();
        for(var i=0;i<settings.Symbols.Count;i++)
        {
            var item=settings.Symbols[i];item.Order=i+1;
            item.Enabled=Holdings.First(h=>h.Symbol==item.Symbol).AlertEnabled;
            // Keep old callers' single-list enabled semantics before they adopt explicit groups.
            if(legacyEditor && !MarketDemand.HasAlert(item.Alert) && Symbols.FirstOrDefault(s=>s.Symbol==item.Symbol) is { } member)
                item.Enabled=member.Enabled;
        }
        if(!settings.Watchlists.Any(g=>g.Id==settings.ActiveWatchlistId))settings.ActiveWatchlistId=settings.Watchlists[0].Id;
        return settings;
    }

    public void PreserveUneditedSparklinePreferences(AppSettings edited, AppSettings current)
    {
        if (!showSparklineEdited) edited.Ui.ShowSparkline = current.Ui.ShowSparkline;
        if (!sparklineRangeEdited) edited.Ui.SparklineRange = current.Ui.SparklineRange;
    }

    private void Remove()
    {
        if (SelectedSymbol is not { } item) return;
        var index = Symbols.IndexOf(item);
        Symbols.Remove(item);
        SelectedSymbol = Symbols.Count == 0 ? null : Symbols[Math.Min(index, Symbols.Count - 1)];
        RefreshSelection();
    }
    private void Move(int delta)
    {
        if (SelectedSymbol is not { } item) return;
        var from = Symbols.IndexOf(item);
        var to = from + delta;
        if (to >= 0 && to < Symbols.Count) Symbols.Move(from, to);
        RefreshSelection();
    }
    private void RefreshSelection() { RemoveCommand.Refresh(); MoveUpCommand.Refresh(); MoveDownCommand.Refresh(); }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}
