using System.Globalization;
using Odw.Core.Contracts;
using Odw.Core.Model;

namespace Odw.Policy;

/// <summary>
/// Motor determinista: reglas ordenadas por prioridad y, en empate, por orden de declaración.
/// Gana la primera coincidencia; nunca usa el reloj del sistema (recibe <c>Now</c>).
/// </summary>
public sealed class PolicyEngine : IPolicyEngine
{
    private readonly Func<string, TimeZoneInfo> _zoneResolver;
    private readonly PolicyRule[] _ordered;

    public PolicyEngine(PolicyDocument document, Func<string, TimeZoneInfo>? zoneResolver = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _zoneResolver = zoneResolver ?? TimeZoneInfo.FindSystemTimeZoneById;

        _ordered = document.Rules
            .Select((rule, index) => (Rule: rule, Index: index))
            .OrderBy(item => item.Rule.Priority)
            .ThenBy(item => item.Index)
            .Select(item => item.Rule)
            .ToArray();
    }

    public PolicyDocument Document { get; }

    public static PolicyEngine FromFile(string path, Func<string, TimeZoneInfo>? zoneResolver = null)
        => new(PolicyDocumentLoader.LoadFile(path, zoneResolver), zoneResolver);

    public static PolicyEngine FromJson(string json, Func<string, TimeZoneInfo>? zoneResolver = null)
        => new(PolicyDocumentLoader.Load(json, zoneResolver), zoneResolver);

    public PolicyDecision Evaluate(PolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        foreach (var rule in _ordered)
        {
            if (Matches(rule.When, request))
            {
                return new PolicyDecision(rule.Effect, rule.Id, rule.Reason);
            }
        }

        return new PolicyDecision(
            Document.Default,
            "default",
            "Ninguna regla coincide; se aplica el valor por defecto.");
    }

    private bool Matches(PolicyWhen when, PolicyRequest request)
    {
        if (when.Direction is not null
            && !string.Equals(when.Direction, Direction(request.Role), StringComparison.Ordinal))
        {
            return false;
        }

        if (!MatchesValue(when.Kind, request.Task.Kind.ToWireValue(), StringComparison.Ordinal)
            || !MatchesValue(when.Mode, request.Task.Workspace.Mode.ToWireValue(), StringComparison.Ordinal))
        {
            return false;
        }

        if (!MatchesValue(when.Peer, request.PeerNodeId, StringComparison.Ordinal)
            || !MatchesValue(when.PeerTrust, request.PeerTrust, StringComparison.OrdinalIgnoreCase)
            || !MatchesValue(when.Repo, request.Task.Workspace.Repo, StringComparison.Ordinal))
        {
            return false;
        }

        if (when.Parent is { } parent
            && parent != (request.Task.ParentTaskId is not null))
        {
            return false;
        }

        if (!MatchesCapabilities(when.CapabilitiesAll, when.CapabilitiesNone, request.PeerCapabilities))
        {
            return false;
        }

        if (when.AnyAction is { } requiredActions)
        {
            if (requiredActions.Count == 0
                || !request.Actions.Any(action => requiredActions.Contains(action, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (!MatchesLimits(when.Limits, request.Task.Limits))
        {
            return false;
        }

        if (when.TimeWindow is { } window && !MatchesWindow(window, request.Now))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesValue(
        IReadOnlyCollection<string>? allowed,
        string value,
        StringComparison comparison)
        => allowed is null || allowed.Any(item => string.Equals(item, value, comparison));

    private static bool MatchesCapabilities(
        IReadOnlyCollection<string>? all,
        IReadOnlyCollection<string>? none,
        IReadOnlyCollection<string> capabilities)
    {
        if (all is { Count: > 0 }
            && !all.All(required => capabilities.Contains(required, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (none is { Count: > 0 }
            && none.Any(forbidden => capabilities.Contains(forbidden, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesLimits(LimitsWhen? limits, TaskLimits taskLimits)
    {
        if (limits is null)
        {
            return true;
        }

        if (limits.MaxMinutes is { } minutes && !minutes.Contains(taskLimits.MaxMinutes))
        {
            return false;
        }

        if (limits.MaxMemoryGb is { } memory && !memory.Contains(taskLimits.MaxMemoryGb))
        {
            return false;
        }

        if (limits.MaxCostUsd is { } cost && !cost.Contains((double)taskLimits.MaxCostUsd))
        {
            return false;
        }

        if (limits.Network is not null
            && !string.Equals(limits.Network, taskLimits.Network.ToWireValue(), StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private bool MatchesWindow(TimeWindowWhen window, DateTimeOffset now)
    {
        var zone = window.TimeZone is null ? TimeZoneInfo.Local : _zoneResolver(window.TimeZone);
        var local = TimeZoneInfo.ConvertTime(now, zone);

        var day = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        var time = TimeOnly.FromDateTime(local.DateTime);
        var from = TimeOnly.ParseExact(window.From, "HH:mm", CultureInfo.InvariantCulture);
        var to = TimeOnly.ParseExact(window.To, "HH:mm", CultureInfo.InvariantCulture);

        if (from <= to)
        {
            return window.Days.Contains(day) && time >= from && time < to;
        }

        // Ventana nocturna: empieza un día y termina al siguiente.
        var previousDay = day == 1 ? 7 : day - 1;
        return (window.Days.Contains(day) && time >= from)
               || (window.Days.Contains(previousDay) && time < to);
    }

    private static string Direction(TaskRole role)
        => role == TaskRole.Origin ? "out" : "in";
}
