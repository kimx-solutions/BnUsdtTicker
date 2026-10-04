using System.Text.Json;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class AdvancedAlertSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    [Fact]
    public void RepeatPolicySurvivesSettingsRoundTrip()
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsService(Path.Combine(directory, "settings.json"));
        File.WriteAllText(store.FilePath, """{"symbols":[{"symbol":"BTCUSDT","alert":{"upperPrice":100,"upperTriggered":true,"upperPolicy":{"strategy":"Repeat","cooldownMinutes":2,"lastTriggeredAt":"2026-10-04T00:00:00Z","armed":false}}}]}""");
        var settings = store.Load();
        Assert.True(settings.Symbols[0].Alert.UpperTriggered);
        store.Save(settings);
        using var json = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        var alert = json.RootElement.GetProperty("symbols")[0].GetProperty("alert");
        Assert.True(alert.TryGetProperty("upperPolicy", out var policy), "Repeat policy must not be lost when settings are saved.");
        Assert.Equal("Repeat", policy.GetProperty("strategy").GetString());
        Assert.Equal(2, policy.GetProperty("cooldownMinutes").GetInt32());
    }

    [Theory]
    [InlineData("\"rise\":{\"thresholdPercent\":2,\"windowMinutes\":0}")]
    [InlineData("\"rise\":{\"thresholdPercent\":2,\"windowMinutes\":61}")]
    [InlineData("\"rise\":{\"thresholdPercent\":0,\"windowMinutes\":5}")]
    [InlineData("\"upperPolicy\":{\"cooldownMinutes\":-1}")]
    [InlineData("\"upperPolicy\":{\"cooldownMinutes\":1441}")]
    public void InvalidNewSettingsAreBackedUp(string fields)
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsService(Path.Combine(directory, "settings.json"));
        var original = "{\"symbols\":[{\"symbol\":\"BTCUSDT\",\"alert\":{" + fields + "}}]}";
        File.WriteAllText(store.FilePath, original);
        store.Load();
        Assert.NotNull(store.LoadWarning);
        Assert.Equal(original, File.ReadAllText(store.FilePath + ".bak"));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
