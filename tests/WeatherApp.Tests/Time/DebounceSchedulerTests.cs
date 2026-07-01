using FluentAssertions;
using WeatherApp.Core.Time;

namespace WeatherApp.Tests.Time;

public class DebounceSchedulerTests
{
    // Poll a condition rather than sleeping a fixed span, to keep the real-clock
    // adapter's tests robust against scheduler jitter on a busy CI host.
    private static async Task<bool> Eventually(Func<bool> condition, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(10);
        }
        return condition();
    }

    [Fact]
    public async Task Fires_the_scheduled_action_after_the_delay()
    {
        using var scheduler = new DebounceScheduler();
        var fired = false;

        scheduler.Schedule(TimeSpan.FromMilliseconds(20), () => { fired = true; return Task.CompletedTask; });

        (await Eventually(() => fired)).Should().BeTrue();
    }

    [Fact]
    public async Task A_new_schedule_cancels_the_previous_pending_action()
    {
        using var scheduler = new DebounceScheduler();
        var count = 0;

        scheduler.Schedule(TimeSpan.FromMilliseconds(80), () => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        scheduler.Schedule(TimeSpan.FromMilliseconds(80), () => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        (await Eventually(() => count >= 1)).Should().BeTrue();
        await Task.Delay(120); // give any wrongly-surviving first action time to also fire
        count.Should().Be(1);
    }

    // Records every Post and runs the callback inline, standing in for the WPF
    // dispatcher's synchronisation context so the test can assert the scheduled
    // action (and its continuation) is marshalled onto the captured UI context.
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;
        public int PostCount => _postCount;

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            // Model a real UI-thread dispatcher: the posted callback observes this
            // context as SynchronizationContext.Current while it runs.
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }

    [Fact]
    public async Task Runs_the_scheduled_action_on_the_synchronization_context_captured_when_scheduled()
    {
        var previous = SynchronizationContext.Current;
        var recording = new RecordingSynchronizationContext();
        SynchronizationContext? observedInsideAction = null;
        var done = false;
        try
        {
            SynchronizationContext.SetSynchronizationContext(recording);
            using var scheduler = new DebounceScheduler();

            scheduler.Schedule(TimeSpan.FromMilliseconds(20), () =>
            {
                observedInsideAction = SynchronizationContext.Current;
                done = true;
                return Task.CompletedTask;
            });

            (await Eventually(() => done)).Should().BeTrue();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        recording.PostCount.Should().BeGreaterThan(0);
        observedInsideAction.Should().BeSameAs(recording);
    }
}
