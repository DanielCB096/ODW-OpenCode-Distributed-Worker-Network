using Odw.Core.Model;

namespace Odw.Node.Persistence.Tasks;

public sealed record TaskSnapshot(
    string TaskId,
    TaskRole Role,
    string? CounterpartNode,
    string? ParentTaskId,
    TaskKind Kind,
    WorkspaceMode Mode,
    TaskState State,
    long StateSeq,
    string TaskHash,
    string RequestJson,
    string OriginNode,
    string TargetNode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LeaseExpiresAt,
    bool CancelRequested,
    int Attempts,
    string? WorkspacePath,
    string? ExecutorRef,
    string? ResultJson,
    DateTimeOffset? FinishedAt);

public sealed record TaskEventRecord(
    long Seq,
    string EventId,
    string Type,
    string PayloadJson,
    DateTimeOffset CreatedAt,
    string Hash);

public sealed record CreateTaskRequest
{
    public required string TaskId { get; init; }

    public required TaskRole Role { get; init; }

    public string? CounterpartNode { get; init; }

    public string? ParentTaskId { get; init; }

    public required TaskKind Kind { get; init; }

    public required WorkspaceMode Mode { get; init; }

    public required TaskState State { get; init; }

    public required string TaskHash { get; init; }

    public required string RequestJson { get; init; }

    public required string OriginNode { get; init; }

    public required string TargetNode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? LeaseExpiresAt { get; init; }

    public string? WorkspacePath { get; init; }

    public string? ExecutorRef { get; init; }

    public string InitialEventType { get; init; } = "task.created";

    public string InitialEventPayloadJson { get; init; } = "{}";

    public NewPendingAction? NextAction { get; init; }
}

public sealed record TransitionTaskRequest
{
    public required string TaskId { get; init; }

    public required TaskRole Role { get; init; }

    public required TaskState ExpectedState { get; init; }

    public required long ExpectedSeq { get; init; }

    public required TaskState NextState { get; init; }

    public required string EventType { get; init; }

    public string EventPayloadJson { get; init; } = "{}";

    public required DateTimeOffset Now { get; init; }

    public DateTimeOffset? LeaseExpiresAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public bool? CancelRequested { get; init; }

    public string? ExecutorRef { get; init; }

    public string? ResultJson { get; init; }

    public NewPendingAction? NextAction { get; init; }
}

public enum TransitionStatus
{
    Applied,
    Conflict,
    NotFound,
}

public sealed record TransitionResult(TransitionStatus Status, long StateSeq);
