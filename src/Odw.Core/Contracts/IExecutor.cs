using Odw.Core.Model;

namespace Odw.Core.Contracts;

public enum ToolEffect
{
    Allow,
    Deny,
    Ask,
}

public sealed record ToolRule(string Action, string Resource, ToolEffect Effect);

public sealed record ToolPolicy(IReadOnlyList<ToolRule> Rules)
{
    public static ToolPolicy AllowAll { get; } = new([new ToolRule("*", "*", ToolEffect.Allow)]);

    public static ToolPolicy AskAll { get; } = new([new ToolRule("*", "*", ToolEffect.Ask)]);
}

public sealed record ExecutorLimits(
    int MaxMinutes,
    int MaxMemoryGb,
    int MaxProcesses,
    bool NetworkAllowed,
    decimal MaxCostUsd);

public sealed record ExecutorStartRequest(
    string TaskId,
    string WorkspacePath,
    string Instructions,
    string? ModelHint,
    ExecutorLimits Limits,
    ToolPolicy Policy,
    IReadOnlyDictionary<string, string> Environment);

public sealed record ExecutorSession(
    string SessionId,
    int ProcessId,
    string BaseUrl,
    DateTimeOffset StartedAt);

public enum ExecutorEventKind
{
    Started,
    ToolCalled,
    ToolFailed,
    PermissionAsked,
    TextDelta,
    UsageUpdated,
    Completed,
    Failed,
    Interrupted,
    Heartbeat,
}

public sealed record ExecutorEvent(
    ExecutorEventKind Kind,
    string? Tool = null,
    string? Detail = null,
    DateTimeOffset At = default);

public enum ExecutorOutcomeKind
{
    Succeeded,
    Failed,
    ReviewRequired,
    Interrupted,
    Cancelled,
}

public sealed record ExecutorOutcome(
    ExecutorOutcomeKind Kind,
    string? Code = null,
    string? Message = null);

public sealed record ExecutorSnapshot(
    string State,
    double? ProgressPercent,
    string? Phase,
    DateTimeOffset? LeaseExpiresAt,
    bool IsIdle);

/// <summary>
/// Contrato del ejecutor (docs/OPENCODE-ADAPTER.md §2). La primera implementación es OpenCode.
/// </summary>
public interface IExecutor
{
    Task<ExecutorSession> StartAsync(ExecutorStartRequest request, CancellationToken cancellationToken);

    Task SendAsync(string sessionId, string text, MessageDelivery delivery, CancellationToken cancellationToken);

    IAsyncEnumerable<ExecutorEvent> StreamAsync(string sessionId, CancellationToken cancellationToken);

    Task<ExecutorSnapshot> GetSnapshotAsync(string sessionId, CancellationToken cancellationToken);

    Task<ExecutorOutcome> WaitAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken);

    Task AbortAsync(string sessionId, TimeSpan grace, CancellationToken cancellationToken);

    Task StopAsync(string sessionId, CancellationToken cancellationToken);
}
