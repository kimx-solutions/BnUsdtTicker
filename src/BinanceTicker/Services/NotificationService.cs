using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using System.Globalization;

namespace BinanceTicker.Services;

public sealed class NotificationService(Action<string, string> show) : INotificationService
{
    public NotificationService(TrayIconService tray) : this(tray.ShowNotification) { }
    public void Show(AlertHistoryEntry entry)
    {
        if (entry.AlertType is AlertType.Rise or AlertType.Fall)
        {
            var direction = entry.AlertType == AlertType.Rise ? "上漲" : "下跌";
            string Format(decimal? value) => value?.ToString("0.############################", CultureInfo.InvariantCulture) ?? "—";
            show($"{entry.Symbol} 價格提醒", $"{entry.WindowMinutes} 分鐘內{direction}至少 {Format(entry.ThresholdPercent)}%\n基準價格：{Format(entry.BaselinePrice)}\n目前價格：{Format(entry.TriggeredPrice)}\n實際漲跌幅：{Format(entry.ChangePercent)}%");
            return;
        }
        var side = entry.AlertType == AlertType.Upper ? "上限" : "下限";
        show($"{entry.Symbol} 價格提醒",
            $"已到達{side}價格\n目前價格：{entry.TriggeredPrice.ToString("#,0.00##########################", CultureInfo.InvariantCulture)}\n設定價格：{entry.TargetPrice?.ToString("#,0.############################", CultureInfo.InvariantCulture)}");
    }
}
