namespace BinanceTicker.Core.Models;

public sealed class HoldingSetting
{
    public string Symbol { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal AverageCost { get; set; }
    public HoldingSetting Copy() => new() { Symbol=Symbol, Quantity=Quantity, AverageCost=AverageCost };
}
