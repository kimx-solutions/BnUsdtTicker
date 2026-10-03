using System.Collections.ObjectModel;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Core.ViewModels;

public sealed class TickerRowViewModel(string symbol) : ObservableObject
{
    private TickerPrice? price;
    public string Symbol { get; } = symbol;
    public string Asset => Symbol[..^4];
    public string PriceText => price is null ? "—" : PriceFormatter.Format(price.Price);
    public string ChangeText => price is null ? "—" : PriceFormatter.Change(price.ChangePercent24h);
    public bool IsPositive => price?.ChangePercent24h >= 0;
    public bool HasPrice => price is not null;
    public string UpdatedText => price is null ? "等待報價" : "更新於 " + price.UpdatedAt.ToLocalTime().ToString("HH:mm:ss");

    public void Update(TickerPrice value)
    {
        if (price is not null && value.UpdatedAt < price.UpdatedAt) return;
        price = value;
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
    public ObservableCollection<TickerRowViewModel> Prices { get; } = [];
    public bool IsEmpty => Prices.Count == 0;
    public bool ShowChangePercent { get => showChangePercent; private set => Set(ref showChangePercent, value); }
    public bool CompactMode { get => compactMode; private set => Set(ref compactMode, value); }
    public string ModeText => mode == DisplayMode.Fix ? "FIX" : "FLOAT";
    public bool IsConnected => status == ConnectionStatus.Connected;
    public string StatusText => IsEmpty ? "請在設定中啟用幣種" : status switch
    {
        ConnectionStatus.Connected => "Connected · 即時報價",
        ConnectionStatus.Connecting => "Connecting · 連線中",
        _ => "Disconnected · 正在重連，保留最後報價"
    };

    public void Configure(AppSettings settings)
    {
        var existing = Prices.ToDictionary(p => p.Symbol);
        Prices.Clear();
        foreach (var symbol in settings.Symbols.Where(s => s.Enabled).OrderBy(s => s.Order))
            Prices.Add(existing.TryGetValue(symbol.Symbol, out var row) ? row : new(symbol.Symbol));
        ShowChangePercent = settings.Ui.ShowChangePercent;
        CompactMode = settings.Ui.CompactMode;
        SetMode(settings.Mode);
        Notify(nameof(IsEmpty)); Notify(nameof(StatusText));
    }
    public void SetMode(DisplayMode value) { mode = value; Notify(nameof(ModeText)); }
    public void Update(TickerPrice price) => Prices.FirstOrDefault(p => p.Symbol == price.Symbol)?.Update(price);
    public void SetStatus(ConnectionStatus value)
    {
        status = value;
        Notify(nameof(StatusText)); Notify(nameof(IsConnected));
    }
}
