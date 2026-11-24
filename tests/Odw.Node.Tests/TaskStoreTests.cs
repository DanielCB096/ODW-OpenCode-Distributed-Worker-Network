using Microsoft.Data.Sqlite;
using Odw.Core.Model;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Tasks;
using Xunit;

namespace Odw.Node.Tests;

public class TaskStoreTests
{
    [Fact]
    public async Task Create_writes_initial_event_and_audit_chain()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);

        var snapshot = await store.CreateAsync(TestRows.Origin());

        Assert.Equal(TaskState.Proposed, snapshot.State);
        Assert.Equal(0, snapshot.StateSeq);
        Assert.True(snapshot.CancelRequested == false);

        var events = store.GetEvents(snapshot.TaskId, TaskRole.Origin);
        Assert.Single(events);
        Assert.Equal(0, events[0].Seq);
        Assert.Equal("task.created", events[0].Type);
        Assert.True(store.VerifyEvents(snapshot.TaskId, TaskRole.Origin).IsValid);
        Assert.True(store.VerifyAudit().IsValid);
    }

    [Fact]
    public async Task Transition_is_compare_and_set()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var created = await store.CreateAsync(TestRows.Origin());

        var request = TestRows.Transition(created, TaskState.WaitingApproval, fixture.Time.Now);
        var applied = await store.TransitionAsync(request);

        Assert.Equal(TransitionStatus.Applied, applied.Status);
        Assert.Equal(1, applied.StateSeq);

        var stale = await store.TransitionAsync(request);
        Assert.Equal(TransitionStatus.Conflict, stale.Status);
        Assert.Equal(1, stale.StateSeq);

        var current = store.Get(created.TaskId, TaskRole.Origin);
        Assert.NotNull(current);
        Assert.Equal(TaskState.WaitingApproval, current.State);
        Assert.Equal(1, current.StateSeq);
        Assert.Equal(2, store.GetEvents(created.TaskId, TaskRole.Origin).Count);
        Assert.True(store.VerifyEvents(created.TaskId, TaskRole.Origin).IsValid);
        Assert.True(store.VerifyAudit().IsValid);
    }

    [Fact]
    public async Task Transition_on_unknown_task_returns_not_found()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var created = await store.CreateAsync(TestRows.Origin());

        var request = TestRows.Transition(created, TaskState.WaitingApproval, fixture.Time.Now) with
        {
            TaskId = TestRows.NewId(),
        };

        var result = await store.TransitionAsync(request);
        Assert.Equal(TransitionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Duplicate_task_id_is_rejected()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var id = TestRows.NewId();

        await store.CreateAsync(TestRows.Origin(taskId: id));

        await Assert.ThrowsAsync<SqliteException>(() => store.CreateAsync(TestRows.Origin(taskId: id)));
    }

    [Fact]
    public async Task Create_with_next_action_is_atomic()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var action = new NewPendingAction(
            OutboxActions.SendTask,
            TestRows.NewId(),
            TaskRole.Origin,
            "{}",
            fixture.Time.Now);

        await store.CreateAsync(TestRows.Origin(nextAction: action));

        using var connection = fixture.Database.OpenReadConnection();
        Assert.Equal(1L, MigrationTests.ScalarLong(connection, "SELECT COUNT(*) FROM pending_actions;"));
        Assert.Equal(1L, MigrationTests.ScalarLong(connection, "SELECT COUNT(*) FROM task_events;"));
    }

    [Fact]
    public async Task Invalid_state_for_role_is_rejected()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateAsync(TestRows.Origin(TaskState.Completed)));
    }

    [Fact]
    public async Task Tampered_event_chain_is_detected()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var created = await store.CreateAsync(TestRows.Origin());

        using (var connection = new SqliteConnection(
                   new SqliteConnectionStringBuilder { DataSource = fixture.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            MigrationTests.Execute(connection, "DROP TRIGGER trg_task_events_no_update;");
            MigrationTests.Execute(connection, "UPDATE task_events SET payload_json = '{\"tampered\":true}';");
        }

        var verification = store.VerifyEvents(created.TaskId, TaskRole.Origin);
        Assert.False(verification.IsValid);
        Assert.Equal(0, verification.FirstBadSeq);
    }

    [Fact]
    public async Task Tampered_audit_chain_is_detected()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        await store.CreateAsync(TestRows.Origin());

        using (var connection = new SqliteConnection(
                   new SqliteConnectionStringBuilder { DataSource = fixture.Path, Pooling = false }.ToString()))
        {
            connection.Open();
            MigrationTests.Execute(connection, "DROP TRIGGER trg_audit_no_update;");
            MigrationTests.Execute(connection, "UPDATE audit_log SET actor = 'attacker';");
        }

        Assert.False(store.VerifyAudit().IsValid);
    }

    [Fact]
    public async Task List_by_state_filters_and_orders()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);

        await store.CreateAsync(TestRows.Origin(TaskState.Proposed));
        await store.CreateAsync(TestRows.Origin(TaskState.Proposed));
        await store.CreateAsync(TestRows.Origin(TaskState.Done));

        Assert.Equal(2, store.ListByState(TaskRole.Origin, TaskState.Proposed).Count);
        Assert.Equal(3, store.ListByState(TaskRole.Origin).Count);
    }
}
