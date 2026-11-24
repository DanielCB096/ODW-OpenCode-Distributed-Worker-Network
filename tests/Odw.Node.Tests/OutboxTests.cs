using Odw.Core.Model;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Outbox;
using Xunit;

namespace Odw.Node.Tests;

public class OutboxTests
{
    [Fact]
    public async Task Claim_is_exclusive_under_concurrency()
    {
        await using var fixture = TestDatabase.Create();
        var outbox = new OutboxStore(fixture.Database, fixture.Time);

        for (var i = 0; i < 3; i++)
        {
            await outbox.EnqueueAsync(new NewPendingAction(
                OutboxActions.SendTask,
                TestRows.NewId(),
                TaskRole.Origin,
                "{}",
                fixture.Time.Now.AddSeconds(-1)));
        }

        var results = await Task.WhenAll(
            outbox.ClaimDueAsync(2),
            outbox.ClaimDueAsync(2));

        var claimed = results.SelectMany(items => items).ToList();

        Assert.Equal(3, claimed.Count);
        Assert.Equal(3, claimed.Select(item => item.Id).Distinct().Count());
        Assert.All(claimed, item => Assert.Equal(1, item.Attempts));
        Assert.Empty(await outbox.ClaimDueAsync(2));
    }

    [Fact]
    public async Task Future_actions_are_not_claimed()
    {
        await using var fixture = TestDatabase.Create();
        var outbox = new OutboxStore(fixture.Database, fixture.Time);

        await outbox.EnqueueAsync(new NewPendingAction(
            OutboxActions.PollTask,
            TestRows.NewId(),
            TaskRole.Origin,
            "{}",
            fixture.Time.Now.AddHours(1)));

        Assert.Empty(await outbox.ClaimDueAsync(10));
        Assert.Equal(1, outbox.Count(OutboxStatus.Pending));
    }

    [Fact]
    public async Task Failure_schedules_backoff_and_dead_letters()
    {
        await using var fixture = TestDatabase.Create();
        var outbox = new OutboxStore(fixture.Database, fixture.Time, maxAttempts: 3);

        var id = await outbox.EnqueueAsync(new NewPendingAction(
            OutboxActions.SendTask,
            TestRows.NewId(),
            TaskRole.Origin,
            "{}",
            fixture.Time.Now));

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var claimed = Assert.Single(await outbox.ClaimDueAsync(1));
            Assert.Equal(id, claimed.Id);
            Assert.Equal(attempt, claimed.Attempts);

            var outcome = await outbox.FailAsync(id, $"error {attempt}");
            var stored = outbox.Get(id);
            Assert.NotNull(stored);

            if (attempt < 3)
            {
                Assert.Equal(OutboxFailOutcome.RetryScheduled, outcome);
                Assert.Equal(OutboxStatus.Pending, stored.Status);
                Assert.True(stored.NextAttemptAt > fixture.Time.Now);

                fixture.Time.Advance(OutboxStore.BackoffFor(attempt) + TimeSpan.FromSeconds(1));
            }
            else
            {
                Assert.Equal(OutboxFailOutcome.DeadLettered, outcome);
                Assert.Equal(OutboxStatus.Dead, stored.Status);
                Assert.Equal("error 3", stored.LastError);
            }
        }

        Assert.Empty(await outbox.ClaimDueAsync(1));
    }

    [Fact]
    public async Task Reset_returns_running_actions_to_pending()
    {
        await using var fixture = TestDatabase.Create();
        var outbox = new OutboxStore(fixture.Database, fixture.Time);

        await outbox.EnqueueAsync(new NewPendingAction(
            OutboxActions.FetchResult,
            TestRows.NewId(),
            TaskRole.Origin,
            "{}",
            fixture.Time.Now));

        var claimed = Assert.Single(await outbox.ClaimDueAsync(1));
        Assert.Equal(OutboxStatus.Running, claimed.Status);

        Assert.Equal(1, await outbox.ResetStuckAsync());

        var stored = outbox.Get(claimed.Id);
        Assert.NotNull(stored);
        Assert.Equal(OutboxStatus.Pending, stored.Status);
        Assert.True(stored.NextAttemptAt <= fixture.Time.Now);
    }

    [Fact]
    public async Task Completing_a_running_action_marks_it_done()
    {
        await using var fixture = TestDatabase.Create();
        var outbox = new OutboxStore(fixture.Database, fixture.Time);

        await outbox.EnqueueAsync(new NewPendingAction(
            OutboxActions.CancelTask,
            TestRows.NewId(),
            TaskRole.Origin,
            "{}",
            fixture.Time.Now));

        var claimed = Assert.Single(await outbox.ClaimDueAsync(1));
        Assert.True(await outbox.CompleteAsync(claimed.Id));

        var stored = outbox.Get(claimed.Id);
        Assert.NotNull(stored);
        Assert.Equal(OutboxStatus.Done, stored.Status);
        Assert.False(await outbox.CompleteAsync(claimed.Id));
    }
}
