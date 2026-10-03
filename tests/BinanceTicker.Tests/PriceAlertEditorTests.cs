using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class PriceAlertEditorTests
{
    [Theory]
    [InlineData("90000.125", "0.00001234", 90000.125, 0.00001234)]
    [InlineData("", "", null, null)]
    public void ParsesDecimalLimitsAndEmptyDisables(string upper, string lower, double? expectedUpper, double? expectedLower)
    {
        var editor = new PriceAlertEditorViewModel("BTCUSDT", new());
        editor.UpperPriceText = upper;
        editor.LowerPriceText = lower;
        var draft = editor.CreateAlert();
        Assert.Equal(expectedUpper is null ? null : (decimal?)expectedUpper.Value, draft.UpperPrice);
        Assert.Equal(expectedLower is null ? null : (decimal?)expectedLower.Value, draft.LowerPrice);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0")]
    [InlineData("abc")]
    [InlineData("1,234")]
    [InlineData("9999999999999999999999999999999999")]
    public void RejectsInvalidPrices(string value)
    {
        var editor = new PriceAlertEditorViewModel("BTCUSDT", new()) { UpperPriceText = value };
        Assert.Throws<ArgumentException>(() => editor.CreateAlert());
        editor.UpperPriceText = "";
        editor.LowerPriceText = value;
        Assert.Throws<ArgumentException>(() => editor.CreateAlert());
    }

    [Fact]
    public void ResetIsPerConditionAndCancelLeavesLiveStateAlone()
    {
        var live = new PriceAlertSettings { UpperPrice = 85000m, LowerPrice = 80000m, UpperTriggered = true, LowerTriggered = true };
        var editor = new PriceAlertEditorViewModel("BTCUSDT", live);
        editor.ResetUpperCommand.Execute(null);
        Assert.False(editor.CreateAlert().UpperTriggered);
        Assert.True(editor.CreateAlert().LowerTriggered);
        Assert.True(live.UpperTriggered);
        Assert.True(editor.ResetUpperRequested);
        Assert.False(editor.ResetLowerRequested);
        editor.RefreshState(live);
        Assert.False(editor.CreateAlert().UpperTriggered);
        editor.ResetBothCommand.Execute(null);
        Assert.False(editor.CreateAlert().LowerTriggered);
        Assert.True(editor.ResetLowerRequested);
    }

    [Fact]
    public void RefreshUpdatesTriggeredStatusWithoutOverwritingEditedThresholds()
    {
        var editor = new PriceAlertEditorViewModel("BTCUSDT", new()) { UpperPriceText = "90000" };
        editor.RefreshState(new() { UpperPrice = 85000m, UpperTriggered = true });
        Assert.True(editor.CreateAlert().UpperTriggered);
        Assert.Equal(90000m, editor.CreateAlert().UpperPrice);
        editor.ResetLowerCommand.Execute(null);
        Assert.True(editor.ResetLowerRequested);
        Assert.False(editor.CreateAlert().LowerTriggered);
    }

    [Fact]
    public void SettingsDraftPreservesAlertsWithoutMutatingOriginal()
    {
        var settings = CreateSettings();
        using var editor = new SettingsViewModel(settings, new NoNetwork());
        var draft = editor.CreateSettings();
        Assert.Equal(85000m, draft.Symbols[0].Alert.UpperPrice);
        Assert.True(draft.Symbols[0].Alert.UpperTriggered);
        draft.Symbols[0].Alert.UpperPrice = 90000m;
        Assert.Equal(85000m, settings.Symbols[0].Alert.UpperPrice);
    }

    [Fact]
    public void SwitchingSymbolsRetainsDraftsAndValidatesAllOfThem()
    {
        using var editor = new SettingsViewModel(CreateSettings(), new NoNetwork());
        editor.SelectedAlert!.UpperPriceText = "invalid";
        editor.SelectedSymbol = editor.Symbols[1];
        editor.SelectedAlert!.LowerPriceText = "2950.2";
        Assert.Throws<ArgumentException>(() => editor.CreateSettings());
        editor.SelectedSymbol = editor.Symbols[0];
        editor.SelectedAlert!.UpperPriceText = "90000";
        var draft = editor.CreateSettings();
        Assert.Equal(90000m, draft.Symbols[0].Alert.UpperPrice);
        Assert.True(draft.Symbols[0].Alert.UpperTriggered);
        Assert.Equal(2950.2m, draft.Symbols[1].Alert.LowerPrice);
    }

    [Fact]
    public void GlobalResetRearmsAllDraftsWithoutChangingLiveSettings()
    {
        var settings = CreateSettings();
        using var editor = new SettingsViewModel(settings, new NoNetwork());
        editor.ResetAllAlertsCommand.Execute(null);
        Assert.Equal(4, editor.GetAlertResets().Count);
        Assert.All(editor.CreateSettings().Symbols, s =>
        {
            Assert.False(s.Alert.UpperTriggered);
            Assert.False(s.Alert.LowerTriggered);
        });
        Assert.All(settings.Symbols, s => Assert.True(s.Alert.UpperTriggered));
    }

    private static AppSettings CreateSettings() => new()
    {
        Symbols = [new() { Symbol = "BTCUSDT", Alert = new() { UpperPrice = 85000m, LowerPrice = 80000m, UpperTriggered = true, LowerTriggered = true } },
                   new() { Symbol = "ETHUSDT", Alert = new() { UpperPrice = 3000m, UpperTriggered = true } }]
    };

    private sealed class NoNetwork : IBinanceService
    {
        public Task<bool> IsValidSymbolAsync(string symbol, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<TickerPrice>> GetPricesAsync(IReadOnlyList<string> symbols, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
