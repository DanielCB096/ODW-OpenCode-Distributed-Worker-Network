using System.Text.Json;
using Odw.Core.Model;
using Odw.Mcp.Abstractions;
using Odw.Node.Persistence.Tasks;

namespace Odw.Node.Persistence.Queries;

/// <summary>Vistas de tarea para la fachada MCP, sobre el <see cref="TaskStore"/>.</summary>
public sealed class SqliteTaskQuery : ITaskQuery
{
    private readonly TaskStore _store;

    public SqliteTaskQuery(TaskStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<TaskStatusView?> GetStatusAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = Find(taskId);
        if (snapshot is null)
        {
            return Task.FromResult<TaskStatusView?>(null);
        }

        var (percent, phase) = ReadProgress(snapshot.TaskId, snapshot.Role);

        var view = new TaskStatusView(
            snapshot.TaskId,
            snapshot.Role,
            snapshot.State,
            snapshot.StateSeq,
            snapshot.CounterpartNode ?? string.Empty,
            snapshot.UpdatedAt,
            snapshot.LeaseExpiresAt,
            phase,
            percent,
            NextAction(snapshot.State),
            HasResult(snapshot.State),
            snapshot.CancelRequested);

        return Task.FromResult<TaskStatusView?>(view);
    }

    public Task<TaskResultView?> GetResultAsync(string taskId, bool includeEvents, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = Find(taskId);
        if (snapshot is null)
        {
            return Task.FromResult<TaskResultView?>(null);
        }

        var details = ParseResult(snapshot.ResultJson);
        var available = HasResult(snapshot.State);
        var outcome = details.Outcome
                      ?? (snapshot.State == TaskState.ReviewRequired
                          ? TaskOutcome.ReviewRequired
                          : FromState(snapshot.State));

        var events = includeEvents
            ? _store.GetEvents(snapshot.TaskId, snapshot.Role)
                .Select(item => new TaskEventView(item.Seq, item.Type, item.CreatedAt, item.PayloadJson))
                .ToArray()
            : [];

        var view = new TaskResultView(
            snapshot.TaskId,
            snapshot.State,
            outcome,
            details.Summary ?? string.Empty,
            details.ChangedPaths,
            details.Branch,
            details.Commit,
            details.Artifacts,
            details.RequiresReview || outcome == TaskOutcome.ReviewRequired,
            available,
            details.Diff,
            details.DiffTruncated,
            events,
            details.ModelProfile,
            details.ModelProvider,
            details.ModelId,
            details.CostUsd);

        return Task.FromResult<TaskResultView?>(view);
    }

    private TaskSnapshot? Find(string taskId)
        => _store.Get(taskId, TaskRole.Origin) ?? _store.Get(taskId, TaskRole.Target);

    private static bool HasResult(TaskState state)
        => state is TaskState.Completed or TaskState.ReviewRequired or TaskState.Done or TaskState.Failed;

    private static string NextAction(TaskState state) => state switch
    {
        TaskState.WaitingApproval or TaskState.AwaitingLocalApproval => "wait_for_human",
        TaskState.Unreachable => "retry_later",
        TaskState.Done or TaskState.Completed or TaskState.ReviewRequired => "read_result",
        _ => "none",
    };

    private static TaskOutcome FromState(TaskState state) => state switch
    {
        TaskState.ReviewRequired => TaskOutcome.ReviewRequired,
        TaskState.Failed => TaskOutcome.Failed,
        TaskState.Cancelled => TaskOutcome.Cancelled,
        _ => TaskOutcome.Succeeded,
    };

    private (double? Percent, string? Phase) ReadProgress(string taskId, TaskRole role)
    {
        var progress = _store.GetEvents(taskId, role)
            .LastOrDefault(item => item.Type == "task.progress");

        if (progress is null)
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(progress.PayloadJson);
            var root = document.RootElement;
            var percent = root.TryGetProperty("percent", out var p) && p.TryGetDouble(out var value) ? value : (double?)null;
            var phase = root.TryGetProperty("phase", out var f) && f.ValueKind == JsonValueKind.String
                ? f.GetString()
                : null;
            return (percent, phase);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private sealed record ResultDetails(
        TaskOutcome? Outcome,
        string? Summary,
        IReadOnlyList<string> ChangedPaths,
        string? Branch,
        string? Commit,
        IReadOnlyList<string> Artifacts,
        string? Diff,
        bool DiffTruncated,
        bool RequiresReview,
        string? ModelProfile,
        string? ModelProvider,
        string? ModelId,
        decimal? CostUsd);

    private static ResultDetails ParseResult(string? json)
    {
        var empty = new ResultDetails(null, null, [], null, null, [], null, false, false, null, null, null, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return empty;
            }

            return new ResultDetails(
                ParseOutcome(root),
                root.TryGetProperty("summary_md", out var summary) && summary.ValueKind == JsonValueKind.String
                    ? summary.GetString()
                    : null,
                GetStrings(root, "changed_paths"),
                root.TryGetProperty("branch", out var branch) && branch.ValueKind == JsonValueKind.String
                    ? branch.GetString()
                    : null,
                root.TryGetProperty("commit", out var commit) && commit.ValueKind == JsonValueKind.String
                    ? commit.GetString()
                    : null,
                ReadArtifacts(root),
                root.TryGetProperty("diff", out var diff) && diff.ValueKind == JsonValueKind.String
                    ? diff.GetString()
                    : null,
                root.TryGetProperty("diff_truncated", out var truncated)
                && truncated.ValueKind is JsonValueKind.True or JsonValueKind.False
                && truncated.GetBoolean(),
                root.TryGetProperty("requires_review", out var review)
                && review.ValueKind is JsonValueKind.True or JsonValueKind.False
                && review.GetBoolean(),
                root.TryGetProperty("model_profile", out var modelProfile)
                && modelProfile.ValueKind == JsonValueKind.String
                    ? modelProfile.GetString()
                    : null,
                root.TryGetProperty("model_provider", out var modelProvider)
                && modelProvider.ValueKind == JsonValueKind.String
                    ? modelProvider.GetString()
                    : null,
                root.TryGetProperty("model_id", out var modelId)
                && modelId.ValueKind == JsonValueKind.String
                    ? modelId.GetString()
                    : null,
                root.TryGetProperty("cost_usd", out var costUsd)
                && costUsd.ValueKind == JsonValueKind.Number
                && costUsd.TryGetDecimal(out var cost)
                    ? cost
                    : null);
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static TaskOutcome? ParseOutcome(JsonElement root)
    {
        if (!root.TryGetProperty("outcome", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString() switch
        {
            "succeeded" => TaskOutcome.Succeeded,
            "failed" => TaskOutcome.Failed,
            "review_required" => TaskOutcome.ReviewRequired,
            "cancelled" => TaskOutcome.Cancelled,
            _ => null,
        };
    }

    private static IReadOnlyList<string> GetStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadArtifacts(JsonElement root)
    {
        if (!root.TryGetProperty("artifacts", out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var names = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                names.Add(item.GetString()!);
            }
            else if (item.ValueKind == JsonValueKind.Object
                     && item.TryGetProperty("name", out var name)
                     && name.ValueKind == JsonValueKind.String)
            {
                names.Add(name.GetString()!);
            }
        }

        return names;
    }
}
