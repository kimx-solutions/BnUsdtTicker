using System.Globalization;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.ViewModels;

public sealed record AlertStrategyOption(AlertStrategy Value, string Label);

public sealed class AlertPolicyEditorViewModel : ObservableObject
{
    private AlertConditionPolicy state;
    private AlertStrategy strategy;
    private string cooldownMinutesText;
    public IReadOnlyList<AlertStrategyOption> Strategies { get; } = [new(AlertStrategy.Once, "單次"), new(AlertStrategy.Repeat, "可重複")];
    public AlertStrategy Strategy { get => strategy; set { if (Set(ref strategy, value)) { Notify(nameof(IsRepeat)); Notify(nameof(Status)); } } }
    public string CooldownMinutesText { get => cooldownMinutesText; set => Set(ref cooldownMinutesText, value); }
    public bool IsRepeat => Strategy == AlertStrategy.Repeat;
    public bool WasTriggered { get; private set; }
    public bool ResetRequested { get; private set; }
    public string Status => ResetRequested ? "待儲存重設" : !WasTriggered ? "等待中" : !IsRepeat ? "已提醒" :
        state.LastTriggeredAt is { } last && DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(state.CooldownMinutes)
        ? "冷卻中 · 需再次突破" : state.Armed ? "已武裝 · 等待再次突破" : "已提醒 · 等待回到未觸發區";
    public AlertPolicyEditorViewModel(AlertConditionPolicy policy, bool triggered)
    {
        state = policy.Copy(); strategy = policy.Strategy;
        cooldownMinutesText = policy.CooldownMinutes.ToString(CultureInfo.InvariantCulture); WasTriggered = triggered;
    }
    public AlertConditionPolicy Create(string label)
    {
        var result = state.Copy(); result.Strategy = Strategy;
        if (!int.TryParse(CooldownMinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var cooldown) || cooldown is < 0 or > 1440)
            throw new ArgumentException(label + "冷卻時間須為整數 0–1440 分鐘。");
        result.CooldownMinutes = cooldown; result.Validate(); return result;
    }
    public void Reset()
    {
        ResetRequested = true; WasTriggered = false; state.Armed = true; state.LastTriggeredAt = null; Notify(nameof(Status));
    }
    public void Refresh(AlertConditionPolicy live, bool triggered)
    {
        if (!ResetRequested) { state = live.Copy(); WasTriggered = triggered; }
        Notify(nameof(Status));
    }
}

public sealed class ShortTermAlertEditorViewModel : ObservableObject
{
    private string thresholdText, windowMinutesText;
    private bool comparisonReady;
    public string Label { get; }
    public string ThresholdText { get => thresholdText; set { if (Set(ref thresholdText, value)) Notify(nameof(Status)); } }
    public string WindowMinutesText { get => windowMinutesText; set { if (Set(ref windowMinutesText, value)) Notify(nameof(Status)); } }
    public AlertPolicyEditorViewModel Policy { get; }
    public RelayCommand ResetCommand { get; }
    public string Status => Policy.ResetRequested ? "待儲存重設" : string.IsNullOrWhiteSpace(ThresholdText) ? "未設定" :
        Policy.WasTriggered && !Policy.IsRepeat ? "已提醒" : !comparisonReady ? $"等待完整 {WindowMinutesText} 分鐘報價" : Policy.Status;
    public ShortTermAlertEditorViewModel(string label, ShortTermAlertSettings condition, Action reset)
    {
        Label = label; thresholdText = condition.ThresholdPercent?.ToString(CultureInfo.InvariantCulture) ?? "";
        windowMinutesText = condition.WindowMinutes.ToString(CultureInfo.InvariantCulture);
        comparisonReady = condition.ComparisonReady;
        Policy = new(condition.Policy, condition.Triggered); ResetCommand = new(reset);
        Policy.PropertyChanged += (_, _) => Notify(nameof(Status));
    }
    public ShortTermAlertSettings Create()
    {
        if (!int.TryParse(WindowMinutesText.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes is < 1 or > 60)
            throw new ArgumentException(Label + "區間須為整數 1–60 分鐘。");
        return new() { ThresholdPercent = AlertInput.Positive(ThresholdText, Label + "百分比"), WindowMinutes = minutes,
            Triggered = Policy.WasTriggered, Policy = Policy.Create(Label), ComparisonReady = comparisonReady };
    }
    public void Refresh(ShortTermAlertSettings live)
    {
        comparisonReady = live.ComparisonReady; Policy.Refresh(live.Policy, live.Triggered); Notify(nameof(Status));
    }
}

internal static class AlertInput
{
    internal static decimal? Positive(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || value <= 0)
            throw new ArgumentException(label + "須為正數，小數點請使用 .；留空可停用。");
        return value;
    }
}
