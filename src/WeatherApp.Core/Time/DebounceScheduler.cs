namespace WeatherApp.Core.Time;

/// Production debounce backed by a System.Timers.Timer. The Timer raises Elapsed on
/// a ThreadPool thread, so the scheduler marshals the scheduled action onto the
/// SynchronizationContext captured when Schedule is called (the UI thread in
/// production): the whole action — including its post-await continuation — and its
/// UI-bound state mutations therefore run on the UI thread. If no context is
/// installed (e.g. in tests), the action is invoked inline on the ThreadPool thread.
/// A new Schedule stops and disposes any prior pending timer, so only the latest
/// action fires.
public sealed class DebounceScheduler : IDebounceScheduler, IDisposable
{
    private System.Timers.Timer? _timer;

    public void Schedule(TimeSpan delay, Func<Task> action)
    {
        var context = SynchronizationContext.Current;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = new System.Timers.Timer(delay.TotalMilliseconds) { AutoReset = false };
        _timer.Elapsed += (_, _) =>
        {
            if (context is not null)
                context.Post(_ => _ = action(), null);
            else
                _ = action();
        };
        _timer.Start();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
