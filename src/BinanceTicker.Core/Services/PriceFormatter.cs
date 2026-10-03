using System.Globalization;

namespace BinanceTicker.Core.Services;

public static class PriceFormatter
{
    public static string Format(decimal price) => price.ToString(
        price >= 1000 ? "N2" : price >= 1 ? "N4" : price >= 0.01m ? "N5" : "N8",
        CultureInfo.InvariantCulture);
    public static string Change(decimal change) => change.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%";
}
