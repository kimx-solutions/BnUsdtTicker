using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using System.Globalization;

namespace BinanceTicker.Services;

public sealed class NotificationService(Action<string, string> show) : INotificationService
{
    public NotificationService(TrayIconService tray) : this(tray.ShowNotification) { }
    public void Show(AlertHistoryEntry entry)
    {
        var side = entry.AlertType == AlertType.Upper ? "上限" : "下限";
        show($"{entry.Symbol} 價格提醒",
            $"已到達{side}價格\n目前價格：{entry.TriggeredPrice.ToString("#,0.00##########################", CultureInfo.InvariantCulture)}\n設定價格：{entry.TargetPrice?.ToString("#,0.############################", CultureInfo.InvariantCulture)}");
    }
}
