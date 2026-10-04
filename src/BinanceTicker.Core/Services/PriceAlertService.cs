using BinanceTicker.Core.Models;

namespace BinanceTicker.Core.Services;

public interface INotificationService { void Show(AlertHistoryEntry entry); }
public interface IPriceAlertService
{
    Task CheckAsync(string symbol, decimal currentPrice);
    Task CheckQuoteAsync(TickerPrice quote) => CheckAsync(quote.Symbol, quote.Price);
    Task OnConnectionStatusAsync(ConnectionStatus status) => Task.CompletedTask;
    Task ResetAsync(string symbol, AlertType? type = null);
    Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets, bool preserveAlerts = false);
    Task SaveAlertAsync(string symbol, PriceAlertSettings updated, IReadOnlyList<AlertResetRequest> resets);
}

public sealed class PriceAlertService : IPriceAlertService
{
    private AppSettings settings;
    private readonly SettingsService store;
    private readonly AlertHistoryService history;
    private readonly INotificationService notifications;
    private readonly Func<DateTimeOffset> clock;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly QuoteWindow quotes = new();
    private readonly Dictionary<string, DateTimeOffset> lastQuote = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, AlertType), DateTimeOffset> starts = [];
    private IReadOnlyList<AlertHistoryEntry>? pendingHistoryRollback;
    private bool pendingSettingsRollback;
    public event Action<AlertHistoryEntry>? Submitted;

    public PriceAlertService(AppSettings settings, SettingsService store, AlertHistoryService history,
        INotificationService notifications, Func<DateTimeOffset>? clock = null)
    {
        this.settings = settings; this.store = store; this.history = history; this.notifications = notifications;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        foreach (var item in settings.Symbols) DisarmPreviouslyTriggered(item.Alert);
    }

    public async Task CheckAsync(string symbol, decimal currentPrice)
    {
        if (currentPrice <= 0) return;
        var submitted = new List<AlertHistoryEntry>();
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            var item = settings.Symbols.FirstOrDefault(s => s.Enabled && s.Symbol == symbol);
            if (item is not null) EvaluatePrices(item, currentPrice, clock(), submitted);
        }
        finally { gate.Release(); Publish(submitted); }
    }

    public async Task CheckQuoteAsync(TickerPrice quote)
    {
        if (quote.Price <= 0) return;
        var submitted = new List<AlertHistoryEntry>();
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            var item = settings.Symbols.FirstOrDefault(s => s.Enabled && s.Symbol == quote.Symbol);
            if (item is null) return;
            var now = clock();
            var at = new DateTimeOffset(quote.UpdatedAt.ToUniversalTime());
            if (quote.UpdatedAt == default || (now - at).Duration() > TimeSpan.FromSeconds(5)) return;
            if (lastQuote.TryGetValue(item.Symbol, out var previous))
            {
                if (at <= previous) return;
                if (at - previous > TimeSpan.FromSeconds(5)) ResetContinuity(item);
            }
            lastQuote[item.Symbol] = at;
            var lengths = new[] { item.Alert.Rise, item.Alert.Fall }.Where(c => c.ThresholdPercent is > 0)
                .Select(c => c.WindowMinutes).ToArray();
            // Valid market observations are independent of persistence/shell submission failures.
            var sampled = lengths.Length > 0 && quotes.Add(quote, now, TimeSpan.FromMinutes(lengths.Max()));
            if (lengths.Length == 0) quotes.Clear(item.Symbol);
            EvaluatePrices(item, quote.Price, now, submitted);
            if (!sampled) return;
            foreach (var type in new[] { AlertType.Rise, AlertType.Fall })
            {
                var condition = type == AlertType.Rise ? item.Alert.Rise : item.Alert.Fall;
                if (condition.ThresholdPercent is not > 0) continue;
                var comparison = quotes.Compare(item.Symbol, at, condition.WindowMinutes);
                condition.ComparisonReady = comparison is not null && (!starts.TryGetValue((item.Symbol, type), out var since) || comparison.BaselineAt >= since);
                if (comparison is null || starts.TryGetValue((item.Symbol, type), out var start) && comparison.BaselineAt < start) continue;
                var matches = type == AlertType.Rise ? comparison.ChangePercent >= condition.ThresholdPercent :
                    comparison.ChangePercent <= -condition.ThresholdPercent;
                Evaluate(item, type, matches, quote.Price, now, comparison, submitted);
            }
        }
        finally { gate.Release(); Publish(submitted); }
    }

    private void EvaluatePrices(SymbolSetting item, decimal price, DateTimeOffset now, List<AlertHistoryEntry> submitted)
    {
        if (item.Alert.UpperPrice is > 0) Evaluate(item, AlertType.Upper, price >= item.Alert.UpperPrice, price, now, null, submitted);
        if (item.Alert.LowerPrice is > 0) Evaluate(item, AlertType.Lower, price <= item.Alert.LowerPrice, price, now, null, submitted);
    }

    private void Evaluate(SymbolSetting item, AlertType type, bool matches, decimal price, DateTimeOffset now,
        QuoteComparison? comparison, List<AlertHistoryEntry> submitted)
    {
        var policy = item.Alert.Policy(type);
        var triggered = item.Alert.Triggered(type);
        var decision = AlertConditionEvaluator.Decide(matches, triggered, policy, now);
        if (decision == AlertDecision.None) return;
        if (decision is AlertDecision.Arm or AlertDecision.Consume)
        {
            var previous = item.Alert.Copy();
            policy.Armed = decision == AlertDecision.Arm;
            try { store.Save(settings); }
            catch { item.Alert = previous; throw; }
            return;
        }
        var entry = type is AlertType.Upper or AlertType.Lower
            ? new AlertHistoryEntry(item.Symbol, type, type == AlertType.Upper ? item.Alert.UpperPrice : item.Alert.LowerPrice, price, now)
            : new AlertHistoryEntry(item.Symbol, type, null, price, now,
                (type == AlertType.Rise ? item.Alert.Rise : item.Alert.Fall).WindowMinutes,
                (type == AlertType.Rise ? item.Alert.Rise : item.Alert.Fall).ThresholdPercent,
                comparison!.BaselinePrice, comparison.BaselineAt, comparison.QuoteAt, comparison.ChangePercent);
        Trigger(item, type, entry);
        submitted.Add(entry);
    }

    private void Trigger(SymbolSetting item, AlertType type, AlertHistoryEntry entry)
    {
        var previousHistory = history.Load();
        var previous = item.Alert.Copy();
        var stateSaved = false; var historySaved = false;
        item.Alert.SetTriggered(type, true);
        item.Alert.Policy(type).LastTriggeredAt = entry.TriggeredAt;
        item.Alert.Policy(type).Armed = false;
        try
        {
            store.Save(settings); stateSaved = true;
            history.Save([.. previousHistory, entry]); historySaved = true;
            notifications.Show(entry);
        }
        catch (Exception failure)
        {
            item.Alert = previous;
            pendingHistoryRollback = historySaved ? previousHistory : null;
            pendingSettingsRollback = stateSaved;
            var rollbackFailures = RestorePendingWrites();
            if (rollbackFailures.Count > 0)
                throw new AggregateException("提醒提交失敗，且尚未還原檔案；恢復寫入權限後將自動還原，再恢復提醒。", [failure, .. rollbackFailures]);
            throw;
        }
    }

    private void Publish(IEnumerable<AlertHistoryEntry> entries)
    {
        foreach (var entry in entries)
            foreach (var callback in Submitted?.GetInvocationList() ?? [])
                try { ((Action<AlertHistoryEntry>)callback)(entry); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError("提醒紀錄畫面更新失敗：{0}", ex); }
    }

    private List<Exception> RestorePendingWrites()
    {
        var failures = new List<Exception>();
        if (pendingHistoryRollback is not null)
            try { history.Save(pendingHistoryRollback); pendingHistoryRollback = null; }
            catch (Exception ex) { failures.Add(ex); }
        if (pendingSettingsRollback)
            try { store.Save(settings); pendingSettingsRollback = false; }
            catch (Exception ex) { failures.Add(ex); }
        return failures;
    }
    private void EnsureRecovered()
    {
        var failures = RestorePendingWrites();
        if (failures.Count > 0) throw new AggregateException("尚未還原上次失敗的提醒；請恢復設定與紀錄檔的寫入權限。", failures);
    }

    private static void DisarmPreviouslyTriggered(PriceAlertSettings alert)
    {
        foreach (var type in Enum.GetValues<AlertType>())
            if (alert.Triggered(type) && alert.Policy(type).Strategy == AlertStrategy.Repeat) alert.Policy(type).Armed = false;
    }
    private void ResetContinuity(SymbolSetting item)
    {
        quotes.Clear(item.Symbol); lastQuote.Remove(item.Symbol);
        item.Alert.Rise.ComparisonReady = false; item.Alert.Fall.ComparisonReady = false;
        DisarmPreviouslyTriggered(item.Alert);
    }
    public async Task OnConnectionStatusAsync(ConnectionStatus status)
    {
        if (status == ConnectionStatus.Connected) return;
        await gate.WaitAsync();
        try { foreach (var item in settings.Symbols) ResetContinuity(item); }
        finally { gate.Release(); }
    }

    private static void Reset(PriceAlertSettings alert, AlertType type)
    {
        alert.SetTriggered(type, false);
        var policy = alert.Policy(type); policy.Armed = true; policy.LastTriggeredAt = null;
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
            foreach (var side in type is null ? Enum.GetValues<AlertType>() : [type.Value]) Reset(item.Alert, side);
            try { store.Save(settings); } catch { item.Alert = previous; throw; }
        }
        finally { gate.Release(); }
    }

    private static void MergeState(PriceAlertSettings updated, PriceAlertSettings? live, IEnumerable<AlertResetRequest> resets)
    {
        foreach (var type in Enum.GetValues<AlertType>())
        {
            updated.SetTriggered(type, live?.Triggered(type) ?? false);
            var policy = updated.Policy(type); var previous = live?.Policy(type);
            policy.LastTriggeredAt = previous?.LastTriggeredAt;
            policy.Armed = previous?.Armed ?? true;
            if (previous is not null && policy.Strategy != previous.Strategy && updated.Triggered(type)) policy.Armed = false;
            if (live is not null && type is AlertType.Rise or AlertType.Fall)
            {
                var next = type == AlertType.Rise ? updated.Rise : updated.Fall;
                var old = type == AlertType.Rise ? live.Rise : live.Fall;
                if ((next.WindowMinutes != old.WindowMinutes || next.ThresholdPercent != old.ThresholdPercent) &&
                    next.Triggered && next.Policy.Strategy == AlertStrategy.Repeat) next.Policy.Armed = false;
            }
        }
        foreach (var reset in resets) Reset(updated, reset.Type);
        updated.Validate();
    }
    private void MarkChangedWindows(string symbol, PriceAlertSettings updated, PriceAlertSettings? live)
    {
        foreach (var type in new[] { AlertType.Rise, AlertType.Fall })
        {
            var next = type == AlertType.Rise ? updated.Rise : updated.Fall;
            var old = live is null ? null : type == AlertType.Rise ? live.Rise : live.Fall;
            if (old?.WindowMinutes != next.WindowMinutes || old?.ThresholdPercent != next.ThresholdPercent)
            {
                starts[(symbol, type)] = clock();
                next.ComparisonReady = false;
            }
        }
    }
    public async Task ApplySettingsAsync(AppSettings updated, IReadOnlyList<AlertResetRequest> resets, bool preserveAlerts = false)
    {
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            foreach (var item in updated.Symbols)
            {
                var live = settings.Symbols.FirstOrDefault(s => s.Symbol == item.Symbol)?.Alert;
                if (preserveAlerts && live is not null) item.Alert = live.Copy();
                MergeState(item.Alert, live, resets.Where(r => r.Symbol == item.Symbol));
            }
            store.Save(updated);
            foreach (var item in updated.Symbols) MarkChangedWindows(item.Symbol, item.Alert, settings.Symbols.FirstOrDefault(s => s.Symbol == item.Symbol)?.Alert);
            foreach (var item in settings.Symbols.Where(s => !updated.Symbols.Any(n => n.Enabled && n.Symbol == s.Symbol))) ResetContinuity(item);
            settings = updated;
        }
        finally { gate.Release(); }
    }
    public async Task SaveAlertAsync(string symbol, PriceAlertSettings updated, IReadOnlyList<AlertResetRequest> resets)
    {
        await gate.WaitAsync();
        try
        {
            EnsureRecovered();
            var item = settings.Symbols.FirstOrDefault(s => s.Symbol == symbol)
                ?? throw new InvalidOperationException("此幣種已移除，請關閉警示視窗。");
            var alert = updated.Copy();
            MergeState(alert, item.Alert, resets.Where(r => r.Symbol == symbol));
            var snapshot = settings.Copy(); snapshot.Symbols.Single(s => s.Symbol == symbol).Alert = alert;
            store.Save(snapshot);
            MarkChangedWindows(symbol, alert, item.Alert);
            item.Alert = alert;
        }
        finally { gate.Release(); }
    }
}
