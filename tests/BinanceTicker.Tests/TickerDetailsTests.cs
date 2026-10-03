using System.Reflection;
using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class TickerDetailsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParsesRestAndStreamDetailsWithoutBreakingOldPayloads(bool stream)
    {
        var json = stream
            ? """{"data":{"s":"BTCUSDT","c":"100.25","P":"2","E":1700000000000,"h":"120","l":"80","v":"42.5","q":"4500.75"}}"""
            : """{"symbol":"BTCUSDT","lastPrice":"100.25","priceChangePercent":"2","closeTime":1700000000000,"highPrice":"120","lowPrice":"80","volume":"42.5","quoteVolume":"4500.75"}""";
        using var doc = JsonDocument.Parse(json);
        var price = stream ? TickerParser.ParseStream(doc.RootElement) : TickerParser.ParseRest(doc.RootElement);
        Assert.Equal(100.25m, price.Price);
        Assert.Equal(120m, Detail(price, "HighPrice24h"));
        Assert.Equal(80m, Detail(price, "LowPrice24h"));
        Assert.Equal(42.5m, Detail(price, "Volume24h"));
        Assert.Equal(4500.75m, Detail(price, "QuoteVolume24h"));
        Assert.Null(Detail(new("BTCUSDT", 1m, 0m, DateTime.UnixEpoch), "Volume24h"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"bad\"")]
    [InlineData("\"-1\"")]
    [InlineData("12")]
    [InlineData("\"99999999999999999999999999999999999\"")]
    public void BadOptionalFieldsDoNotDiscardValidQuote(string invalid)
    {
        using var doc = JsonDocument.Parse("""{"s":"BTCUSDT","c":"100","P":"0","E":1700000000000,"h":""" + invalid + "}");
        var price = TickerParser.ParseStream(doc.RootElement);
        Assert.Equal(100m, price.Price);
        Assert.Null(Detail(price, "HighPrice24h"));
        Assert.Null(Detail(price, "QuoteVolume24h"));
    }

    [Fact]
    public void TradeLinkUsesValidatedSpotSymbol()
    {
        var type = typeof(TickerParser).Assembly.GetType("BinanceTicker.Core.Services.BinanceTradeLink");
        Assert.NotNull(type);
        var method = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var uri = Assert.IsType<Uri>(method.Invoke(null, ["BTCUSDT"]));
        Assert.Equal("https://www.binance.com/en/trade/BTC_USDT?type=spot", uri.AbsoluteUri);
        var invalid = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, ["https://example.com"]));
        Assert.IsType<ArgumentException>(invalid.InnerException);
    }

    private static decimal? Detail(TickerPrice value, string property)
    {
        var info = typeof(TickerPrice).GetProperty(property);
        Assert.NotNull(info);
        return (decimal?)info.GetValue(value);
    }
}
