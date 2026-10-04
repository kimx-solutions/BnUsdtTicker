using BinanceTicker.Core.Models;
using BinanceTicker.Core.ViewModels;

namespace BinanceTicker.Tests;

public sealed class AdvancedAlertEditorTests
{
    [Fact]
    public void ResetAllIncludesBothWaveConditionsAndDoesNotMutateOriginal()
    {
        var alert = new PriceAlertSettings { Rise = new() { ThresholdPercent = 2, Triggered = true }, Fall = new() { ThresholdPercent = 2, Triggered = true } };
        var vm = new PriceAlertEditorViewModel("BTCUSDT", alert);
        vm.ResetBothCommand.Execute(null);
        Assert.Equal(4, vm.GetAlertResets().Count);
        Assert.Contains(vm.GetAlertResets(), r => r.Type == AlertType.Rise);
        Assert.Contains(vm.GetAlertResets(), r => r.Type == AlertType.Fall);
        Assert.True(alert.Rise.Triggered); Assert.True(alert.Fall.Triggered);
    }

    [Fact]
    public void BellIncludesWaveConfigurationAndTriggeredSummary()
    {
        var row = new TickerRowViewModel("BTCUSDT");
        row.SetAlertState(new() { Rise = new() { ThresholdPercent = 2, Triggered = true } });
        Assert.True(row.HasConfiguredAlert); Assert.True(row.HasTriggeredAlert);
        Assert.Contains("短期上漲", row.AlertStatusText);
    }

    [Theory]
    [InlineData("0", "2", "0")]
    [InlineData("61", "2", "0")]
    [InlineData("5", "-2", "0")]
    [InlineData("5", "2", "1441")]
    public void InvalidWaveDraftIsRejected(string minutes, string threshold, string cooldown)
    {
        var vm = new PriceAlertEditorViewModel("BTCUSDT", new());
        vm.Rise.WindowMinutesText = minutes; vm.Rise.ThresholdText = threshold; vm.Rise.Policy.CooldownMinutesText = cooldown;
        Assert.Throws<ArgumentException>(() => vm.CreateAlert());
    }

    [Fact]
    public void RefreshPreservesUserParametersAndLatestCooldownState()
    {
        var vm = new PriceAlertEditorViewModel("BTCUSDT", new());
        vm.Rise.ThresholdText = "2.125"; vm.Rise.WindowMinutesText = "5"; vm.Rise.Policy.Strategy = AlertStrategy.Repeat;
        var time = new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);
        vm.RefreshState(new() { Rise = new() { Triggered = true, Policy = new() { LastTriggeredAt = time, Armed = false } } });
        var draft = vm.CreateAlert();
        Assert.Equal(2.125m, draft.Rise.ThresholdPercent); Assert.Equal(5, draft.Rise.WindowMinutes);
        Assert.Equal(AlertStrategy.Repeat, draft.Rise.Policy.Strategy); Assert.True(draft.Rise.Triggered);
        Assert.Equal(time, draft.Rise.Policy.LastTriggeredAt);
    }
}
