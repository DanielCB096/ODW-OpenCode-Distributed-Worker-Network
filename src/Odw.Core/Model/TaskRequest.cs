namespace Odw.Core.Model;

/// <summary>Contrato de tarea v1 (docs/DOMAIN-MODEL.md §2). Inmutable.</summary>
public sealed record TaskRequest
{
    public int ProtocolVersion { get; init; } = 1;

    public required string TaskId { get; init; }

    public required string OriginNode { get; init; }

    public required string TargetNode { get; init; }

    public string? ParentTaskId { get; init; }

    public Lineage Lineage { get; init; } = new();

    public TaskKind Kind { get; init; }

    public required string Instructions { get; init; }

    public WorkspaceSpec Workspace { get; init; } = new() { Repo = string.Empty };

    public TaskLimits Limits { get; init; } = new();

    public ExecutorSpec Executor { get; init; } = new();

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public ApprovalEvidence? Approval { get; init; }
}

public sealed record Lineage
{
    public IReadOnlyList<string> Hops { get; init; } = [];

    public int MaxDepth { get; init; } = 1;
}

public sealed record WorkspaceSpec
{
    public required string Repo { get; init; }

    public string BaseRef { get; init; } = "main";

    public WorkspaceMode Mode { get; init; } = WorkspaceMode.ReadOnly;

    public WorkspaceStrategy Strategy { get; init; } = WorkspaceStrategy.Clone;

    public IReadOnlyList<string> AllowedPaths { get; init; } = [];

    public IReadOnlyList<string> DeniedPaths { get; init; } = [];
}

public sealed record TaskLimits
{
    public int MaxMinutes { get; init; } = 45;

    public int MaxMemoryGb { get; init; } = 12;

    public int MaxProcesses { get; init; } = 64;

    public NetworkAccess Network { get; init; } = NetworkAccess.None;

    public decimal MaxCostUsd { get; init; } = 5m;
}

public sealed record ExecutorSpec
{
    public string Type { get; init; } = "opencode";

    public string? ModelHint { get; init; }
}

public sealed record ApprovalEvidence
{
    public required string ApprovalId { get; init; }

    public required string TaskHash { get; init; }

    public required string ApprovedBy { get; init; }

    public required DateTimeOffset ApprovedAt { get; init; }

    public required string Nonce { get; init; }

    public required string Signature { get; init; }
}
