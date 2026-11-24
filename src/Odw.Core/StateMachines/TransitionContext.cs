namespace Odw.Core.StateMachines;

/// <summary>
/// Hechos que condicionan una transición. Se construyen desde la persistencia o desde el
/// evento recibido; nunca desde texto libre.
/// </summary>
public sealed record TransitionContext
{
    public bool ApprovalMatches { get; init; }

    public bool ApprovalExpired { get; init; }

    public bool LeaseExpired { get; init; }

    public bool ResourcesAvailable { get; init; } = true;

    public bool ValidationViolated { get; init; }

    public static TransitionContext Default { get; } = new();

    /// <summary>Contexto con todas las guardas de éxito satisfechas (para pruebas y simulación).</summary>
    public static TransitionContext Satisfied { get; } = new()
    {
        ApprovalMatches = true,
        ApprovalExpired = false,
        LeaseExpired = true,
        ResourcesAvailable = true,
        ValidationViolated = false,
    };
}
