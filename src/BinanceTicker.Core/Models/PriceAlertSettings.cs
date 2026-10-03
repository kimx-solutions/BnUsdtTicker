namespace BinanceTicker.Core.Models;

public enum AlertType { Upper, Lower }

public sealed class PriceAlertSettings
{
    public decimal? UpperPrice { get; set; }
    public bool UpperTriggered { get; set; }
    public decimal? LowerPrice { get; set; }
    public bool LowerTriggered { get; set; }

    public PriceAlertSettings Copy() => new()
    {
        UpperPrice = UpperPrice, UpperTriggered = UpperTriggered,
        LowerPrice = LowerPrice, LowerTriggered = LowerTriggered
    };
}
