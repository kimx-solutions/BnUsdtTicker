using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class ViewModelTests
{
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
