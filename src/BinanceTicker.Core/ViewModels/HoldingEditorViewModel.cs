using System.Globalization;
using BinanceTicker.Core.Models;
namespace BinanceTicker.Core.ViewModels;

public sealed class HoldingEditorViewModel : ObservableObject
{
    private string quantityText;
    private string averageCostText;
    private bool alertEnabled=true;
    public string Symbol { get; }
    public string QuantityText { get=>quantityText; set=>Set(ref quantityText,value); }
    public string AverageCostText { get=>averageCostText; set=>Set(ref averageCostText,value); }
    public bool AlertEnabled { get=>alertEnabled; set=>Set(ref alertEnabled,value); }
    public HoldingEditorViewModel(string symbol,HoldingSetting? holding)
    {
        Symbol=symbol; quantityText=holding?.Quantity.ToString(CultureInfo.InvariantCulture) ?? "";
        averageCostText=holding?.AverageCost.ToString(CultureInfo.InvariantCulture) ?? "";
    }
    public HoldingSetting? CreateHolding()
    {
        if(string.IsNullOrWhiteSpace(QuantityText) && string.IsNullOrWhiteSpace(AverageCostText))return null;
        const NumberStyles styles=NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign;
        if (!decimal.TryParse(QuantityText.Trim(),styles,CultureInfo.InvariantCulture,out var quantity) || quantity<0 ||
            !decimal.TryParse(AverageCostText.Trim(),styles,CultureInfo.InvariantCulture,out var cost) || cost<0)
            throw new ArgumentException($"{Symbol}：數量與平均成本須填入非負數，小數點使用 .，不使用千分位；兩欄留空可清除持倉。");
        return new() { Symbol=Symbol,Quantity=quantity,AverageCost=cost };
    }
}
