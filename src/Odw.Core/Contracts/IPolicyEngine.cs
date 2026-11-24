using Odw.Core.Model;

namespace Odw.Core.Contracts;

public enum PolicyEffect
{
    Auto,
    Ask,
    Deny,
}

/// <summary>Entrada completa de una evaluación: nunca se consulta el reloj dentro del motor.</summary>
public sealed record PolicyRequest(
    TaskRole Role,
    string PeerNodeId,
    string PeerTrust,
    TaskRequest Task,
    IReadOnlyCollection<string> PeerCapabilities,
    DateTimeOffset Now)
{
    /// <summary>
    /// Acciones de herramienta a evaluar (por ejemplo <c>shell</c>). Vacío para una evaluación
    /// de tarea. Permite reglas con <c>any_action</c> sin acoplar el motor al ejecutor.
    /// </summary>
    public IReadOnlyCollection<string> Actions { get; init; } = [];
}

public sealed record PolicyDecision(PolicyEffect Effect, string MatchedRuleId, string Reason)
{
    public bool IsAllowed => Effect != PolicyEffect.Deny;

    public bool RequiresHuman => Effect == PolicyEffect.Ask;
}

/// <summary>Motor de reglas ordenadas, primera coincidencia (docs/PLANNER-POLICY.md §1).</summary>
public interface IPolicyEngine
{
    PolicyDecision Evaluate(PolicyRequest request);
}
