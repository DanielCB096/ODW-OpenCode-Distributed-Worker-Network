using Odw.Core.Model;

namespace Odw.Core.StateMachines;

public sealed record TaskStateChange(
    string TaskId,
    TaskRole Role,
    long Seq,
    TaskState From,
    TaskState To,
    TaskTrigger Trigger,
    DateTimeOffset At);

public sealed class InvalidStateTransitionException : InvalidOperationException
{
    public InvalidStateTransitionException(
        string taskId,
        TaskRole role,
        TaskState from,
        TaskTrigger trigger,
        string? failure)
        : base($"Transición inválida para {taskId} ({role}): {from} + {trigger} ({failure ?? "not_allowed"}).")
    {
        TaskId = taskId;
        Role = role;
        From = from;
        Trigger = trigger;
        Failure = failure;
    }

    public string TaskId { get; }

    public TaskRole Role { get; }

    public TaskState From { get; }

    public TaskTrigger Trigger { get; }

    public string? Failure { get; }
}
