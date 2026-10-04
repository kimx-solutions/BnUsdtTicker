using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
using BinanceTicker.Services;

namespace BinanceTicker.Tests;

public sealed class AlertHistoryViewModelTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private AlertHistoryService History => new(Path.Combine(directory, "history.json"));
    private static readonly DateTimeOffset Time = new(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(8));
    private AlertHistoryEntry Fall(string symbol = "OLDUSDT") => new(symbol, AlertType.Fall, null, 98, Time.AddMinutes(1), 5, 2, 100, Time.AddMinutes(-4), Time.AddMinutes(1), -2);

    [Fact]
    public void QueriesMixedOldAndNewRecordsBySymbolTypeDateAndActualInstant()
    {
        History.Save([new("BTCUSDT", AlertType.Upper, 100, 101, Time), Fall(), new("BTCUSDT", AlertType.Lower, 90, 89, Time.AddDays(-1))]);
        var vm = new AlertHistoryViewModel(History); vm.Reload();
        Assert.Equal(3, vm.Entries.Count); Assert.Equal("OLDUSDT", vm.Entries[0].Symbol);
        Assert.Contains("OLDUSDT", vm.Symbols);
        vm.TypeFilter = AlertType.Fall; Assert.Single(vm.Entries);
        vm.TypeFilter = null; vm.SymbolFilter = "BTCUSDT"; Assert.Equal(2, vm.Entries.Count);
        vm.StartDate = Time.LocalDateTime.Date; vm.EndDate = Time.LocalDateTime.Date;
        Assert.Single(vm.Entries);
        vm.SymbolFilter = ""; Assert.Equal(2, vm.Entries.Count);
        vm.NewestFirst = false; Assert.Equal(AlertType.Upper, vm.Entries[0].AlertType);
    }

    [Fact]
    public void EndDateIncludesLastSecondButExcludesFollowingDay()
    {
        var localDate = Time.LocalDateTime.Date;
        var end = new DateTimeOffset(DateTime.SpecifyKind(localDate.AddDays(1).AddTicks(-1), DateTimeKind.Local));
        History.Save([new("BTCUSDT", AlertType.Upper, 100, 101, end), new("BTCUSDT", AlertType.Upper, 100, 101, end.AddTicks(1))]);
        var vm = new AlertHistoryViewModel(History) { StartDate = localDate, EndDate = localDate };
        vm.Reload(); Assert.Single(vm.Entries);
    }

    [Fact]
    public void CorruptionPreservesFileAndLastLoadedRowsWithRetry()
    {
        History.Save([Fall()]);
        var vm = new AlertHistoryViewModel(History); vm.Reload();
        File.WriteAllText(History.FilePath, "{"); vm.Reload();
        Assert.NotEmpty(vm.Error); Assert.Single(vm.Entries); Assert.Equal("{", File.ReadAllText(History.FilePath));
        History.Save([]); vm.Reload(); Assert.Empty(vm.Error); Assert.Empty(vm.Entries);
        Assert.Equal("尚無提醒紀錄", vm.EmptyText);
    }

    [Fact]
    public void EmptyFilterAndInvalidDatesAreDistinguishedFromEmptyFile()
    {
        History.Save([Fall()]);
        var vm = new AlertHistoryViewModel(History); vm.Reload(); vm.SymbolFilter = "ETHUSDT";
        Assert.Equal("沒有符合篩選的紀錄", vm.EmptyText);
        vm.StartDate = Time.Date.AddDays(1); vm.EndDate = Time.Date;
        Assert.NotEmpty(vm.Error);
    }

    [Fact]
    public void WaveNotificationExplainsBaselineThresholdAndActualChange()
    {
        string? message = null;
        new NotificationService((_, m) => message = m).Show(Fall());
        Assert.Contains("5 分鐘", message); Assert.Contains("2%", message);
        Assert.Contains("基準價格：100", message); Assert.Contains("目前價格：98", message);
        Assert.Contains("-2%", message);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
