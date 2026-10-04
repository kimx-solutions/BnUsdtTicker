namespace BinanceTicker.Core.Models;

public sealed class ShortTermAlertSettings
{
    public decimal? ThresholdPercent { get; set; }
    public int WindowMinutes { get; set; } = 5;
    public bool Triggered { get; set; }
    public AlertConditionPolicy Policy { get; set; } = new();
    public ShortTermAlertSettings Copy() => new()
    {
        ThresholdPercent = ThresholdPercent, WindowMinutes = WindowMinutes,
        Triggered = Triggered, Policy = Policy.Copy()
    };
}
