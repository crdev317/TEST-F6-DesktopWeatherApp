namespace WeatherApp.Core.Time;

/// Schedules a single pending action after a delay; a new Schedule cancels the
/// previous pending one. Injected so tests fire it synchronously (no real wait).
public interface IDebounceScheduler
{
    void Schedule(TimeSpan delay, Func<Task> action);
}
