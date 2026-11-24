namespace Odw.Core.Validation;

/// <summary>Códigos de error del protocolo (docs/PROTOCOL.md §3).</summary>
public static class ValidationCodes
{
    public const string ProtocolUnsupported = "PROTOCOL_UNSUPPORTED";
    public const string ContractInvalid = "CONTRACT_INVALID";
    public const string PathInvalid = "PATH_INVALID";
    public const string LimitsInvalid = "LIMITS_INVALID";
    public const string LineageLoop = "LINEAGE_LOOP";
    public const string SelfDelegation = "SELF_DELEGATION";
    public const string ApprovalInvalid = "APPROVAL_INVALID";
    public const string RepoUnknown = "REPO_UNKNOWN";
    public const string TaskIdConflict = "TASK_ID_CONFLICT";
    public const string TaskStateConflict = "TASK_STATE_CONFLICT";
    public const string PolicyDenied = "POLICY_DENIED";
}

public sealed record ValidationIssue(string Code, string Field, string Message);

public sealed class ValidationResult
{
    public ValidationResult(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues;
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public IReadOnlyList<string> Codes
        => Issues.Select(issue => issue.Code).Distinct(StringComparer.Ordinal).ToArray();

    public static ValidationResult Valid { get; } = new([]);
}
