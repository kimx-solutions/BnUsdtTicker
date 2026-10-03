using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Core.Models;

public sealed class SymbolSetting : ObservableObject
{
    private bool enabled = true;
    public string Symbol { get; set; } = "";
    public bool Enabled { get => enabled; set => Set(ref enabled, value); }
    public int Order { get; set; }
    public PriceAlertSettings Alert { get; set; } = new();
    public SymbolSetting Copy() => new() { Symbol = Symbol, Enabled = Enabled, Order = Order, Alert = Alert?.Copy() ?? new() };
}
