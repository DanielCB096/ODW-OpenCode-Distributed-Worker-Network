using Odw.Core.Hashing;
using Odw.Core.Model;

namespace Odw.Core.StateMachines;

/// <summary>
/// Agregado de una tarea para un rol concreto (ADR-010). Mantiene <c>state_seq</c> monótono
/// y registra cada cambio; la persistencia es responsable de escribir el evento asociado.
/// </summary>
public sealed class TaskAggregate
{
    private readonly List<TaskStateChange> _changes = [];

    private TaskAggregate(
        string taskId,
        TaskRole role,
        TaskState state,
        long stateSeq,
        string taskHash,
        DateTimeOffset createdAt)
    {
        TaskId = taskId;
        Role = role;
        State = state;
        StateSeq = stateSeq;
        TaskHash = taskHash;
        CreatedAt = createdAt;
    }

    public string TaskId { get; }

    public TaskRole Role { get; }

    public TaskState State { get; private set; }

    public long StateSeq { get; private set; }

    public string TaskHash { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<TaskStateChange> Changes => _changes;

    public static TaskAggregate CreateOrigin(TaskRequest request, DateTimeOffset? at = null)
        => Create(request, TaskRole.Origin, at);

    public static TaskAggregate CreateTarget(TaskRequest request, DateTimeOffset? at = null)
        => Create(request, TaskRole.Target, at);

    public static TaskAggregate Restore(
        string taskId,
        TaskRole role,
        TaskState state,
        long stateSeq,
        string taskHash,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskHash);

        var expectedRole = role == TaskRole.Origin ? state.IsOriginState() : state.IsTargetState();
        if (!expectedRole)
        {
            throw new ArgumentException($"El estado {state} no pertenece al rol {role}.", nameof(state));
        }

        if (stateSeq < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(stateSeq));
        }

        return new TaskAggregate(taskId, role, state, stateSeq, taskHash, createdAt);
    }

    public bool CanApply(TaskTrigger trigger, TransitionContext context)
        => TaskStateMachine.TryTransition(Role, State, trigger, context, out _, out _);

    public TaskStateChange Apply(TaskTrigger trigger, TransitionContext context, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TaskStateMachine.TryTransition(Role, State, trigger, context, out var next, out var failure))
        {
            throw new InvalidStateTransitionException(TaskId, Role, State, trigger, failure);
        }

        var from = State;
        State = next;
        StateSeq++;

        var change = new TaskStateChange(TaskId, Role, StateSeq, from, next, trigger, at);
        _changes.Add(change);
        return change;
    }

    private static TaskAggregate Create(TaskRequest request, TaskRole role, DateTimeOffset? at)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskId);

        return new TaskAggregate(
            request.TaskId,
            role,
            TaskStateMachine.InitialState(role),
            0,
            Odw.Core.Hashing.TaskHash.Compute(request),
            at ?? request.CreatedAt);
    }
}
