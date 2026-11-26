using Odw.Core.Model;

namespace Odw.Mcp.Abstractions;

public sealed record ProposalInput(
    string TargetNode,
    TaskKind Kind,
    WorkspaceMode Mode,
    string Repo,
    string BaseRef,
    string Instructions,
    IReadOnlyList<string> AllowedPaths,
    IReadOnlyList<string> DeniedPaths,
    int MaxMinutes,
    int MaxMemoryGb,
    int MaxProcesses,
    NetworkAccess Network,
    decimal MaxCostUsd,
    int ExpiresInMinutes = 60,
    string? TaskId = null,
    string? ModelProfile = null);

public sealed record ProposalOutcome(
    string TaskId,
    string State,
    bool ApprovalRequired,
    string TaskHash,
    string NextAction,
    string? ErrorCode = null,
    string? Detail = null,
    string? ApprovalId = null)
{
    public bool IsError => ErrorCode is not null;
}

public interface ITaskProposalService
{
    Task<ProposalOutcome> ProposeAsync(ProposalInput input, CancellationToken cancellationToken);
}
