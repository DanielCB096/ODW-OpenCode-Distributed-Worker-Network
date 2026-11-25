using Odw.Node.Scheduling;
using Xunit;

namespace Odw.Node.Tests;

public class NetworkIsolationWatchdogTests
{
    [Fact]
    public async Task Fires_when_the_probe_stops_confirming_isolation()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        await using var watchdog = new NetworkIsolationWatchdog(
            () => Interlocked.Increment(ref calls) < 2,
            TimeSpan.FromMilliseconds(20),
            () => signal.TrySetResult());

        var completed = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.Same(signal.Task, completed);
        Assert.True(watchdog.Lost);
    }

    [Fact]
    public async Task Stops_quietly_when_disposed()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var watchdog = new NetworkIsolationWatchdog(
            () => true,
            TimeSpan.FromMilliseconds(20),
            () => signal.TrySetResult());

        await watchdog.DisposeAsync();

        Assert.False(watchdog.Lost);
        Assert.False(signal.Task.IsCompleted);
    }
}
