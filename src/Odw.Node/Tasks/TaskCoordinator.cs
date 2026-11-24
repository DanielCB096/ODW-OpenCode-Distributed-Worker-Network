using Odw.Core.Hashing;
using Odw.Core.Model;
using Odw.Core.StateMachines;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Tasks;

namespace Odw.Node.Tasks;

public sealed record TransitionOptions
{
    public DateTimeOffset? LeaseExpiresAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public bool? CancelRequested { get; init; }

    public string? ExecutorRef { get; init; }

    public string? ResultJson { get; init; }

    public NewPendingAction? NextAction { get; init; }
}

/// <summary>
/// Une la máquina de estados de <c>Odw.Core</c> con la persistencia: restaura el agregado,
/// aplica el disparador, escribe la transición CAS y el evento, y reintenta una vez si otro
/// actor ganó la carrera.
/// </summary>
public sealed class TaskCoordinator
{
    private readonly TaskStore _store;
    private readonly TimeProvider _timeProvider;

    public TaskCoordinator(TaskStore store, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public Task<TaskSnapshot> AcceptAsync(
        TaskRequest request,
        TaskRole role,
        string requestJson,
        TaskState initialState,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        var snapshot = new CreateTaskRequest
        {
            TaskId = request.TaskId,
            Role = role,
            CounterpartNode = role == TaskRole.Target ? request.OriginNode : request.TargetNode,
            ParentTaskId = request.ParentTaskId,
            Kind = request.Kind,
            Mode = request.Workspace.Mode,
            State = initialState,
            TaskHash = TaskHash.Compute(request),
            RequestJson = requestJson,
            OriginNode = request.OriginNode,
            TargetNode = request.TargetNode,
            CreatedAt = request.CreatedAt,
            ExpiresAt = request.ExpiresAt,
            LeaseExpiresAt = role == TaskRole.Origin ? _timeProvider.GetUtcNow().AddSeconds(90) : null,
            InitialEventType = eventType,
            InitialEventPayloadJson = payloadJson,
        };

        return _store.CreateAsync(snapshot, cancellationToken);
    }

    public async Task<TransitionResult> TransitionAsync(
        string taskId,
        TaskRole role,
        TaskTrigger trigger,
        TransitionContext context,
        DateTimeOffset now,
        TransitionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        options ??= new TransitionOptions();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var snapshot = _store.Get(taskId, role);
            if (snapshot is null)
            {
                return new TransitionResult(TransitionStatus.NotFound, 0);
            }

            var aggregate = TaskAggregate.Restore(
                snapshot.TaskId,
                snapshot.Role,
                snapshot.State,
                snapshot.StateSeq,
                snapshot.TaskHash,
                snapshot.CreatedAt);

            var change = aggregate.Apply(trigger, context, now);

            var payload = PersistenceJson.Serialize(new
            {
                trigger = trigger.ToString(),
                from = change.From.ToContractString(),
                to = change.To.ToContractString(),
            });

            var result = await _store.TransitionAsync(new TransitionTaskRequest
            {
                TaskId = taskId,
                Role = role,
                ExpectedState = snapshot.State,
                ExpectedSeq = snapshot.StateSeq,
                NextState = change.To,
                EventType = EventType(change),
                EventPayloadJson = payload,
                Now = now,
                LeaseExpiresAt = options.LeaseExpiresAt,
                FinishedAt = options.FinishedAt,
                CancelRequested = options.CancelRequested,
                ExecutorRef = options.ExecutorRef,
                ResultJson = options.ResultJson,
                NextAction = options.NextAction,
            }, cancellationToken).ConfigureAwait(false);

            if (result.Status == TransitionStatus.Applied)
            {
                return result;
            }
        }

        return new TransitionResult(TransitionStatus.Conflict, _store.Get(taskId, role)?.StateSeq ?? 0);
    }

    private static string EventType(TaskStateChange change) => (change.From, change.To) switch
    {
        (TaskState.Received, TaskState.Queued) => "task.queued",
        (TaskState.Received, TaskState.AwaitingLocalApproval) => "task.awaiting_approval",
        (TaskState.Queued, TaskState.Running) => "task.started",
        (TaskState.Queued, TaskState.Failed) => "task.failed",
        (TaskState.Running, TaskState.Completed) => "task.completed",
        (TaskState.Running, TaskState.ReviewRequired) => "task.review_required",
        (TaskState.Running, TaskState.Failed) => "task.failed",
        (TaskState.Running, TaskState.Cancelling) => "task.cancel_requested",
        (_, TaskState.Cancelled) => "task.cancelled",
        (_, TaskState.Interrupted) => "task.interrupted",
        _ => "task.state_changed",
    };
}
