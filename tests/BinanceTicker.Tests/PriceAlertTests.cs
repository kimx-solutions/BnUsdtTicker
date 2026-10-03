using System.Text.Json;
using BinanceTicker.Core.Models;
using BinanceTicker.Core.Services;

namespace BinanceTicker.Tests;

public sealed class PriceAlertTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "TickerAlerts-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AlertSettingsSurviveRoundTripAndDeepCopy()
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsService(Path.Combine(directory, "settings.json"));
        File.WriteAllText(store.FilePath, """
            {"symbols":[{"symbol":"BTCUSDT","enabled":true,"order":1,
              "alert":{"upperPrice":85000,"upperTriggered":true,"lowerPrice":80000,"lowerTriggered":false}}]}
            """);
        store.Save(store.Load().Copy());
        using var json = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        Assert.True(json.RootElement.GetProperty("symbols")[0].TryGetProperty("alert", out var alert));
        Assert.Equal(85000m, alert.GetProperty("upperPrice").GetDecimal());
        Assert.True(alert.GetProperty("upperTriggered").GetBoolean());
        Assert.Equal(80000m, alert.GetProperty("lowerPrice").GetDecimal());
        Assert.False(alert.GetProperty("lowerTriggered").GetBoolean());
        var loaded = store.Load();
        loaded.Copy().Symbols[0].Alert.UpperTriggered = false;
        Assert.True(loaded.Symbols[0].Alert.UpperTriggered);
    }

    [Fact]
    public async Task InclusiveLimitsTriggerOnceIndependentlyAndRecordActualPrices()
    {
        var (settings, store, history, notifications, service) = Create();
        await service.CheckAsync("BTCUSDT", 84999m);
        Assert.Empty(history.Load());
        await service.CheckAsync("BTCUSDT", 85000m);
        await service.CheckAsync("BTCUSDT", 87000m);
        Assert.True(settings.Symbols[0].Alert.UpperTriggered);
        Assert.False(settings.Symbols[0].Alert.LowerTriggered);
        await service.CheckAsync("BTCUSDT", 80000m);
        await service.CheckAsync("BTCUSDT", 79000m);
        Assert.Equal(2, notifications.Entries.Count);
        var entries = history.Load();
        Assert.Equal(new AlertHistoryEntry("BTCUSDT", AlertType.Upper, 85000m, 85000m,
            DateTimeOffset.Parse("2026-10-03T18:20:35+08:00")), entries[0]);
        Assert.Equal(AlertType.Lower, entries[1].AlertType);
        Assert.Equal(80000m, entries[1].TriggeredPrice);
        Assert.True(store.Load().Symbols[0].Alert.LowerTriggered);
    }

    [Fact]
    public async Task HighFrequencyConcurrentQuotesAndRestartDoNotRepeatNotifications()
    {
        var (_, store, history, notifications, service) = Create();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() => service.CheckAsync("BTCUSDT", 85120.5m))));
        Assert.Single(history.Load());
        Assert.Single(notifications.Entries);
        var restarted = new PriceAlertService(store.Load(), store, history, notifications);
        await restarted.CheckAsync("BTCUSDT", 86000m);
        Assert.Single(notifications.Entries);
    }

    [Theory]
    [InlineData(AlertType.Upper, 3, true, true)]
    [InlineData(AlertType.Lower, 3, true, true)]
    [InlineData(null, 4, true, true)]
    public async Task ResetOnlyRearmsRequestedConditions(AlertType? type, int count, bool upper, bool lower)
    {
        var (settings, store, history, notifications, service) = Create();
        await service.CheckAsync("BTCUSDT", 85000m);
        await service.CheckAsync("BTCUSDT", 80000m);
        await service.ResetAsync("BTCUSDT", type);
        var saved = store.Load().Symbols[0].Alert;
        Assert.Equal(type == AlertType.Lower, saved.UpperTriggered);
        Assert.Equal(type == AlertType.Upper, saved.LowerTriggered);
        await service.CheckAsync("BTCUSDT", 85000m);
        await service.CheckAsync("BTCUSDT", 80000m);
        Assert.Equal(count, notifications.Entries.Count);
        Assert.Equal(count, history.Load().Count);
        Assert.Equal(upper, settings.Symbols[0].Alert.UpperTriggered);
        Assert.Equal(lower, settings.Symbols[0].Alert.LowerTriggered);
    }

    [Fact]
    public async Task DisabledUnknownUnconfiguredAndInvalidQuotesDoNotTrigger()
    {
        var (settings, _, history, _, service) = Create();
        settings.Symbols[0].Enabled = false;
        await service.CheckAsync("BTCUSDT", 90000m);
        settings.Symbols[0].Enabled = true;
        await service.CheckAsync("BTCUSDT", 0m);
        await service.CheckAsync("BTCUSDT", -1m);
        await service.CheckAsync("ETHUSDT", 90000m);
        settings.Symbols[0].Alert = new();
        await service.CheckAsync("BTCUSDT", 90000m);
        Assert.Empty(history.Load());
    }

    [Fact]
    public async Task NotificationFailureRollsBackHistoryAndStateThenAllowsRetry()
    {
        var (settings, store, history, notifications, service) = Create();
        notifications.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CheckAsync("BTCUSDT", 85120.5m));
        Assert.False(settings.Symbols[0].Alert.UpperTriggered);
        Assert.False(store.Load().Symbols[0].Alert.UpperTriggered);
        Assert.Empty(history.Load());
        notifications.Fail = false;
        await service.CheckAsync("BTCUSDT", 85120.5m);
        Assert.Single(history.Load());
        Assert.Single(notifications.Entries);
    }

    [Fact]
    public async Task HistoryWriteFailureDoesNotSendOrConsumeTheCondition()
    {
        var (settings, store, history, notifications, service) = Create();
        Directory.CreateDirectory(history.FilePath);
        var failure = await Record.ExceptionAsync(() => service.CheckAsync("BTCUSDT", 90000m));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Empty(notifications.Entries);
        Assert.False(settings.Symbols[0].Alert.UpperTriggered);
        Assert.False(store.Load().Symbols[0].Alert.UpperTriggered);
    }

    [Fact]
    public async Task CorruptHistoryIsPreservedAndBlocksNotifications()
    {
        var (settings, _, history, notifications, service) = Create();
        File.WriteAllText(history.FilePath, "{");
        await Assert.ThrowsAsync<JsonException>(() => service.CheckAsync("BTCUSDT", 90000m));
        Assert.Empty(notifications.Entries);
        Assert.False(settings.Symbols[0].Alert.UpperTriggered);
        Assert.Equal("{", File.ReadAllText(history.FilePath));
    }

    [Fact]
    public void OldSettingsDefaultToUnconfiguredAlerts()
    {
        Directory.CreateDirectory(directory);
        var store = new SettingsService(Path.Combine(directory, "settings.json"));
        File.WriteAllText(store.FilePath, """{"symbols":[{"symbol":"BTCUSDT"}]}""");
        var alert = store.Load().Symbols[0].Alert;
        Assert.Null(alert.UpperPrice);
        Assert.Null(alert.LowerPrice);
        Assert.False(alert.UpperTriggered);
        Assert.False(alert.LowerTriggered);
    }

    [Fact]
    public async Task ApplyingStaleDraftPreservesLiveTriggersUntilExplicitReset()
    {
        var (settings, store, history, notifications, service) = Create();
        var staleDraft = settings.Copy();
        await service.CheckAsync("BTCUSDT", 85000m);
        staleDraft.Symbols[0].Alert.UpperPrice = 90000m;
        await service.ApplySettingsAsync(staleDraft, []);
        await service.CheckAsync("BTCUSDT", 95000m);
        Assert.Single(notifications.Entries);
        Assert.True(store.Load().Symbols[0].Alert.UpperTriggered);
        var resetDraft = staleDraft.Copy();
        await service.ApplySettingsAsync(resetDraft, [new("BTCUSDT", AlertType.Upper)]);
        await service.CheckAsync("BTCUSDT", 95120m);
        Assert.Equal(2, notifications.Entries.Count);
        Assert.Equal(90000m, history.Load()[1].TargetPrice);
    }

    [Fact]
    public async Task ConcurrentChecksAndResetsKeepBothJsonFilesReadable()
    {
        var (_, store, history, _, service) = Create();
        var checks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
        {
            if (i % 5 == 0) await service.ResetAsync("BTCUSDT", AlertType.Upper);
            await service.CheckAsync("BTCUSDT", 90000m);
            // Production readers share the file gate. A raw Windows File.ReadAllText holds
            // a handle without delete sharing and can intentionally block atomic replacement.
            _ = store.Load();
            _ = history.Load();
        }));
        await Task.WhenAll(checks);
        using var settingsJson = JsonDocument.Parse(File.ReadAllText(store.FilePath));
        using var historyJson = JsonDocument.Parse(File.ReadAllText(history.FilePath));
        Assert.InRange(history.Load().Count, 1, 11);
        Assert.True(store.Load().Symbols[0].Alert.UpperTriggered);
    }

    [Fact]
    public async Task EqualLimitsCanTriggerBothIndependentConditionsOnTheSameQuote()
    {
        var (settings, _, history, notifications, service) = Create();
        settings.Symbols[0].Alert.LowerPrice = 85000m;
        await service.CheckAsync("BTCUSDT", 85000m);
        Assert.Equal(new[] { AlertType.Upper, AlertType.Lower }, history.Load().Select(e => e.AlertType));
        Assert.Equal(2, notifications.Entries.Count);
    }

    [Fact]
    public async Task SettingsWriteFailureDoesNotSendOrCreateHistory()
    {
        var (settings, store, history, notifications, service) = Create();
        File.Delete(store.FilePath);
        Directory.CreateDirectory(store.FilePath);
        var failure = await Record.ExceptionAsync(() => service.CheckAsync("BTCUSDT", 90000m));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Empty(notifications.Entries);
        Assert.Empty(history.Load());
        Assert.False(settings.Symbols[0].Alert.UpperTriggered);
    }

    [Fact]
    public async Task FailedResetPreservesInMemoryTriggeredState()
    {
        var (settings, store, history, notifications, service) = Create();
        await service.CheckAsync("BTCUSDT", 90000m);
        File.Delete(store.FilePath);
        Directory.CreateDirectory(store.FilePath);
        var failure = await Record.ExceptionAsync(() => service.ResetAsync("BTCUSDT", AlertType.Upper));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.True(settings.Symbols[0].Alert.UpperTriggered);
        await service.CheckAsync("BTCUSDT", 95000m);
        Assert.Single(notifications.Entries);
        Assert.Single(history.Load());
    }

    [Fact]
    public async Task FailedSettingsReplacementKeepsCurrentLimitsActive()
    {
        var (settings, store, _, notifications, service) = Create();
        var draft = settings.Copy();
        draft.Symbols[0].Alert.UpperPrice = 95000m;
        File.Delete(store.FilePath);
        Directory.CreateDirectory(store.FilePath);
        var failure = await Record.ExceptionAsync(() => service.ApplySettingsAsync(draft, []));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Directory.Delete(store.FilePath);
        await service.CheckAsync("BTCUSDT", 90000m);
        Assert.Equal(85000m, Assert.Single(notifications.Entries).TargetPrice);
    }

    [Fact]
    public void HistoryJsonUsesIssueFieldNamesAndOffsetTimestamp()
    {
        Directory.CreateDirectory(directory);
        var history = new AlertHistoryService(Path.Combine(directory, "alerts-history.json"));
        history.Save([new("BTCUSDT", AlertType.Upper, 85000m, 85120.5m,
            DateTimeOffset.Parse("2026-10-03T18:20:35+08:00"))]);
        using var json = JsonDocument.Parse(File.ReadAllText(history.FilePath));
        var entry = json.RootElement[0];
        Assert.Equal("BTCUSDT", entry.GetProperty("symbol").GetString());
        Assert.Equal("Upper", entry.GetProperty("alertType").GetString());
        Assert.Equal(85000m, entry.GetProperty("targetPrice").GetDecimal());
        Assert.Equal(85120.5m, entry.GetProperty("triggeredPrice").GetDecimal());
        Assert.Equal("2026-10-03T18:20:35+08:00", entry.GetProperty("triggeredAt").GetString());
    }

    [Fact]
    public async Task FailedHistoryRollbackStillRestoresSettingsAndIsRecoveredBeforeRetry()
    {
        var (settings, store, history, notifications, service) = Create();
        notifications.Fail = true;
        notifications.OnFailure = () => File.SetAttributes(history.FilePath, FileAttributes.ReadOnly);
        try
        {
            await Assert.ThrowsAsync<AggregateException>(() => service.CheckAsync("BTCUSDT", 90000m));
            Assert.False(settings.Symbols[0].Alert.UpperTriggered);
            Assert.False(store.Load().Symbols[0].Alert.UpperTriggered);
            notifications.Fail = false;
            Assert.NotNull(await Record.ExceptionAsync(() => service.CheckAsync("BTCUSDT", 91000m)));
            Assert.Empty(notifications.Entries);
            await Assert.ThrowsAsync<AggregateException>(() => service.ResetAsync("BTCUSDT"));
            await Assert.ThrowsAsync<AggregateException>(() => service.ApplySettingsAsync(settings.Copy(), []));
            File.SetAttributes(history.FilePath, FileAttributes.Normal);
            await service.CheckAsync("BTCUSDT", 92000m);
            Assert.Equal(92000m, Assert.Single(history.Load()).TriggeredPrice);
            Assert.Single(notifications.Entries);
        }
        finally { if (File.Exists(history.FilePath)) File.SetAttributes(history.FilePath, FileAttributes.Normal); }
    }

    [Fact]
    public async Task IndependentSaveKeepsOtherSymbolsPreferencesAndLiveTriggers()
    {
        var (settings, store, _, _, service) = Create();
        settings.Symbols.Add(new() { Symbol = "ETHUSDT", Alert = new() { LowerPrice = 2000m } });
        settings.Window.Opacity = 0.73;
        var oldAlert = settings.Symbols[0].Alert.Copy();
        await service.CheckAsync("BTCUSDT", 90000m);
        oldAlert.UpperPrice = 95000m;
        await service.SaveAlertAsync("BTCUSDT", oldAlert, []);
        var saved = store.Load();
        Assert.Equal(0.73, saved.Window.Opacity);
        Assert.Equal(2000m, saved.Symbols[1].Alert.LowerPrice);
        Assert.Equal(95000m, saved.Symbols[0].Alert.UpperPrice);
        Assert.True(saved.Symbols[0].Alert.UpperTriggered);
        await service.SaveAlertAsync("BTCUSDT", oldAlert, [new("ETHUSDT", AlertType.Lower), new("BTCUSDT", AlertType.Upper)]);
        Assert.False(settings.Symbols[0].Alert.UpperTriggered);
    }

    [Fact]
    public async Task GeneralSettingsSavePreservesLatestIndependentAlertEdits()
    {
        var (settings, store, _, notifications, service) = Create();
        var staleGeneralSettings = settings.Copy();
        await service.SaveAlertAsync("BTCUSDT", new() { UpperPrice = 95000m }, []);
        await service.CheckAsync("BTCUSDT", 95000m);
        staleGeneralSettings.Window.Opacity = 0.6;
        await service.ApplySettingsAsync(staleGeneralSettings, [], preserveAlerts: true);
        var saved = store.Load();
        Assert.Equal(0.6, saved.Window.Opacity);
        Assert.Equal(95000m, saved.Symbols[0].Alert.UpperPrice);
        Assert.Null(saved.Symbols[0].Alert.LowerPrice);
        Assert.True(saved.Symbols[0].Alert.UpperTriggered);
        // Further independent saves must target the newly installed settings object.
        await service.SaveAlertAsync("BTCUSDT", new() { UpperPrice = 96000m }, [new("BTCUSDT", AlertType.Upper)]);
        await service.CheckAsync("BTCUSDT", 96000m);
        Assert.Equal(96000m, notifications.Entries[1].TargetPrice);
        Assert.Equal(96000m, store.Load().Symbols[0].Alert.UpperPrice);
    }

    [Fact]
    public async Task FailedIndependentSaveLeavesLiveAlertUnchanged()
    {
        var (settings, store, _, _, service) = Create();
        await service.CheckAsync("BTCUSDT", 90000m);
        File.Delete(store.FilePath);
        Directory.CreateDirectory(store.FilePath);
        var failure = await Record.ExceptionAsync(() => service.SaveAlertAsync("BTCUSDT", new() { UpperPrice = 95000m }, [new("BTCUSDT", AlertType.Upper)]));
        Assert.True(failure is IOException or UnauthorizedAccessException);
        Assert.Equal(85000m, settings.Symbols[0].Alert.UpperPrice);
        Assert.True(settings.Symbols[0].Alert.UpperTriggered);
    }

    [Fact]
    public async Task RemovedSymbolCannotBeResurrectedByOpenAlertEditor()
    {
        var (settings, store, _, _, service) = Create();
        var updated = settings.Copy();
        updated.Symbols.Clear();
        await service.ApplySettingsAsync(updated, [], preserveAlerts: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAlertAsync("BTCUSDT", new() { UpperPrice = 90000m }, []));
        Assert.Empty(store.Load().Symbols);
    }

    private (AppSettings, SettingsService, AlertHistoryService, RecordingNotifications, PriceAlertService) Create()
    {
        var settings = new AppSettings { Symbols = [new() { Symbol = "BTCUSDT", Alert = new() { UpperPrice = 85000m, LowerPrice = 80000m } }] };
        var store = new SettingsService(Path.Combine(directory, "settings.json"));
        store.Save(settings);
        var history = new AlertHistoryService(Path.Combine(directory, "alerts-history.json"));
        var notifications = new RecordingNotifications();
        var service = new PriceAlertService(settings, store, history, notifications,
            () => DateTimeOffset.Parse("2026-10-03T18:20:35+08:00"));
        return (settings, store, history, notifications, service);
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public bool Fail { get; set; }
        public Action? OnFailure { get; set; }
        public List<AlertHistoryEntry> Entries { get; } = [];
        public void Show(AlertHistoryEntry entry)
        {
            if (Fail) { OnFailure?.Invoke(); throw new InvalidOperationException("Notification unavailable"); }
            Entries.Add(entry);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
