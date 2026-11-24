using Odw.Core.Model;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Outbox;
using Odw.Node.Persistence.Tasks;
using Xunit;

namespace Odw.Node.Tests;

public class RecoveryTests
{
    [Fact]
    public async Task Running_target_is_interrupted_and_not_rerun()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var created = await store.CreateAsync(TestRows.Target(TaskState.Running));

        var recovery = new StartupRecovery(fixture.Database, fixture.Time);
        var result = await recovery.RunAsync();

        Assert.Equal(1, result.InterruptedTargets);

        var current = store.Get(created.TaskId, TaskRole.Target);
        Assert.NotNull(current);
        Assert.Equal(TaskState.Interrupted, current.State);
        Assert.Equal(1, current.StateSeq);

        var events = store.GetEvents(created.TaskId, TaskRole.Target);
        Assert.Equal("task.interrupted", events[^1].Type);
        Assert.True(store.VerifyEvents(created.TaskId, TaskRole.Target).IsValid);
        Assert.True(store.VerifyAudit().IsValid);
    }

    [Fact]
    public async Task Expired_origin_lease_becomes_unreachable_and_enqueues_poll()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var created = await store.CreateAsync(
            TestRows.Origin(TaskState.Running, leaseExpiresAt: fixture.Time.Now.AddMinutes(-1)));

        var recovery = new StartupRecovery(fixture.Database, fixture.Time);
        var result = await recovery.RunAsync();

        Assert.Equal(1, result.UnreachableOrigins);

        var current = store.Get(created.TaskId, TaskRole.Origin);
        Assert.NotNull(current);
        Assert.Equal(TaskState.Unreachable, current.State);
        Assert.Equal(1, current.StateSeq);

        var outbox = new OutboxStore(fixture.Database, fixture.Time);
        var claimed = Assert.Single(await outbox.ClaimDueAsync(10));
        Assert.Equal(OutboxActions.PollTask, claimed.Action);
        Assert.Equal(created.TaskId, claimed.TaskId);
    }

    [Fact]
    public async Task Fresh_lease_is_left_alone()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var created = await store.CreateAsync(
            TestRows.Origin(TaskState.Running, leaseExpiresAt: fixture.Time.Now.AddMinutes(5)));

        var recovery = new StartupRecovery(fixture.Database, fixture.Time);
        var result = await recovery.RunAsync();

        Assert.Equal(0, result.UnreachableOrigins);
        Assert.Equal(TaskState.Running, store.Get(created.TaskId, TaskRole.Origin)!.State);
    }

    [Fact]
    public async Task Stuck_outbox_actions_are_reset_at_startup()
    {
        await using var fixture = TestDatabase.Create();
        var outbox = new OutboxStore(fixture.Database, fixture.Time);

        await outbox.EnqueueAsync(new NewPendingAction(
            OutboxActions.SendTask,
            TestRows.NewId(),
            TaskRole.Origin,
            "{}",
            fixture.Time.Now));

        var claimed = Assert.Single(await outbox.ClaimDueAsync(1));
        Assert.Equal(OutboxStatus.Running, claimed.Status);

        var recovery = new StartupRecovery(fixture.Database, fixture.Time);
        var result = await recovery.RunAsync();

        Assert.Equal(1, result.ResetOutboxActions);
        Assert.Equal(OutboxStatus.Pending, outbox.Get(claimed.Id)!.Status);
    }

    [Fact]
    public async Task Recovery_writes_a_service_start_audit_entry()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);

        await new StartupRecovery(fixture.Database, fixture.Time).RunAsync();

        using var connection = fixture.Database.OpenReadConnection();
        Assert.Equal(1L, MigrationTests.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM audit_log WHERE entry_type = 'service.start';"));
        Assert.True(store.VerifyAudit().IsValid);
    }
}
