namespace Odw.Core.Model;

/// <summary>Resultado de una tarea (docs/DOMAIN-MODEL.md §3).</summary>
public sealed record TaskResult
{
    public int ProtocolVersion { get; init; } = 1;

    public required string TaskId { get; init; }

    public required TaskState State { get; init; }

    public required TaskOutcome Outcome { get; init; }

    public string SummaryMarkdown { get; init; } = string.Empty;

    public IReadOnlyList<string> ChangedPaths { get; init; } = [];

    public string? Branch { get; init; }

    public string? Commit { get; init; }

    public string? DiffSha256 { get; init; }

    public IReadOnlyList<TestResult> Tests { get; init; } = [];

    public IReadOnlyList<ArtifactRef> Artifacts { get; init; } = [];

    public TaskUsage? Usage { get; init; }

    public string? ModelUsed { get; init; }

    public TaskError? Error { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required DateTimeOffset FinishedAt { get; init; }
}

public sealed record TestResult(string Name, TestStatus Status, int? DurationMs = null);

public sealed record ArtifactRef(string Name, long Bytes, string Sha256, string ContentType);

public sealed record TaskUsage(long TokensIn, long TokensOut, decimal CostUsd);

public sealed record TaskError(string Code, string Message, bool Retryable = false);
