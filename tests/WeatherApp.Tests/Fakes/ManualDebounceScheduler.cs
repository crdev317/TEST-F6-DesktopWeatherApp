using WeatherApp.Core.Time;

namespace WeatherApp.Tests.Fakes;

/// Captures the last scheduled action so a test can fire it synchronously —
/// no real clock, no waiting. Counts schedules so a test can assert that a
/// too-short query never scheduled a search.
public sealed class ManualDebounceScheduler : IDebounceScheduler
{
    private Func<Task>? _pending;
    public int ScheduleCount { get; private set; }

    public void Schedule(TimeSpan delay, Func<Task> action)
    {
        ScheduleCount++;
        _pending = action;
    }

    public Task FireAsync() => _pending?.Invoke() ?? Task.CompletedTask;
}
