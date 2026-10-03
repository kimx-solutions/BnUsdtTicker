using System.Globalization;
using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.ViewModels;

public sealed class PriceAlertEditorViewModel : ObservableObject
{
    private readonly PriceAlertSettings alert;
    private string upperPriceText;
    private string lowerPriceText;
    public string Symbol { get; }
    public string UpperPriceText { get => upperPriceText; set => Set(ref upperPriceText, value); }
    public string LowerPriceText { get => lowerPriceText; set => Set(ref lowerPriceText, value); }
    public string UpperStatus => Status(UpperPriceText, alert.UpperTriggered, ResetUpperRequested);
    public string LowerStatus => Status(LowerPriceText, alert.LowerTriggered, ResetLowerRequested);
    public bool ResetUpperRequested { get; private set; }
    public bool ResetLowerRequested { get; private set; }
    public RelayCommand ResetUpperCommand { get; }
    public RelayCommand ResetLowerCommand { get; }
    public RelayCommand ResetBothCommand { get; }

    public PriceAlertEditorViewModel(string symbol, PriceAlertSettings alert)
    {
        Symbol = symbol;
        this.alert = alert.Copy();
        upperPriceText = alert.UpperPrice?.ToString(CultureInfo.InvariantCulture) ?? "";
        lowerPriceText = alert.LowerPrice?.ToString(CultureInfo.InvariantCulture) ?? "";
        ResetUpperCommand = new(() => Reset(AlertType.Upper));
        ResetLowerCommand = new(() => Reset(AlertType.Lower));
        ResetBothCommand = new(() => { Reset(AlertType.Upper); Reset(AlertType.Lower); });
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UpperPriceText)) Notify(nameof(UpperStatus));
            if (e.PropertyName == nameof(LowerPriceText)) Notify(nameof(LowerStatus));
        };
    }

    public PriceAlertSettings CreateAlert()
    {
        var result = alert.Copy();
        result.UpperPrice = Parse(UpperPriceText, "上限");
        result.LowerPrice = Parse(LowerPriceText, "下限");
        return result;
    }

    private decimal? Parse(string text, string side)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) || price <= 0)
            throw new ArgumentException($"{Symbol} {side}價格須為正數，小數點請使用 .；留空可停用。");
        return price;
    }

    private static string Status(string text, bool triggered, bool reset) =>
        reset ? "待儲存重設" : triggered ? "已提醒" : string.IsNullOrWhiteSpace(text) ? "未設定" : "等待中";

    private void Reset(AlertType type)
    {
        if (type == AlertType.Upper) { ResetUpperRequested = true; alert.UpperTriggered = false; }
        else { ResetLowerRequested = true; alert.LowerTriggered = false; }
        Notify(nameof(UpperStatus)); Notify(nameof(LowerStatus));
    }

    public void RefreshState(PriceAlertSettings live)
    {
        if (!ResetUpperRequested) alert.UpperTriggered = live.UpperTriggered;
        if (!ResetLowerRequested) alert.LowerTriggered = live.LowerTriggered;
        Notify(nameof(UpperStatus)); Notify(nameof(LowerStatus));
    }
}
