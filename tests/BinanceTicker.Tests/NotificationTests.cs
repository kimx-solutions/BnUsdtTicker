using BinanceTicker.Core.Models;
using BinanceTicker.Services;

namespace BinanceTicker.Tests;

public sealed class NotificationTests
{
    [Theory]
    [InlineData(AlertType.Upper, "已到達上限價格")]
    [InlineData(AlertType.Lower, "已到達下限價格")]
    public void NotificationContainsSymbolSideAndBothPrices(AlertType type, string side)
    {
        string? title = null, message = null;
        var notifications = new NotificationService((t, m) => { title = t; message = m; });
        notifications.Show(new("BTCUSDT", type, 85000m, 85120.5m, DateTimeOffset.Now));
        Assert.Equal("BTCUSDT 價格提醒", title);
        Assert.Contains(side, message);
        Assert.Contains("目前價格：85,120.50", message);
        Assert.Contains("設定價格：85,000", message);
    }

    [Fact]
    public void TinyTargetsAndHighPrecisionQuotesAreNotRoundedAwayInNotifications()
    {
        string? message = null;
        var notifications = new NotificationService((_, m) => message = m);
        notifications.Show(new("ENAUSDT", AlertType.Lower, 0.000000000001m, 0.0000000000005m, DateTimeOffset.Now));
        Assert.Contains("目前價格：0.0000000000005", message);
        Assert.Contains("設定價格：0.000000000001", message);
    }
}
