using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public interface INotificationService
{
    void Show(AlertHistoryEntry entry);
}

public interface IPriceAlertService
{
    Task CheckAsync(string symbol, decimal currentPrice);
    Task ResetAsync(string symbol, AlertType? type = null);
    Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets);
}

public sealed class PriceAlertService(AppSettings settings, SettingsService store,
    AlertHistoryService history, INotificationService notifications,
    Func<DateTimeOffset>? clock = null) : IPriceAlertService
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<AlertHistoryEntry>? pendingHistoryRollback;
    private bool pendingSettingsRollback;

    public async Task CheckAsync(string symbol, decimal currentPrice)
    {
        if (currentPrice <= 0) return;
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            var item = settings.Symbols.FirstOrDefault(s => s.Enabled && s.Symbol == symbol);
            if (item is null) return;
            var alert = item.Alert;
            if (alert.UpperPrice is > 0 && currentPrice >= alert.UpperPrice && !alert.UpperTriggered)
                Trigger(item, AlertType.Upper, alert.UpperPrice.Value, currentPrice);
            if (alert.LowerPrice is > 0 && currentPrice <= alert.LowerPrice && !alert.LowerTriggered)
                Trigger(item, AlertType.Lower, alert.LowerPrice.Value, currentPrice);
        }
        finally { gate.Release(); }
    }

    private void Trigger(SymbolSetting item, AlertType type, decimal target, decimal price)
    {
        var previousHistory = history.Load();
        var entry = new AlertHistoryEntry(item.Symbol, type, target, price, clock?.Invoke() ?? DateTimeOffset.Now);
        var stateSaved = false;
        var historySaved = false;
        SetTriggered(item.Alert, type, true);
        try
        {
            // Commit durable state before submitting to the shell so subsequent quotes cannot repeat it.
            store.Save(settings);
            stateSaved = true;
            history.Save([.. previousHistory, entry]);
            historySaved = true;
            notifications.Show(entry);
        }
        catch (Exception failure)
        {
            SetTriggered(item.Alert, type, false);
            pendingHistoryRollback = historySaved ? previousHistory : null;
            pendingSettingsRollback = stateSaved;
            var rollbackFailures = RestorePendingWrites();
            if (rollbackFailures.Count > 0)
                throw new AggregateException("提醒提交失敗，且尚未還原檔案；恢復寫入權限後將自動還原，再恢復提醒。", [failure, .. rollbackFailures]);
            throw;
        }
    }

    private List<Exception> RestorePendingWrites()
    {
        var failures = new List<Exception>();
        if (pendingHistoryRollback is not null)
        {
            try { history.Save(pendingHistoryRollback); pendingHistoryRollback = null; }
            catch (Exception ex) { failures.Add(ex); }
        }
        // A failed history restoration must not skip an independently writable settings file.
        if (pendingSettingsRollback)
        {
            try { store.Save(settings); pendingSettingsRollback = false; }
            catch (Exception ex) { failures.Add(ex); }
        }
        return failures;
    }

    private void EnsureRecovered()
    {
        var failures = RestorePendingWrites();
        if (failures.Count > 0)
            throw new AggregateException("尚未還原上次失敗的提醒；請恢復設定與紀錄檔的寫入權限。", failures);
    }

    public async Task ResetAsync(string symbol, AlertType? type = null)
    {
        if (type is not null && !Enum.IsDefined(type.Value)) throw new ArgumentOutOfRangeException(nameof(type));
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            var item = settings.Symbols.FirstOrDefault(s => s.Symbol == symbol);
            if (item is null) return;
            var previous = item.Alert.Copy();
            if (type is null or AlertType.Upper) item.Alert.UpperTriggered = false;
            if (type is null or AlertType.Lower) item.Alert.LowerTriggered = false;
            try { store.Save(settings); }
            catch { item.Alert = previous; throw; }
        }
        finally { gate.Release(); }
    }

    private static void SetTriggered(PriceAlertSettings alert, AlertType type, bool value)
    {
        if (type == AlertType.Upper) alert.UpperTriggered = value;
        else alert.LowerTriggered = value;
    }

    public async Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets)
    {
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            // Drafts may predate a notification. Only explicit reset requests can rearm live conditions.
            foreach (var item in updated.Symbols)
            {
                var live = settings.Symbols.FirstOrDefault(s => s.Symbol == item.Symbol)?.Alert;
                item.Alert.UpperTriggered = live?.UpperTriggered ?? false;
                item.Alert.LowerTriggered = live?.LowerTriggered ?? false;
                foreach (var request in resets.Where(r => r.Symbol == item.Symbol))
                    SetTriggered(item.Alert, request.Type, false);
            }
            store.Save(updated);
            settings = updated;
        }
        finally { gate.Release(); }
    }
}
