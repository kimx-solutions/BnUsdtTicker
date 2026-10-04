using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class QuoteWindowTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
    private static TickerPrice Quote(DateTimeOffset at, decimal price = 100, string symbol = "BTCUSDT") =>
        new(symbol, price, 0, at.UtcDateTime, Source: QuoteSource.Stream);

    [Fact]
    public void CompleteWindowComparesAgainstExactBaselineWithoutRounding()
    {
        var window = new QuoteWindow();
        for (var i = 0; i <= 300; i++)
            Assert.True(window.Add(Quote(Start.AddSeconds(i), i == 300 ? 98 : 100), Start.AddSeconds(i), TimeSpan.FromMinutes(5)));
        var comparison = window.Compare("BTCUSDT", Start.AddMinutes(5), 5);
        Assert.NotNull(comparison);
        Assert.Equal(-2m, comparison.ChangePercent);
        Assert.Equal(100m, comparison.BaselinePrice);
        Assert.Equal(Start, comparison.BaselineAt);
    }

    [Theory]
    [InlineData(4999, true)]
    [InlineData(5001, false)]
    public void BaselineToleranceDoesNotUseLaterSamples(int milliseconds, bool valid)
    {
        var window = new QuoteWindow();
        var first = Start.AddMilliseconds(-milliseconds);
        window.Add(Quote(first), first, TimeSpan.FromMinutes(2));
        for (var i = 0; i < 60; i++) window.Add(Quote(Start.AddSeconds(i).AddMilliseconds(1)), Start.AddSeconds(i).AddMilliseconds(1), TimeSpan.FromMinutes(2));
        window.Add(Quote(Start.AddMinutes(1)), Start.AddMinutes(1), TimeSpan.FromMinutes(2));
        Assert.Equal(valid, window.Compare("BTCUSDT", Start.AddMinutes(1), 1) is not null);
    }

    [Fact]
    public void MissingDataClearsOnlyItsSymbolAndRequiresNewFullWindow()
    {
        var window = new QuoteWindow();
        for (var i = 0; i <= 60; i++)
        {
            window.Add(Quote(Start.AddSeconds(i)), Start.AddSeconds(i), TimeSpan.FromMinutes(2));
            window.Add(Quote(Start.AddSeconds(i), symbol: "ETHUSDT"), Start.AddSeconds(i), TimeSpan.FromMinutes(2));
        }
        window.Add(Quote(Start.AddSeconds(66)), Start.AddSeconds(66), TimeSpan.FromMinutes(2));
        Assert.Null(window.Compare("BTCUSDT", Start.AddSeconds(66), 1));
        Assert.NotNull(window.Compare("ETHUSDT", Start.AddSeconds(60), 1));
        window.Clear("ETHUSDT");
        Assert.Null(window.Compare("ETHUSDT", Start.AddSeconds(60), 1));
    }

    [Fact]
    public void RejectsRestInvalidStaleFutureDuplicateAndOutOfOrderQuotes()
    {
        var window = new QuoteWindow();
        var quote = Quote(Start);
        Assert.False(window.Add(quote with { Source = QuoteSource.Rest }, Start, TimeSpan.FromMinutes(1)));
        Assert.False(window.Add(quote with { Price = 0 }, Start, TimeSpan.FromMinutes(1)));
        Assert.False(window.Add(quote, Start.AddSeconds(6), TimeSpan.FromMinutes(1)));
        Assert.False(window.Add(quote, Start.AddSeconds(-6), TimeSpan.FromMinutes(1)));
        Assert.True(window.Add(quote, Start, TimeSpan.FromMinutes(1)));
        Assert.False(window.Add(quote, Start, TimeSpan.FromMinutes(1)));
        Assert.False(window.Add(Quote(Start.AddSeconds(-1)), Start, TimeSpan.FromMinutes(1)));
        Assert.Null(window.Compare("BTCUSDT", Start, 1));
    }
}
