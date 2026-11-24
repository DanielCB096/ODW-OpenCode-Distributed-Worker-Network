using Odw.Core.Model;
using Odw.Core.StateMachines;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Tasks;
using Xunit;

namespace Odw.Node.Tests;

public class TaskCoordinatorTests
{
    [Fact]
    public async Task Accept_creates_initial_event_and_audit()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);

        var snapshot = await coordinator.AcceptAsync(
            ContractModels.Analysis(),
            TaskRole.Target,
            "{}",
            TaskState.Queued,
            "task.received",
            "{}");

        Assert.Equal(TaskState.Queued, snapshot.State);
        Assert.Equal(0, snapshot.StateSeq);
        Assert.Equal("PC-A", snapshot.CounterpartNode);
        Assert.Single(store.GetEvents(snapshot.TaskId, TaskRole.Target));
        Assert.True(store.VerifyAudit().IsValid);
    }

    [Fact]
    public async Task Transition_applies_the_state_machine()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);

        var created = await coordinator.AcceptAsync(
            ContractModels.Analysis(),
            TaskRole.Target,
            "{}",
            TaskState.Queued,
            "task.received",
            "{}");

        var result = await coordinator.TransitionAsync(
            created.TaskId,
            TaskRole.Target,
            TaskTrigger.SlotAvailable,
            TransitionContext.Satisfied,
            fixture.Time.Now);

        Assert.Equal(TransitionStatus.Applied, result.Status);
        Assert.Equal(1, result.StateSeq);

        var current = store.Get(created.TaskId, TaskRole.Target);
        Assert.NotNull(current);
        Assert.Equal(TaskState.Running, current.State);

        var events = store.GetEvents(created.TaskId, TaskRole.Target);
        Assert.Equal(2, events.Count);
        Assert.Equal("task.started", events[^1].Type);
    }

    [Fact]
    public async Task Invalid_trigger_throws_and_does_not_write()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);

        var created = await coordinator.AcceptAsync(
            ContractModels.Analysis(),
            TaskRole.Target,
            "{}",
            TaskState.Queued,
            "task.received",
            "{}");

        await Assert.ThrowsAsync<InvalidStateTransitionException>(() => coordinator.TransitionAsync(
            created.TaskId,
            TaskRole.Target,
            TaskTrigger.TaskCompleted,
            TransitionContext.Satisfied,
            fixture.Time.Now));

        var current = store.Get(created.TaskId, TaskRole.Target);
        Assert.NotNull(current);
        Assert.Equal(TaskState.Queued, current.State);
        Assert.Single(store.GetEvents(created.TaskId, TaskRole.Target));
    }

    [Fact]
    public async Task Transition_on_unknown_task_returns_not_found()
    {
        await using var fixture = TestDatabase.Create();
        var coordinator = new TaskCoordinator(
            new TaskStore(fixture.Database, fixture.Time),
            fixture.Time);

        var result = await coordinator.TransitionAsync(
            ContractModels.TaskId,
            TaskRole.Target,
            TaskTrigger.SlotAvailable,
            TransitionContext.Satisfied,
            fixture.Time.Now);

        Assert.Equal(TransitionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Transition_persists_result_and_finished_time()
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);

        var created = await coordinator.AcceptAsync(
            ContractModels.Analysis(),
            TaskRole.Target,
            "{}",
            TaskState.Running,
            "task.received",
            "{}");

        var result = await coordinator.TransitionAsync(
            created.TaskId,
            TaskRole.Target,
            TaskTrigger.TaskCompleted,
            TransitionContext.Default,
            fixture.Time.Now,
            new TransitionOptions
            {
                FinishedAt = fixture.Time.Now,
                ResultJson = """{"outcome":"succeeded"}""",
            });

        Assert.Equal(TransitionStatus.Applied, result.Status);

        var current = store.Get(created.TaskId, TaskRole.Target);
        Assert.Equal(TaskState.Completed, current!.State);
        Assert.Equal("""{"outcome":"succeeded"}""", current.ResultJson);
        Assert.Equal(fixture.Time.Now, current.FinishedAt);
    }
}
