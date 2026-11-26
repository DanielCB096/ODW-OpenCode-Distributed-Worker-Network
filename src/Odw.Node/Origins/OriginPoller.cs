using System.Globalization;
using Odw.Core.Model;
using Odw.Core.StateMachines;
using Odw.Node.Peers;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Peers;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Tasks;
using Odw.PeerApi;

namespace Odw.Node.Origins;

public sealed record OriginPollerOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Refresca el estado de las tareas de origen contra sus pares: aceptación, progreso,
/// resultado y estados terminales. No reejecuta nada.
/// </summary>
public sealed class OriginPoller : IAsyncDisposable
{
    private enum Goal
    {
        Accepted,
        Running,
        Interrupted,
        Done,
        Failed,
        Rejected,
        Cancelled,
    }

    private readonly TaskStore _store;
    private readonly TaskCoordinator _coordinator;
    private readonly PeerRegistry _peers;
    private readonly PeerApiClientFactory _clients;
    private readonly PeerStore? _peerStore;
    private readonly TimeProvider _timeProvider;
    private readonly OriginPollerOptions _options;
    private readonly Action<string>? _log;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private bool _disposed;

    public OriginPoller(
        TaskStore store,
        TaskCoordinator coordinator,
        PeerRegistry peers,
        PeerApiClientFactory clients,
        TimeProvider timeProvider,
        PeerStore? peerStore = null,
        OriginPollerOptions? options = null,
        Action<string>? log = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _peers = peers ?? throw new ArgumentNullException(nameof(peers));
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _peerStore = peerStore;
        _options = options ?? new OriginPollerOptions();
        _log = log;

        if (_options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
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

    public async Task<int> RefreshOnceAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _store.ListByState(
            TaskRole.Origin,
            TaskState.Sent,
            TaskState.Accepted,
            TaskState.Running,
            TaskState.Unreachable,
            TaskState.Interrupted);

        var processed = 0;

        foreach (var task in tasks)
        {
            var registration = _peers.GetRegistration(task.CounterpartNode ?? string.Empty);
            if (registration is null || string.IsNullOrWhiteSpace(registration.Address))
            {
                continue;
            }

            try
            {
                using var client = _clients.Create(registration);
                var view = await client.GetTaskAsync(task.TaskId, cancellationToken).ConfigureAwait(false);
                if (view is null)
                {
                    continue;
                }

                processed++;
                await TouchAsync(registration.NodeId, cancellationToken).ConfigureAwait(false);

                var goal = GoalFor(view.State);
                if (goal is null)
                {
                    continue;
                }

                string? resultJson = null;
                if (goal is Goal.Done or Goal.Failed && view.ResultAvailable)
                {
                    var result = await client.GetResultAsync(task.TaskId, cancellationToken).ConfigureAwait(false);
                    resultJson = result?.ResultJson;
                }

                await AdvanceAsync(task.TaskId, goal.Value, resultJson, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                _log?.Invoke($"poller: {registration.NodeId}: {exception.Message}");
            }
        }

        return processed;
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
                await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log?.Invoke($"poller: {exception.Message}");
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

    private async Task AdvanceAsync(
        string taskId,
        Goal goal,
        string? resultJson,
        CancellationToken cancellationToken)
    {
        for (var step = 0; step < 6; step++)
        {
            var snapshot = _store.Get(taskId, TaskRole.Origin);
            if (snapshot is null || snapshot.State.IsTerminal())
            {
                return;
            }

            if (snapshot.State == GoalState(goal))
            {
                return;
            }

            var trigger = TriggerFor(snapshot.State, goal);
            if (trigger is null)
            {
                return;
            }

            var now = _timeProvider.GetUtcNow();
            var final = trigger is TaskTrigger.TaskCompleted
                or TaskTrigger.TaskFailed
                or TaskTrigger.TaskCancelled;

            var result = await _coordinator.TransitionAsync(
                taskId,
                TaskRole.Origin,
                trigger.Value,
                TransitionContext.Default,
                now,
                final
                    ? new TransitionOptions
                    {
                        FinishedAt = now,
                        ResultJson = resultJson ?? "{}",
                    }
                    : null,
                cancellationToken).ConfigureAwait(false);

            if (result.Status != TransitionStatus.Applied)
            {
                return;
            }
        }
    }

    private static TaskState GoalState(Goal goal) => goal switch
    {
        Goal.Accepted => TaskState.Accepted,
        Goal.Running => TaskState.Running,
        Goal.Interrupted => TaskState.Interrupted,
        Goal.Done => TaskState.Done,
        Goal.Failed => TaskState.Failed,
        Goal.Rejected => TaskState.Rejected,
        _ => TaskState.Cancelled,
    };

    private static TaskTrigger? TriggerFor(TaskState state, Goal goal)
    {
        switch (state)
        {
            case TaskState.Sent:
                return goal switch
                {
                    Goal.Rejected => TaskTrigger.TransportRejected,
                    Goal.Interrupted => TaskTrigger.TaskInterrupted,
                    _ => TaskTrigger.TransportAccepted,
                };

            case TaskState.Accepted:
                return goal switch
                {
                    Goal.Rejected => TaskTrigger.TaskRejected,
                    Goal.Interrupted => TaskTrigger.TaskInterrupted,
                    Goal.Running or Goal.Done or Goal.Failed => TaskTrigger.TaskProgress,
                    _ => null,
                };

            case TaskState.Running:
                return goal switch
                {
                    Goal.Done => TaskTrigger.TaskCompleted,
                    Goal.Failed or Goal.Rejected => TaskTrigger.TaskFailed,
                    Goal.Interrupted => TaskTrigger.TaskInterrupted,
                    Goal.Cancelled => TaskTrigger.UserCancel,
                    _ => null,
                };

            case TaskState.Unreachable:
                return goal switch
                {
                    Goal.Cancelled => TaskTrigger.UserCancel,
                    Goal.Interrupted => TaskTrigger.TaskInterrupted,
                    Goal.Running or Goal.Done or Goal.Failed or Goal.Rejected => TaskTrigger.TaskProgress,
                    _ => null,
                };

            case TaskState.Interrupted:
                return goal switch
                {
                    Goal.Done => TaskTrigger.TaskCompleted,
                    Goal.Failed or Goal.Rejected => TaskTrigger.TaskFailed,
                    Goal.Cancelled => TaskTrigger.TaskCancelled,
                    _ => null,
                };

            case TaskState.Cancelling:
                return goal == Goal.Cancelled ? TaskTrigger.TaskCancelled : null;

            default:
                return null;
        }
    }

    private static Goal? GoalFor(string remoteState) => remoteState.ToUpperInvariant() switch
    {
        "RECEIVED" or "AWAITING_LOCAL_APPROVAL" or "QUEUED" => Goal.Accepted,
        "RUNNING" => Goal.Running,
        "INTERRUPTED" => Goal.Interrupted,
        "COMPLETED" or "REVIEW_REQUIRED" => Goal.Done,
        "FAILED" => Goal.Failed,
        "REJECTED" => Goal.Rejected,
        "CANCELLED" => Goal.Cancelled,
        _ => null,
    };

    private Task TouchAsync(string nodeId, CancellationToken cancellationToken)
        => _peerStore is null
            ? Task.CompletedTask
            : _peerStore.TouchAsync(
                nodeId,
                """{"status":"online"}""",
                _timeProvider.GetUtcNow(),
                cancellationToken);
}
