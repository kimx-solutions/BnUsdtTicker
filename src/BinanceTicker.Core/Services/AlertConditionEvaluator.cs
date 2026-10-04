using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

internal enum AlertDecision { None, Arm, Consume, Notify }

internal static class AlertConditionEvaluator
{
    internal static AlertDecision Decide(bool matches, bool triggered, AlertConditionPolicy policy, DateTimeOffset now)
    {
        if (policy.Strategy == AlertStrategy.Once) return matches && !triggered ? AlertDecision.Notify : AlertDecision.None;
        if (!matches) return policy.Armed ? AlertDecision.None : AlertDecision.Arm;
        if (!policy.Armed) return AlertDecision.None;
        if (policy.LastTriggeredAt is { } last && (now < last || now - last < TimeSpan.FromMinutes(policy.CooldownMinutes)))
            return AlertDecision.Consume;
        return AlertDecision.Notify;
    }
}
