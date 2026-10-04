namespace BinanceTicker.Core.Models;

public enum AlertType { Upper, Lower, Rise, Fall }

public sealed class PriceAlertSettings
{
    public decimal? UpperPrice { get; set; }
    public bool UpperTriggered { get; set; }
    public decimal? LowerPrice { get; set; }
    public bool LowerTriggered { get; set; }
    public AlertConditionPolicy UpperPolicy { get; set; } = new();
    public AlertConditionPolicy LowerPolicy { get; set; } = new();
    public ShortTermAlertSettings Rise { get; set; } = new();
    public ShortTermAlertSettings Fall { get; set; } = new();

    public PriceAlertSettings Copy() => new()
    {
        UpperPrice = UpperPrice, UpperTriggered = UpperTriggered,
        LowerPrice = LowerPrice, LowerTriggered = LowerTriggered,
        UpperPolicy = UpperPolicy.Copy(), LowerPolicy = LowerPolicy.Copy(),
        Rise = Rise.Copy(), Fall = Fall.Copy()
    };

    public void Validate()
    {
        if (UpperPrice is <= 0 || LowerPrice is <= 0 || UpperPolicy is null || LowerPolicy is null || Rise is null || Fall is null)
            throw new ArgumentException("提醒價格須為正數，提醒設定不可為空。");
        UpperPolicy.Validate(); LowerPolicy.Validate();
        foreach (var condition in new[] { Rise, Fall })
        {
            if (condition.Policy is null || condition.WindowMinutes is < 1 or > 60 || condition.ThresholdPercent is <= 0)
                throw new ArgumentException("短期區間須為 1–60 分鐘，漲跌幅門檻須為正數。");
            condition.Policy.Validate();
        }
    }

    public AlertConditionPolicy Policy(AlertType type) => type switch
    {
        AlertType.Upper => UpperPolicy, AlertType.Lower => LowerPolicy,
        AlertType.Rise => Rise.Policy, AlertType.Fall => Fall.Policy,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public bool Triggered(AlertType type) => type switch
    {
        AlertType.Upper => UpperTriggered, AlertType.Lower => LowerTriggered,
        AlertType.Rise => Rise.Triggered, AlertType.Fall => Fall.Triggered,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public void SetTriggered(AlertType type, bool value)
    {
        switch (type)
        {
            case AlertType.Upper: UpperTriggered = value; break;
            case AlertType.Lower: LowerTriggered = value; break;
            case AlertType.Rise: Rise.Triggered = value; break;
            case AlertType.Fall: Fall.Triggered = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
