using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Scheduling;
using Odw.Node.Tasks;
using Odw.Sandbox.Workspaces;
using Xunit;

namespace Odw.Node.Tests;

internal sealed class FakeWorkspacePreparer : IWorkspacePreparer
{
    public FakeWorkspacePreparer()
    {
        Root = Path.Combine(Path.GetTempPath(), "odw-fake-ws", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public List<WorkspaceRequest> Requests { get; } = [];

    public Func<WorkspaceRequest, Exception?>? FailWith { get; set; }

    public Task<WorkspaceLease> PrepareAsync(WorkspaceRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        var failure = FailWith?.Invoke(request);
        if (failure is not null)
        {
            return Task.FromException<WorkspaceLease>(failure);
        }

        var path = Path.Combine(Root, request.TaskId);
        Directory.CreateDirectory(path);
        return Task.FromResult(new WorkspaceLease(path, "detached:main", request.ReadOnly, keep: false));
    }
}

internal sealed class FakeExecutionRunner : ITaskExecutionRunner
{
    public Func<ExecutionRequest, ExecutionResult> Handler { get; set; }
        = _ => new ExecutionResult(ExecutorOutcomeKind.Succeeded, """{"outcome":"succeeded"}""");

    public List<ExecutionRequest> Requests { get; } = [];

    public Task<ExecutionResult> RunAsync(ExecutionRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(Handler(request));
    }
}

public class ExecutionSchedulerTests
{
    private static async Task<(TestDatabase Fixture, TaskStore Store, TaskCoordinator Coordinator)>
        SetupAsync()
    {
        var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);
        return (fixture, store, coordinator);
    }

    private static Task<TaskSnapshot> QueueAsync(
        TaskCoordinator coordinator,
        string taskId,
        TaskState state = TaskState.Queued)
        => coordinator.AcceptAsync(
            ContractModels.Analysis(taskId),
            TaskRole.Target,
            ContractJson.Analysis(taskId),
            state,
            "task.received",
            "{}");

    [Fact]
    public async Task Runs_a_queued_task_to_completion()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var task = await QueueAsync(coordinator, TestRows.NewId());
        var workspaces = new FakeWorkspacePreparer();
        var runner = new FakeExecutionRunner();

        await using var scheduler = new ExecutionScheduler(
            store, coordinator, workspaces, runner, fixture.Time);

        var executed = await scheduler.RunPendingAsync();

        Assert.Equal(1, executed);
        var current = store.Get(task.TaskId, TaskRole.Target);
        Assert.NotNull(current);
        Assert.Equal(TaskState.Completed, current.State);
        Assert.Equal(2, current.StateSeq);
        Assert.Equal("""{"outcome":"succeeded"}""", current.ResultJson);
        Assert.NotNull(current.FinishedAt);

        var events = store.GetEvents(task.TaskId, TaskRole.Target);
        Assert.Equal(["task.received", "task.started", "task.completed"], events.Select(item => item.Type));
        Assert.Single(runner.Requests);
        Assert.Equal(workspaces.Root, Path.GetDirectoryName(runner.Requests[0].WorkspacePath));
    }

    [Fact]
    public async Task Workspace_is_deleted_after_execution()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        await QueueAsync(coordinator, TestRows.NewId());
        var workspaces = new FakeWorkspacePreparer();
        await using var scheduler = new ExecutionScheduler(
            store, coordinator, workspaces, new FakeExecutionRunner(), fixture.Time);

        await scheduler.RunPendingAsync();

        var path = Path.Combine(workspaces.Root, store.ListByState(TaskRole.Target).Single().TaskId);
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task Runner_failure_marks_task_failed()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var task = await QueueAsync(coordinator, TestRows.NewId());
        var runner = new FakeExecutionRunner
        {
            Handler = _ => new ExecutionResult(ExecutorOutcomeKind.Failed, """{"outcome":"failed"}"""),
        };

        await using var scheduler = new ExecutionScheduler(
            store, coordinator, new FakeWorkspacePreparer(), runner, fixture.Time);

        await scheduler.RunPendingAsync();

        Assert.Equal(TaskState.Failed, store.Get(task.TaskId, TaskRole.Target)!.State);
    }

    [Fact]
    public async Task Review_flag_maps_to_review_required()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var task = await QueueAsync(coordinator, TestRows.NewId());
        var runner = new FakeExecutionRunner
        {
            Handler = _ => new ExecutionResult(
                ExecutorOutcomeKind.Succeeded,
                """{"outcome":"review_required"}""",
                RequiresReview: true),
        };

        await using var scheduler = new ExecutionScheduler(
            store, coordinator, new FakeWorkspacePreparer(), runner, fixture.Time);

        await scheduler.RunPendingAsync();

        Assert.Equal(TaskState.ReviewRequired, store.Get(task.TaskId, TaskRole.Target)!.State);
    }

    [Fact]
    public async Task Cancelled_outcome_cancels_task()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var task = await QueueAsync(coordinator, TestRows.NewId());
        var runner = new FakeExecutionRunner
        {
            Handler = _ => new ExecutionResult(ExecutorOutcomeKind.Cancelled, """{"outcome":"cancelled"}"""),
        };

        await using var scheduler = new ExecutionScheduler(
            store, coordinator, new FakeWorkspacePreparer(), runner, fixture.Time);

        await scheduler.RunPendingAsync();

        var current = store.Get(task.TaskId, TaskRole.Target);
        Assert.Equal(TaskState.Cancelled, current!.State);
        Assert.Equal(3, current.StateSeq);
    }

    [Fact]
    public async Task Workspace_failure_marks_task_failed()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var task = await QueueAsync(coordinator, TestRows.NewId());
        var workspaces = new FakeWorkspacePreparer
        {
            FailWith = _ => new WorkspaceException("sin repo"),
        };

        await using var scheduler = new ExecutionScheduler(
            store, coordinator, workspaces, new FakeExecutionRunner(), fixture.Time);

        await scheduler.RunPendingAsync();

        var current = store.Get(task.TaskId, TaskRole.Target);
        Assert.Equal(TaskState.Failed, current!.State);
        Assert.Contains("sin repo", current.ResultJson);
    }

    [Fact]
    public async Task Only_queued_tasks_are_executed()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var queued = await QueueAsync(coordinator, TestRows.NewId());
        var awaiting = await QueueAsync(coordinator, TestRows.NewId(), TaskState.AwaitingLocalApproval);
        var runner = new FakeExecutionRunner();

        await using var scheduler = new ExecutionScheduler(
            store, coordinator, new FakeWorkspacePreparer(), runner, fixture.Time);

        var executed = await scheduler.RunPendingAsync();

        Assert.Equal(1, executed);
        Assert.Equal(TaskState.Completed, store.Get(queued.TaskId, TaskRole.Target)!.State);
        Assert.Equal(TaskState.AwaitingLocalApproval, store.Get(awaiting.TaskId, TaskRole.Target)!.State);
    }

    [Fact]
    public async Task Concurrency_limit_is_respected()
    {
        var (fixture, store, coordinator) = await SetupAsync();
        await using var _ = fixture;

        var first = await QueueAsync(coordinator, TestRows.NewId());
        var second = await QueueAsync(coordinator, TestRows.NewId());

        await using var scheduler = new ExecutionScheduler(
            store,
            coordinator,
            new FakeWorkspacePreparer(),
            new FakeExecutionRunner(),
            fixture.Time,
            new ExecutionSchedulerOptions { MaxConcurrentTasks = 1 });

        Assert.Equal(1, await scheduler.RunPendingAsync());
        Assert.Single(store.ListByState(TaskRole.Target, TaskState.Queued));

        Assert.Equal(1, await scheduler.RunPendingAsync());
        Assert.Equal(TaskState.Completed, store.Get(first.TaskId, TaskRole.Target)!.State);
        Assert.Equal(TaskState.Completed, store.Get(second.TaskId, TaskRole.Target)!.State);
    }
}
