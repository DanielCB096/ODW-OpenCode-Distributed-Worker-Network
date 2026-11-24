using Odw.Core.Model;

namespace Odw.Node.Persistence;

/// <summary>Acciones remotas durables (docs/PERSISTENCE.md §5.1).</summary>
public static class OutboxActions
{
    public const string SendTask = "send_task";
    public const string PollTask = "poll_task";
    public const string CancelTask = "cancel_task";
    public const string FetchResult = "fetch_result";
    public const string NotifyApproval = "notify_approval";
}

public static class OutboxStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Done = "done";
    public const string Dead = "dead";
}

public sealed record NewPendingAction(
    string Action,
    string? TaskId,
    TaskRole? Role,
    string PayloadJson,
    DateTimeOffset NextAttemptAt);

public sealed record PendingAction(
    long Id,
    string Action,
    string? TaskId,
    TaskRole? Role,
    string PayloadJson,
    string Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
