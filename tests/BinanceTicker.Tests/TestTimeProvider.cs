namespace BinanceTicker.Tests;

internal sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly object gate = new();
    private DateTimeOffset now = start;
    private readonly HashSet<TestTimer> timers = [];
    public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => GetUtcNow().UtcTicks;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new TestTimer(this, callback, state);
        timer.Change(dueTime, period); return timer;
    }
    public void Advance(TimeSpan delta)
    {
        var target = GetUtcNow() + delta;
        while (true)
        {
            TestTimer[] due;
            lock (gate)
            {
                var next = timers.Select(t => t.Due).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();
                if (next > target) { now = target; return; }
                now = next;
                due = timers.Where(t => t.Due == next).ToArray();
                foreach (var timer in due) timer.Due = timer.Period > TimeSpan.Zero ? next + timer.Period : DateTimeOffset.MaxValue;
            }
            foreach (var timer in due) timer.Callback(timer.State);
        }
    }
    public async Task<T> CompleteAsync<T>(Task<T> task, TimeSpan? step = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!task.IsCompleted) { Advance(step ?? TimeSpan.FromMilliseconds(250)); await Task.Delay(1, timeout.Token); }
        return await task;
    }
    public async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) { Advance(TimeSpan.FromMilliseconds(250)); await Task.Delay(1, timeout.Token); }
    }
    private sealed class TestTimer(TestTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public DateTimeOffset Due { get; set; } = DateTimeOffset.MaxValue;
        public TimeSpan Period { get; private set; }
        private bool disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner.gate)
            {
                if (disposed) return false;
                Period = period; Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : owner.now + dueTime;
                owner.timers.Add(this); return true;
            }
        }
        public void Dispose() { lock (owner.gate) { disposed = true; owner.timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
