using Odw.Core.Model;

namespace Odw.Core.StateMachines;

/// <summary>
/// Máquinas de estado de origen y destino (docs/DOMAIN-MODEL.md §6).
/// Gana la primera regla cuya guarda se cumple; si hay reglas pero ninguna pasa, se informa
/// el motivo de la primera guarda fallida.
/// </summary>
public static class TaskStateMachine
{
    private sealed record Rule(
        TaskRole Role,
        TaskState From,
        TaskTrigger Trigger,
        TaskState To,
        Func<TransitionContext, bool>? Guard = null,
        string? GuardFailure = null);

    private static readonly Rule[] Rules =
    [
        // ---------- Origen ----------
        new(TaskRole.Origin, TaskState.Proposed, TaskTrigger.PolicyAuto, TaskState.Approved),
        new(TaskRole.Origin, TaskState.Proposed, TaskTrigger.PolicyAsk, TaskState.WaitingApproval),
        new(TaskRole.Origin, TaskState.Proposed, TaskTrigger.PolicyDeny, TaskState.Rejected),
        new(TaskRole.Origin, TaskState.Proposed, TaskTrigger.UserCancel, TaskState.Cancelled),

        new(TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.ApprovalGranted, TaskState.Approved,
            context => context.ApprovalMatches && !context.ApprovalExpired, "approval_mismatch"),
        new(TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.ApprovalDenied, TaskState.Rejected),
        new(TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.ApprovalExpired, TaskState.Expired,
            context => context.ApprovalExpired, "approval_not_expired"),
        new(TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.UserCancel, TaskState.Cancelled),

        new(TaskRole.Origin, TaskState.Approved, TaskTrigger.TransportSent, TaskState.Sent),
        new(TaskRole.Origin, TaskState.Approved, TaskTrigger.ApprovalExpired, TaskState.Expired,
            context => context.ApprovalExpired, "approval_not_expired"),

        new(TaskRole.Origin, TaskState.Sent, TaskTrigger.TransportAccepted, TaskState.Accepted),
        new(TaskRole.Origin, TaskState.Sent, TaskTrigger.TransportRejected, TaskState.Rejected),
        new(TaskRole.Origin, TaskState.Sent, TaskTrigger.TransportFailed, TaskState.Unreachable),
        new(TaskRole.Origin, TaskState.Sent, TaskTrigger.TaskInterrupted, TaskState.Interrupted),

        new(TaskRole.Origin, TaskState.Accepted, TaskTrigger.TaskProgress, TaskState.Running),
        new(TaskRole.Origin, TaskState.Accepted, TaskTrigger.TaskRejected, TaskState.Rejected),
        new(TaskRole.Origin, TaskState.Accepted, TaskTrigger.TaskInterrupted, TaskState.Interrupted),
        new(TaskRole.Origin, TaskState.Accepted, TaskTrigger.LeaseExpired, TaskState.Unreachable,
            context => context.LeaseExpired, "lease_not_expired"),

        new(TaskRole.Origin, TaskState.Running, TaskTrigger.TaskCompleted, TaskState.Done),
        new(TaskRole.Origin, TaskState.Running, TaskTrigger.TaskFailed, TaskState.Failed),
        new(TaskRole.Origin, TaskState.Running, TaskTrigger.TaskInterrupted, TaskState.Interrupted),
        new(TaskRole.Origin, TaskState.Running, TaskTrigger.UserCancel, TaskState.Cancelling),
        new(TaskRole.Origin, TaskState.Running, TaskTrigger.LeaseExpired, TaskState.Unreachable,
            context => context.LeaseExpired, "lease_not_expired"),

        new(TaskRole.Origin, TaskState.Unreachable, TaskTrigger.TaskProgress, TaskState.Running),
        new(TaskRole.Origin, TaskState.Unreachable, TaskTrigger.TaskCompleted, TaskState.Done),
        new(TaskRole.Origin, TaskState.Unreachable, TaskTrigger.TaskFailed, TaskState.Failed),
        new(TaskRole.Origin, TaskState.Unreachable, TaskTrigger.TaskInterrupted, TaskState.Interrupted),
        new(TaskRole.Origin, TaskState.Unreachable, TaskTrigger.UserCancel, TaskState.Cancelling),
        new(TaskRole.Origin, TaskState.Unreachable, TaskTrigger.TaskExpired, TaskState.Expired),

        new(TaskRole.Origin, TaskState.Interrupted, TaskTrigger.TaskCompleted, TaskState.Done),
        new(TaskRole.Origin, TaskState.Interrupted, TaskTrigger.TaskFailed, TaskState.Failed),
        new(TaskRole.Origin, TaskState.Interrupted, TaskTrigger.TaskRejected, TaskState.Rejected),
        new(TaskRole.Origin, TaskState.Interrupted, TaskTrigger.TaskCancelled, TaskState.Cancelled),
        new(TaskRole.Origin, TaskState.Interrupted, TaskTrigger.UserCancel, TaskState.Cancelled),

        new(TaskRole.Origin, TaskState.Cancelling, TaskTrigger.TaskCancelled, TaskState.Cancelled),
        new(TaskRole.Origin, TaskState.Cancelling, TaskTrigger.TaskCompleted, TaskState.Done),
        new(TaskRole.Origin, TaskState.Cancelling, TaskTrigger.TaskFailed, TaskState.Failed),
        new(TaskRole.Origin, TaskState.Cancelling, TaskTrigger.CancelTimeout, TaskState.Cancelled),

        // ---------- Destino ----------
        new(TaskRole.Target, TaskState.Received, TaskTrigger.PolicyAuto, TaskState.Queued),
        new(TaskRole.Target, TaskState.Received, TaskTrigger.PolicyAsk, TaskState.AwaitingLocalApproval),
        new(TaskRole.Target, TaskState.Received, TaskTrigger.PolicyDeny, TaskState.Rejected),

        new(TaskRole.Target, TaskState.AwaitingLocalApproval, TaskTrigger.ApprovalGranted, TaskState.Queued,
            context => context.ApprovalMatches && !context.ApprovalExpired, "approval_mismatch"),
        new(TaskRole.Target, TaskState.AwaitingLocalApproval, TaskTrigger.ApprovalDenied, TaskState.Rejected),
        new(TaskRole.Target, TaskState.AwaitingLocalApproval, TaskTrigger.ApprovalExpired, TaskState.Rejected),

        new(TaskRole.Target, TaskState.Queued, TaskTrigger.SlotAvailable, TaskState.Running,
            context => context.ResourcesAvailable, "resources_insufficient"),
        new(TaskRole.Target, TaskState.Queued, TaskTrigger.SetupFailed, TaskState.Failed),
        new(TaskRole.Target, TaskState.Queued, TaskTrigger.SchedulerRejected, TaskState.Rejected),
        new(TaskRole.Target, TaskState.Queued, TaskTrigger.UserCancel, TaskState.Cancelled),

        new(TaskRole.Target, TaskState.Running, TaskTrigger.TaskCompleted, TaskState.ReviewRequired,
            context => context.ValidationViolated, "validation_clean"),
        new(TaskRole.Target, TaskState.Running, TaskTrigger.TaskCompleted, TaskState.Completed,
            context => !context.ValidationViolated, "validation_violated"),
        new(TaskRole.Target, TaskState.Running, TaskTrigger.TaskFailed, TaskState.Failed),
        new(TaskRole.Target, TaskState.Running, TaskTrigger.UserCancel, TaskState.Cancelling),
        new(TaskRole.Target, TaskState.Running, TaskTrigger.ServiceRestart, TaskState.Interrupted),

        new(TaskRole.Target, TaskState.Cancelling, TaskTrigger.TaskCancelled, TaskState.Cancelled),
        new(TaskRole.Target, TaskState.Cancelling, TaskTrigger.TaskCompleted, TaskState.Completed),

        new(TaskRole.Target, TaskState.Interrupted, TaskTrigger.OperatorInspect, TaskState.ReviewRequired),
        new(TaskRole.Target, TaskState.Interrupted, TaskTrigger.OperatorDiscard, TaskState.Failed),
    ];

    private static readonly Dictionary<(TaskRole Role, TaskState From, TaskTrigger Trigger), Rule[]> Index =
        Rules
            .GroupBy(rule => (rule.Role, rule.From, rule.Trigger))
            .ToDictionary(group => group.Key, group => group.ToArray());

    private static readonly TaskState[] OriginStateList =
    [
        TaskState.Proposed, TaskState.WaitingApproval, TaskState.Approved, TaskState.Sent,
        TaskState.Accepted, TaskState.Running, TaskState.Unreachable, TaskState.Cancelling,
        TaskState.Done, TaskState.Failed, TaskState.Rejected, TaskState.Cancelled, TaskState.Expired,
        TaskState.Interrupted,
    ];

    private static readonly TaskState[] TargetStateList =
    [
        TaskState.Received, TaskState.AwaitingLocalApproval, TaskState.Queued, TaskState.Running,
        TaskState.Cancelling, TaskState.Completed, TaskState.ReviewRequired, TaskState.Failed,
        TaskState.Rejected, TaskState.Cancelled, TaskState.Interrupted,
    ];

    public static IReadOnlyList<TaskState> StatesFor(TaskRole role)
        => role == TaskRole.Origin ? OriginStateList : TargetStateList;

    public static TaskState InitialState(TaskRole role)
        => role == TaskRole.Origin ? TaskState.Proposed : TaskState.Received;

    public static bool TryTransition(
        TaskRole role,
        TaskState from,
        TaskTrigger trigger,
        TransitionContext context,
        out TaskState to,
        out string? failure)
    {
        ArgumentNullException.ThrowIfNull(context);

        to = from;
        failure = null;

        if (!Index.TryGetValue((role, from, trigger), out var candidates))
        {
            failure = "transition_not_allowed";
            return false;
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Guard is null || candidate.Guard(context))
            {
                to = candidate.To;
                return true;
            }
        }

        failure = candidates[0].GuardFailure ?? "guard_failed";
        return false;
    }
}
