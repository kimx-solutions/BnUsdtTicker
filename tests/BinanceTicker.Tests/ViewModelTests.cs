using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class ViewModelTests
{
    [Fact]
    public void ThemeChangesNotifyIconAndTooltipAndSurviveSettingsCopy()
    {
        var settings = new AppSettings { Ui = new() { Theme = ColorTheme.Light } };
        var vm = new TickerViewModel();
        vm.Configure(settings);
        Assert.True(vm.IsLightTheme);
        Assert.Equal("切換為深色模式", vm.ThemeToggleText);
        var notified = new List<string?>();
        vm.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        vm.SetTheme(ColorTheme.Dark);
        Assert.False(vm.IsLightTheme);
        Assert.Equal("切換為淺色模式", vm.ThemeToggleText);
        Assert.Contains(nameof(vm.IsLightTheme), notified);
        Assert.Contains(nameof(vm.ThemeToggleText), notified);
        using var editor = new SettingsViewModel(settings, new ValidationService());
        Assert.Equal(ColorTheme.Light, editor.CreateSettings().Ui.Theme);
    }

    [Theory]
    [InlineData(TickerSortColumn.Symbol, "BTC,ENA,ETH", "ETH,ENA,BTC")]
    [InlineData(TickerSortColumn.Price, "ETH,BTC,ENA", "ENA,BTC,ETH")]
    [InlineData(TickerSortColumn.ChangePercent, "ENA,BTC,ETH", "ETH,BTC,ENA")]
    public void TickerSortsColumnsInBothDirectionsUsingNumericValues(TickerSortColumn column, string ascending, string descending)
    {
        var vm = CreateSortingTicker();
        vm.SortBy(column);
        Assert.Equal(ascending, string.Join(",", vm.Prices.Select(p => p.Asset)));
        vm.SortBy(column);
        Assert.Equal(descending, string.Join(",", vm.Prices.Select(p => p.Asset)));
    }

    [Fact]
    public void SwitchingSortColumnsStartsAscending()
    {
        var vm = CreateSortingTicker();
        vm.SortBy(TickerSortColumn.Price);
        vm.SortBy(TickerSortColumn.Price);
        vm.SortBy(TickerSortColumn.ChangePercent);
        Assert.Equal(new[] { "ENA", "BTC", "ETH" }, vm.Prices.Select(p => p.Asset));
    }

    [Fact]
    public void LiveSortKeepsMissingQuotesLastAndPreservesRows()
    {
        var vm = CreateSortingTicker(includeWaiting: true);
        var ena = vm.Prices.Single(p => p.Asset == "ENA");
        vm.SortBy(TickerSortColumn.Price);
        Assert.Equal(new[] { "ETH", "BTC", "ENA", "NEAR" }, vm.Prices.Select(p => p.Asset));
        vm.SortBy(TickerSortColumn.Price);
        Assert.Equal(new[] { "ENA", "BTC", "ETH", "NEAR" }, vm.Prices.Select(p => p.Asset));
        vm.Update(new("NEARUSDT", 20m, 3m, DateTime.UnixEpoch.AddSeconds(1)));
        Assert.Equal(new[] { "ENA", "NEAR", "BTC", "ETH" }, vm.Prices.Select(p => p.Asset));
        vm.Update(new("ENAUSDT", 1m, -10m, DateTime.UnixEpoch.AddSeconds(1)));
        Assert.Equal(new[] { "NEAR", "BTC", "ETH", "ENA" }, vm.Prices.Select(p => p.Asset));
        Assert.Same(ena, vm.Prices[^1]);
        vm.Update(new("ENAUSDT", 9000m, -10m, DateTime.UnixEpoch));
        Assert.Equal(new[] { "NEAR", "BTC", "ETH", "ENA" }, vm.Prices.Select(p => p.Asset));
    }

    [Fact]
    public void EqualValuesUseStableSymbolOrderAndActiveSortSurvivesSettingsChanges()
    {
        var vm = CreateSortingTicker();
        vm.Update(new("ETHUSDT", 10m, 10m, DateTime.UnixEpoch.AddSeconds(1)));
        vm.SortBy(TickerSortColumn.Price);
        Assert.Equal(new[] { "BTC", "ETH", "ENA" }, vm.Prices.Select(p => p.Asset));
        vm.SortBy(TickerSortColumn.Price);
        Assert.Equal(new[] { "ENA", "BTC", "ETH" }, vm.Prices.Select(p => p.Asset));
        var btc = vm.Prices.Single(p => p.Asset == "BTC");
        vm.Configure(new AppSettings { Symbols = [
            new() { Symbol = "ETHUSDT", Order = 1 },
            new() { Symbol = "BTCUSDT", Order = 2 },
            new() { Symbol = "BNBUSDT", Order = 3 }] });
        Assert.Equal(new[] { "BTC", "ETH", "BNB" }, vm.Prices.Select(p => p.Asset));
        Assert.Same(btc, vm.Prices[0]);
    }

    private static TickerViewModel CreateSortingTicker(bool includeWaiting = false)
    {
        var settings = new AppSettings { Symbols = [
            new() { Symbol = "ETHUSDT", Order = 1 },
            new() { Symbol = "ENAUSDT", Order = 2 },
            new() { Symbol = "BTCUSDT", Order = 3 }] };
        if (includeWaiting) settings.Symbols.Insert(0, new() { Symbol = "NEARUSDT", Order = 0 });
        var vm = new TickerViewModel();
        vm.Configure(settings);
        vm.Update(new("BTCUSDT", 10m, -2m, DateTime.UnixEpoch));
        vm.Update(new("ETHUSDT", 2m, 10m, DateTime.UnixEpoch));
        vm.Update(new("ENAUSDT", 1000m, -10m, DateTime.UnixEpoch));
        return vm;
    }

    [Fact]
    public void TickerShowsEnabledRowsInOrderAndNotifiesOnPriceChanges()
    {
        var vm = new TickerViewModel();
        vm.Configure(new AppSettings { Symbols = [
            new() { Symbol = "ETHUSDT", Order = 2 },
            new() { Symbol = "ENAUSDT", Enabled = false, Order = 3 },
            new() { Symbol = "BTCUSDT", Order = 1 }] });
        Assert.Equal(new[] { "BTC", "ETH" }, vm.Prices.Select(p => p.Asset));
        var row = vm.Prices[0];
        var notified = new List<string?>();
        row.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        vm.Update(new("BTCUSDT", 82351.2m, 2.31m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("82,351.20", row.PriceText);
        Assert.Equal("+2.31%", row.ChangeText);
        Assert.Contains(nameof(row.PriceText), notified);
        vm.SetStatus(ConnectionStatus.Disconnected);
        Assert.Equal("82,351.20", row.PriceText);
        vm.Update(new("BTCUSDT", 1, 0, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("82,351.20", row.PriceText);
    }

    [Fact]
    public async Task SettingsAddsValidatedSymbolRejectsDuplicateAndSupportsReorderAndRemove()
    {
        var source = new AppSettings();
        var vm = new SettingsViewModel(source, new ValidationService());
        vm.NewSymbol = " near ";
        await vm.AddAsync();
        Assert.Equal("NEARUSDT", vm.Symbols[^1].Symbol);
        Assert.Equal(3, source.Symbols.Count);
        vm.NewSymbol = "BTC";
        await vm.AddAsync();
        Assert.Equal(4, vm.Symbols.Count);
        Assert.NotEmpty(vm.Error);
        vm.SelectedSymbol = vm.Symbols[^1];
        vm.MoveUpCommand.Execute(null);
        Assert.Equal("NEARUSDT", vm.Symbols[2].Symbol);
        vm.Symbols[2].Enabled = false;
        var saved = vm.CreateSettings();
        Assert.False(saved.Symbols[2].Enabled);
        Assert.Equal(new[] { 1, 2, 3, 4 }, saved.Symbols.Select(s => s.Order));
        vm.RemoveCommand.Execute(null);
        Assert.Equal(3, vm.Symbols.Count);
    }

    [Fact]
    public async Task InvalidOrOfflineSymbolDoesNotModifyWatchlist()
    {
        var vm = new SettingsViewModel(new(), new ValidationService());
        vm.NewSymbol = "NOPE";
        await vm.AddAsync();
        Assert.Equal(3, vm.Symbols.Count);
        Assert.NotEmpty(vm.Error);
        vm.NewSymbol = "OFFLINE";
        await vm.AddAsync();
        Assert.Equal(3, vm.Symbols.Count);
        Assert.False(vm.IsBusy);
        Assert.NotEmpty(vm.Error);
    }

    [Theory]
    [InlineData(-1800, 100, -1920, 0, 1920, 1080, true)]
    [InlineData(5000, 100, 0, 0, 1920, 1080, false)]
    [InlineData(1910, 100, 0, 0, 1920, 1080, false)]
    public void WindowMustFitARealWorkArea(double x, double y, double screenX, double screenY, double w, double h, bool expected) =>
        Assert.Equal(expected, WindowPlacement.IsVisible(new(x, y, 360, 200), [new(screenX, screenY, w, h)]));

    private sealed class ValidationService : IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol, CancellationToken cancellationToken) =>
            symbol == "OFFLINEUSDT" ? throw new HttpRequestException("offline") : Task.FromResult(symbol != "NOPEUSDT");
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
