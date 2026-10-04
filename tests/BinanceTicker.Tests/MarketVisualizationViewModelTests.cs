using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;
namespace BinanceTicker.Tests;
public sealed class MarketVisualizationViewModelTests
{
    [Fact]
    public void RangeSwitchReusesRowsAndSnapshots()
    {
        var vm = new TickerViewModel(); vm.Configure(new AppSettings());
        var row = vm.Prices[0]; var changes = 0;
        var evt = vm.GetType().GetEvent("SparklinePreferencesChanged"); Assert.NotNull(evt);
        evt.AddEventHandler(vm, (Action)(() => changes++));
        dynamic v = vm; v.SelectDayCommand.Execute(null);
        Assert.Equal("24h", (string)v.SparklineRange); Assert.Same(row, vm.Prices[0]);
        v.ToggleSparklineCommand.Execute(null); Assert.False((bool)v.ShowSparkline); Assert.Equal(2, changes);
    }
    [Fact]
    public void HistoryFailureDoesNotDiscardQuote()
    {
        var row = new TickerRowViewModel("BTCUSDT"); row.Update(new("BTCUSDT", 100, 10, CandleFixtures.Now.UtcDateTime));
        Update(row, [], new(HistoryLoadStatus.Failed), CandleFixtures.Now);
        dynamic r = row; Assert.Equal(100, row.Price); Assert.Contains("失敗", (string)r.HistoryStatusText);
    }
    [Fact]
    public void StaleQuoteAndHistoryStatusesAreIndependent()
    {
        var row = new TickerRowViewModel("BTCUSDT"); row.Update(new("BTCUSDT", 100, 10, CandleFixtures.Now.AddSeconds(-61).UtcDateTime));
        Update(row, [CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1))], new(HistoryLoadStatus.Loaded), CandleFixtures.Now);
        dynamic r = row; Assert.Contains("逾期", (string)r.QuoteStatusText);
        Assert.DoesNotContain("逾期", (string)r.HistoryStatusText);
    }
    [Fact]
    public void SelectedRangeTrendIsIndependentOf24hChange()
    {
        var row = new TickerRowViewModel("BTCUSDT"); row.Update(new("BTCUSDT", 100, -10, CandleFixtures.Now.UtcDateTime));
        Update(row, [CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-2), 90), CandleFixtures.Minute(CandleFixtures.Now.AddMinutes(-1), 100)], new(HistoryLoadStatus.Loaded), CandleFixtures.Now);
        dynamic r = row; Assert.False(row.IsPositive); Assert.True((bool)r.Series.IsPositive);
    }
    internal static void Update(TickerRowViewModel row, IReadOnlyList<CandlePrice> candles, HistoryLoadState state, DateTimeOffset now)
    {
        var method = row.GetType().GetMethod("UpdateHistory"); Assert.NotNull(method);
        method.Invoke(row, [candles, state, ConnectionStatus.Connected, "1h", now]);
    }
}
public sealed class MarketDetailsViewModelTests
{
    [Fact]
    public void DetailsShareRowAndFormatUnits()
    {
        var row = new TickerRowViewModel("BTCUSDT"); row.Update(new("BTCUSDT",100,1,CandleFixtures.Now.UtcDateTime,110,90,12,1200));
        Uri? opened = null; using var disposable = (IDisposable)Create(row, uri => opened=uri);
        dynamic vm = disposable; Assert.Same(row, (object)vm.Row);
        Assert.Contains("BTC", (string)vm.Row.VolumeText); Assert.Contains("USDT", (string)vm.Row.QuoteVolumeText);
        vm.OpenTradeCommand.Execute(null); Assert.Contains("/BTC_USDT?", opened!.AbsoluteUri);
        row.Update(new("BTCUSDT",101,2,CandleFixtures.Now.AddSeconds(1).UtcDateTime));
        Assert.Equal("—", (string)vm.Row.HighPriceText);
    }
    [Fact]
    public void BrowserFailureLeavesDetailsUsable()
    {
        var row = new TickerRowViewModel("BTCUSDT"); row.Update(new("BTCUSDT",100,1,CandleFixtures.Now.UtcDateTime));
        using var disposable = (IDisposable)Create(row, _ => throw new System.ComponentModel.Win32Exception("browser"));
        dynamic vm = disposable; vm.OpenTradeCommand.Execute(null);
        Assert.NotEmpty((string)vm.Error); Assert.Equal(100, row.Price);
    }
    private static object Create(TickerRowViewModel row, Action<Uri> browser)
    {
        var type = typeof(TickerViewModel).Assembly.GetType("BinanceTicker.Core.ViewModels.MarketDetailsViewModel"); Assert.NotNull(type);
        return Activator.CreateInstance(type, row, (Func<Task>)(() => Task.CompletedTask), browser)!;
    }
}
