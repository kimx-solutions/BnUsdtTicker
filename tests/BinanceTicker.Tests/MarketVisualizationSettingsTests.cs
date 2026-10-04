using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class MarketVisualizationSettingsTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"ui":{"sparklineRange":"unsupported"},"symbols":[{"symbol":"BTCUSDT","alert":{"upperPrice":100}}]}""")]
    public void OldAndUnknownRangesPreserveExistingSettings(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, json);
            var service = new SettingsService(path);
            var settings = service.Load();
            Assert.Null(service.LoadWarning);
            Assert.True(Get<bool>(settings.Ui, "ShowSparkline"));
            Assert.Equal("1h", Get<string>(settings.Ui, "SparklineRange"));
            if (json.Contains("upperPrice")) Assert.Equal(100m, settings.Symbols[0].Alert.UpperPrice);
            Set(settings.Ui, "ShowSparkline", false);
            Set(settings.Ui, "SparklineRange", "24h");
            service.Save(settings);
            Assert.False(Get<bool>(service.Load().Ui, "ShowSparkline"));
            Assert.Equal("24h", Get<string>(settings.Copy().Ui, "SparklineRange"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void StaleEditorPreservesUneditedPreferenceFields(bool editShow, bool editRange)
    {
        var initial = new AppSettings();
        using var editor = new SettingsViewModel(initial, new NoNetwork());
        if (editShow) { Set(editor, "ShowSparkline", false); Set(editor, "ShowSparkline", true); }
        if (editRange) { Set(editor, "SparklineRange", "24h"); Set(editor, "SparklineRange", "1h"); }
        var current = initial.Copy();
        Set(current.Ui, "ShowSparkline", false);
        Set(current.Ui, "SparklineRange", "24h");
        var edited = editor.CreateSettings();
        var method = typeof(SettingsViewModel).GetMethod("PreserveUneditedSparklinePreferences");
        Assert.NotNull(method);
        method.Invoke(editor, [edited, current]);
        Assert.Equal(editShow, Get<bool>(edited.Ui, "ShowSparkline"));
        Assert.Equal(editRange ? "1h" : "24h", Get<string>(edited.Ui, "SparklineRange"));
        Assert.True(Get<bool>(initial.Ui, "ShowSparkline"));
    }

    private static T Get<T>(object value, string name)
    {
        var property = value.GetType().GetProperty(name);
        Assert.NotNull(property);
        return (T)property.GetValue(value)!;
    }
    private static void Set(object value, string name, object setting)
    {
        var property = value.GetType().GetProperty(name);
        Assert.NotNull(property);
        property.SetValue(value, setting);
    }
    private sealed class NoNetwork : IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols, CancellationToken token) => throw new NotSupportedException();
    }
}
