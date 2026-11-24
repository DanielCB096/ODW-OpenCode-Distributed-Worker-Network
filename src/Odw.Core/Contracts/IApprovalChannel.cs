using Odw.Core.Model;

namespace Odw.Core.Contracts;

public enum ApprovalScope
{
    Task,
    ToolUse,
}

public sealed record ApprovalRequest(
    string ApprovalId,
    ApprovalScope Scope,
    string TaskId,
    string TaskHash,
    TaskRole Role,
    string Summary,
    DateTimeOffset ExpiresAt,
    string? Nonce = null);

public enum ApprovalDecisionKind
{
    Approved,
    Denied,
    Expired,
}

public sealed record ApprovalOutcome(
    ApprovalDecisionKind Decision,
    string Approver,
    DateTimeOffset DecidedAt,
    string? Reason = null);

/// <summary>
/// Canal de aprobación fuera de banda (ADR-009). Implementaciones: Telegram y named pipe.
/// No existe —ni debe existir— una implementación HTTP.
/// </summary>
public interface IApprovalChannel
{
    Task<ApprovalOutcome> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken);
}
