using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class CoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    [Theory]
    [InlineData(" btc ", "BTCUSDT")]
    [InlineData("ena/usdt", "ENAUSDT")]
    [InlineData("ETHUSDT", "ETHUSDT")]
    [InlineData("1inch", "1INCHUSDT")]
    public void NormalizesBaseOrUsdtPair(string input, string expected) =>
        Assert.Equal(expected, SymbolNormalizer.Normalize(input));

    [Theory]
    [InlineData("")]
    [InlineData("BTC/ETH")]
    [InlineData("BTC?x=1")]
    [InlineData("USDT")]
    public void RejectsInvalidSymbols(string input) =>
        Assert.Throws<ArgumentException>(() => SymbolNormalizer.Normalize(input));

    [Theory]
    [InlineData("82351.2", "82,351.20")]
    [InlineData("1.23456", "1.2346")]
    [InlineData("0.582", "0.58200")]
    [InlineData("0.00001234", "0.00001234")]
    public void FormatsPricesWithoutLosingSmallValues(string input, string expected) =>
        Assert.Equal(expected, PriceFormatter.Format(decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void ExistingSettingsWithoutThemeKeepDarkDefault()
    {
        Directory.CreateDirectory(directory);
        var service = new SettingsService(Path.Combine(directory, "settings.json"));
        File.WriteAllText(service.FilePath, "{\"ui\":{\"compactMode\":false}}");
        Assert.Equal(ColorTheme.Dark, service.Load().Ui.Theme);
        Assert.Null(service.LoadWarning);
    }

    [Fact]
    public void LightThemeSurvivesSettingsRoundTrip()
    {
        Directory.CreateDirectory(directory);
        var service = new SettingsService(Path.Combine(directory, "settings.json"));
        File.WriteAllText(service.FilePath, "{\"ui\":{\"theme\":\"Light\"}}");
        service.Save(service.Load());
        using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(service.FilePath));
        Assert.Equal("Light", json.RootElement.GetProperty("ui").GetProperty("theme").GetString());
    }

    [Fact]
    public void SavesAndLoadsEverySetting()
    {
        var service = new SettingsService(Path.Combine(directory, "settings.json"));
        var settings = new AppSettings { Mode = DisplayMode.Float, ShowOnStartup = false };
        settings.Window.Left = -500;
        settings.Window.Opacity = 0.7;
        settings.Ui.CompactMode = false;
        settings.Symbols = [new() { Symbol = "NEARUSDT", Enabled = false, Order = 1 }];
        service.Save(settings);
        var loaded = service.Load();
        Assert.Equal(DisplayMode.Float, loaded.Mode);
        Assert.False(loaded.ShowOnStartup);
        Assert.Equal(-500, loaded.Window.Left);
        Assert.Equal(0.7, loaded.Window.Opacity);
        Assert.False(loaded.Ui.CompactMode);
        Assert.False(Assert.Single(loaded.Symbols).Enabled);
        Assert.Contains("\"mode\": \"Float\"", File.ReadAllText(service.FilePath));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"window\":null,\"symbols\":null}")]
    [InlineData("{\"mode\":\"Unknown\"}")]
    [InlineData("{\"ui\":{\"theme\":\"Unknown\"}}")]
    public void BacksUpCorruptSettingsAndRestoresDefaults(string json)
    {
        Directory.CreateDirectory(directory);
        var service = new SettingsService(Path.Combine(directory, "settings.json"));
        File.WriteAllText(service.FilePath, json);
        var loaded = service.Load();
        Assert.Equal("BTCUSDT", loaded.Symbols[0].Symbol);
        Assert.Equal(json, File.ReadAllText(service.FilePath + ".bak"));
        Assert.NotNull(service.LoadWarning);
        Assert.NotEqual(json, File.ReadAllText(service.FilePath));
    }

    [Fact]
    public void EmptySymbolsStayEmptyAndInvalidValuesAreSanitized()
    {
        var service = new SettingsService(Path.Combine(directory, "settings.json"));
        service.Save(new AppSettings { Symbols = [], Window = new() { Opacity = 9 } });
        var settings = service.Load();
        Assert.Empty(settings.Symbols);
        Assert.Equal(1, settings.Window.Opacity);
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
