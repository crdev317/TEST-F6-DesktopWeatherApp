namespace WeatherApp.Core.Time;

/// Production debounce backed by a System.Timers.Timer. The action is marshalled
/// by the caller (the ViewModel updates observable state, which CommunityToolkit
/// marshals to the UI thread via its synchronisation context). A new Schedule
/// stops and disposes any prior pending timer, so only the latest action fires.
public sealed class DebounceScheduler : IDebounceScheduler, IDisposable
{
    private System.Timers.Timer? _timer;

    public void Schedule(TimeSpan delay, Func<Task> action)
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = new System.Timers.Timer(delay.TotalMilliseconds) { AutoReset = false };
        _timer.Elapsed += async (_, _) => await action();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
