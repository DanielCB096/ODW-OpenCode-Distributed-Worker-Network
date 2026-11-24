using System.Text.Json;
using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Core.StateMachines;
using Odw.Node.Contracts;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Tasks;
using Odw.Sandbox.Workspaces;

namespace Odw.Node.Scheduling;

/// <summary>
/// Toma tareas <c>QUEUED</c> del nodo destino, prepara el workspace, delega en el runner y
/// persiste el resultado. El paso de estado usa CAS: aunque haya dos schedulers, solo uno gana.
/// </summary>
public sealed class ExecutionScheduler : IAsyncDisposable
{
    private readonly TaskStore _store;
    private readonly TaskCoordinator _coordinator;
    private readonly IWorkspacePreparer _workspaces;
    private readonly ITaskExecutionRunner _runner;
    private readonly TimeProvider _timeProvider;
    private readonly ExecutionSchedulerOptions _options;
    private readonly Action<string>? _log;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private bool _disposed;

    public ExecutionScheduler(
        TaskStore store,
        TaskCoordinator coordinator,
        IWorkspacePreparer workspaces,
        ITaskExecutionRunner runner,
        TimeProvider timeProvider,
        ExecutionSchedulerOptions? options = null,
        Action<string>? log = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _options = options ?? new ExecutionSchedulerOptions();
        _log = log;

        if (_options.MaxConcurrentTasks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxConcurrentTasks debe ser >= 1.");
        }
    }

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _loop = Task.Run(() => LoopAsync(_stopping.Token));
    }

    public async Task<int> RunPendingAsync(CancellationToken cancellationToken = default)
    {
        var queued = _store
            .ListByState(TaskRole.Target, TaskState.Queued)
            .Take(_options.MaxConcurrentTasks)
            .ToArray();

        if (queued.Length == 0)
        {
            return 0;
        }

        await Task.WhenAll(queued.Select(task => ExecuteOneAsync(task, cancellationToken)))
            .ConfigureAwait(false);

        return queued.Length;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping.Cancel();

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stopping.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_options.PollInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunPendingAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log?.Invoke($"scheduler: {exception.Message}");
            }

            try
            {
                await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ExecuteOneAsync(TaskSnapshot snapshot, CancellationToken cancellationToken)
    {
        TaskRequest contract;
        try
        {
            contract = Parse(snapshot);
        }
        catch (Exception exception)
        {
            await FailQueuedAsync(snapshot, "CONTRACT_INVALID", exception.Message, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        WorkspaceLease? workspace = null;

        try
        {
            workspace = await _workspaces.PrepareAsync(new WorkspaceRequest(
                snapshot.TaskId,
                contract.Workspace.Repo,
                contract.Workspace.BaseRef,
                contract.Workspace.Mode == WorkspaceMode.ReadOnly), cancellationToken).ConfigureAwait(false);

            var now = _timeProvider.GetUtcNow();
            var leaseMinutes = Math.Clamp(contract.Limits.MaxMinutes, 5, 240);

            var started = await _coordinator.TransitionAsync(
                snapshot.TaskId,
                TaskRole.Target,
                TaskTrigger.SlotAvailable,
                TransitionContext.Satisfied,
                now,
                new TransitionOptions { LeaseExpiresAt = now.AddMinutes(leaseMinutes) },
                cancellationToken).ConfigureAwait(false);

            if (started.Status != TransitionStatus.Applied)
            {
                return;
            }

            ExecutionResult result;
            try
            {
                result = await _runner.RunAsync(
                    new ExecutionRequest(snapshot, contract, workspace.Path, workspace.Branch),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                result = new ExecutionResult(
                    ExecutorOutcomeKind.Failed,
                    ResultJson("failed", exception.Message, review: false));
            }

            await CompleteAsync(snapshot.TaskId, result, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            await FailQueuedAsync(snapshot, "SETUP_FAILED", exception.Message, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            if (workspace is not null)
            {
                await workspace.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task CompleteAsync(string taskId, ExecutionResult result, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var resultJson = string.IsNullOrWhiteSpace(result.ResultJson) ? "{}" : result.ResultJson;

        switch (result.Kind)
        {
            case ExecutorOutcomeKind.Succeeded:
                await _coordinator.TransitionAsync(
                    taskId,
                    TaskRole.Target,
                    TaskTrigger.TaskCompleted,
                    new TransitionContext { ValidationViolated = result.RequiresReview },
                    now,
                    new TransitionOptions { FinishedAt = now, ResultJson = resultJson },
                    cancellationToken).ConfigureAwait(false);
                break;

            case ExecutorOutcomeKind.Interrupted:
            case ExecutorOutcomeKind.Cancelled:
                await CancelAsync(taskId, resultJson, now, cancellationToken).ConfigureAwait(false);
                break;

            default:
                await _coordinator.TransitionAsync(
                    taskId,
                    TaskRole.Target,
                    TaskTrigger.TaskFailed,
                    TransitionContext.Default,
                    now,
                    new TransitionOptions { FinishedAt = now, ResultJson = resultJson },
                    cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task CancelAsync(
        string taskId,
        string resultJson,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cancelling = await _coordinator.TransitionAsync(
            taskId,
            TaskRole.Target,
            TaskTrigger.UserCancel,
            TransitionContext.Default,
            now,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (cancelling.Status != TransitionStatus.Applied)
        {
            return;
        }

        await _coordinator.TransitionAsync(
            taskId,
            TaskRole.Target,
            TaskTrigger.TaskCancelled,
            TransitionContext.Default,
            now,
            new TransitionOptions { FinishedAt = now, ResultJson = resultJson },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task FailQueuedAsync(
        TaskSnapshot snapshot,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        await _coordinator.TransitionAsync(
            snapshot.TaskId,
            TaskRole.Target,
            TaskTrigger.SetupFailed,
            TransitionContext.Default,
            now,
            new TransitionOptions
            {
                FinishedAt = now,
                ResultJson = ResultJson("failed", $"{code}: {message}", review: false),
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static TaskRequest Parse(TaskSnapshot snapshot)
    {
        using var document = JsonDocument.Parse(snapshot.RequestJson);
        return TaskContractReader.Read(document.RootElement);
    }

    private static string ResultJson(string outcome, string message, bool review)
        => PersistenceJson.Serialize(new
        {
            outcome,
            summary_md = message,
            requires_review = review,
        });
}
