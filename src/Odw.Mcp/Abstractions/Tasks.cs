using Odw.Core.Model;

namespace Odw.Mcp.Abstractions;

public sealed record TaskStatusView(
    string TaskId,
    TaskRole Role,
    TaskState State,
    long StateSeq,
    string CounterpartNode,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LeaseExpiresAt,
    string? Phase,
    double? ProgressPercent,
    string NextAction,
    bool ResultAvailable,
    bool CancelRequested);

public sealed record TaskEventView(
    long Seq,
    string Type,
    DateTimeOffset CreatedAt,
    string PayloadJson);

public sealed record TaskResultView(
    string TaskId,
    TaskState State,
    TaskOutcome Outcome,
    string SummaryMarkdown,
    IReadOnlyList<string> ChangedPaths,
    string? Branch,
    string? Commit,
    IReadOnlyList<string> Artifacts,
    bool RequiresReview,
    bool ResultAvailable,
    IReadOnlyList<TaskEventView> Events);

public interface ITaskQuery
{
    Task<TaskStatusView?> GetStatusAsync(string taskId, CancellationToken cancellationToken);

    Task<TaskResultView?> GetResultAsync(string taskId, bool includeEvents, CancellationToken cancellationToken);
}
