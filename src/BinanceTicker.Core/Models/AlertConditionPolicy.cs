namespace BinanceTicker.Core.Models;

public enum AlertStrategy { Once, Repeat }

public sealed class AlertConditionPolicy
{
    public AlertStrategy Strategy { get; set; }
    public int CooldownMinutes { get; set; }
    public DateTimeOffset? LastTriggeredAt { get; set; }
    public bool Armed { get; set; } = true;
    public AlertConditionPolicy Copy() => new()
    {
        Strategy = Strategy, CooldownMinutes = CooldownMinutes,
        LastTriggeredAt = LastTriggeredAt, Armed = Armed
    };
    public void Validate()
    {
        if (!Enum.IsDefined(Strategy) || CooldownMinutes is < 0 or > 1440 || LastTriggeredAt == default(DateTimeOffset))
            throw new ArgumentException("提醒策略無效；冷卻時間須為 0–1440 分鐘。");
    }
}
