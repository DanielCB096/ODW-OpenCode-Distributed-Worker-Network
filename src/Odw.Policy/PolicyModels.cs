using Odw.Core.Contracts;

namespace Odw.Policy;

/// <summary>Documento de política validado (docs/PLANNER-POLICY.md §1).</summary>
public sealed record PolicyDocument(
    int Version,
    PolicyEffect Default,
    IReadOnlyList<PolicyRule> Rules);

public sealed record PolicyRule(
    string Id,
    int Priority,
    PolicyEffect Effect,
    string Reason,
    PolicyWhen When);

/// <summary>Condiciones de una regla. Todas las claves presentes deben cumplirse (AND).</summary>
public sealed record PolicyWhen
{
    public string? Direction { get; init; }

    public IReadOnlyCollection<string>? Kind { get; init; }

    public IReadOnlyCollection<string>? Mode { get; init; }

    public IReadOnlyCollection<string>? Peer { get; init; }

    public IReadOnlyCollection<string>? PeerTrust { get; init; }

    public IReadOnlyCollection<string>? Repo { get; init; }

    public bool? Parent { get; init; }

    public IReadOnlyCollection<string>? AnyAction { get; init; }

    public IReadOnlyCollection<string>? CapabilitiesAll { get; init; }

    public IReadOnlyCollection<string>? CapabilitiesNone { get; init; }

    public LimitsWhen? Limits { get; init; }

    public TimeWindowWhen? TimeWindow { get; init; }
}

public sealed record LimitsWhen
{
    public RangeWhen? MaxMinutes { get; init; }

    public RangeWhen? MaxMemoryGb { get; init; }

    public RangeWhen? MaxCostUsd { get; init; }

    public string? Network { get; init; }
}

public sealed record RangeWhen(double? Lte, double? Gte)
{
    public bool Contains(double value)
        => (Lte is null || value <= Lte.Value) && (Gte is null || value >= Gte.Value);
}

public sealed record TimeWindowWhen(
    IReadOnlyCollection<int> Days,
    string From,
    string To,
    string? TimeZone);

public sealed record PolicyIssue(string Code, string Path, string Message);

public sealed class PolicyValidationException : Exception
{
    public PolicyValidationException(IReadOnlyList<PolicyIssue> issues)
        : base("Documento de política inválido: " + string.Join("; ", issues.Select(issue => $"{issue.Path}: {issue.Message}")))
    {
        Issues = issues;
    }

    public IReadOnlyList<PolicyIssue> Issues { get; }
}
