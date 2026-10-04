using System.Globalization;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.ViewModels;

public sealed class PriceAlertEditorViewModel : ObservableObject
{
    private readonly PriceAlertSettings alert;
    private readonly HashSet<AlertType> resets = [];
    private string upperPriceText, lowerPriceText, error = "";
    public string Symbol { get; }
    public string Error { get => error; set => Set(ref error, value); }
    public string UpperPriceText { get => upperPriceText; set { if (Set(ref upperPriceText, value)) Notify(nameof(UpperStatus)); } }
    public string LowerPriceText { get => lowerPriceText; set { if (Set(ref lowerPriceText, value)) Notify(nameof(LowerStatus)); } }
    public AlertPolicyEditorViewModel UpperPolicy { get; }
    public AlertPolicyEditorViewModel LowerPolicy { get; }
    public ShortTermAlertEditorViewModel Rise { get; }
    public ShortTermAlertEditorViewModel Fall { get; }
    public string UpperStatus => string.IsNullOrWhiteSpace(UpperPriceText) && !UpperPolicy.WasTriggered && !UpperPolicy.ResetRequested ? "未設定" : UpperPolicy.Status;
    public string LowerStatus => string.IsNullOrWhiteSpace(LowerPriceText) && !LowerPolicy.WasTriggered && !LowerPolicy.ResetRequested ? "未設定" : LowerPolicy.Status;
    public bool ResetUpperRequested => resets.Contains(AlertType.Upper);
    public bool ResetLowerRequested => resets.Contains(AlertType.Lower);
    public RelayCommand ResetUpperCommand { get; }
    public RelayCommand ResetLowerCommand { get; }
    public RelayCommand ResetBothCommand { get; }
    public PriceAlertEditorViewModel(string symbol, PriceAlertSettings alert)
    {
        Symbol = symbol; this.alert = alert.Copy();
        upperPriceText = alert.UpperPrice?.ToString(CultureInfo.InvariantCulture) ?? "";
        lowerPriceText = alert.LowerPrice?.ToString(CultureInfo.InvariantCulture) ?? "";
        UpperPolicy = new(alert.UpperPolicy, alert.UpperTriggered); LowerPolicy = new(alert.LowerPolicy, alert.LowerTriggered);
        Rise = new("短期上漲", alert.Rise, () => Reset(AlertType.Rise));
        Fall = new("短期下跌", alert.Fall, () => Reset(AlertType.Fall));
        ResetUpperCommand = new(() => Reset(AlertType.Upper)); ResetLowerCommand = new(() => Reset(AlertType.Lower));
        ResetBothCommand = new(() => { foreach (var type in Enum.GetValues<AlertType>()) Reset(type); });
        UpperPolicy.PropertyChanged += (_, _) => Notify(nameof(UpperStatus));
        LowerPolicy.PropertyChanged += (_, _) => Notify(nameof(LowerStatus));
    }
    public PriceAlertSettings CreateAlert()
    {
        var result = alert.Copy();
        result.UpperPrice = AlertInput.Positive(UpperPriceText, Symbol + " 上限價格");
        result.LowerPrice = AlertInput.Positive(LowerPriceText, Symbol + " 下限價格");
        result.UpperTriggered = UpperPolicy.WasTriggered; result.LowerTriggered = LowerPolicy.WasTriggered;
        result.UpperPolicy = UpperPolicy.Create("上限"); result.LowerPolicy = LowerPolicy.Create("下限");
        result.Rise = Rise.Create(); result.Fall = Fall.Create(); result.Validate(); return result;
    }
    public IReadOnlyList<AlertResetRequest> GetAlertResets() => resets.Select(type => new AlertResetRequest(Symbol, type)).ToArray();
    private void Reset(AlertType type)
    {
        resets.Add(type);
        switch (type)
        {
            case AlertType.Upper: UpperPolicy.Reset(); break;
            case AlertType.Lower: LowerPolicy.Reset(); break;
            case AlertType.Rise: Rise.Policy.Reset(); break;
            case AlertType.Fall: Fall.Policy.Reset(); break;
        }
        Notify(nameof(ResetUpperRequested)); Notify(nameof(ResetLowerRequested));
        Notify(nameof(UpperStatus)); Notify(nameof(LowerStatus));
    }
    public void RefreshState(PriceAlertSettings live)
    {
        UpperPolicy.Refresh(live.UpperPolicy, live.UpperTriggered); LowerPolicy.Refresh(live.LowerPolicy, live.LowerTriggered);
        Rise.Refresh(live.Rise); Fall.Refresh(live.Fall);
    }
}
