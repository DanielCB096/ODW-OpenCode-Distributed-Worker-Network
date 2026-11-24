namespace Odw.Core.Model;

/// <summary>Disparadores de transición. Se traducen desde eventos de red, política, humano o scheduler.</summary>
public enum TaskTrigger
{
    // Política
    PolicyAuto,
    PolicyAsk,
    PolicyDeny,

    // Aprobación humana
    ApprovalGranted,
    ApprovalDenied,
    ApprovalExpired,

    // Usuario / operador
    UserCancel,
    CancelTimeout,
    OperatorInspect,
    OperatorDiscard,

    // Transporte
    TransportSent,
    TransportAccepted,
    TransportRejected,
    TransportFailed,

    // Progreso y leases
    TaskProgress,
    LeaseExpired,
    TaskExpired,

    // Resultado
    TaskCompleted,
    TaskFailed,
    TaskRejected,
    TaskCancelled,

    // Destino
    SlotAvailable,
    SetupFailed,
    SchedulerRejected,
    ServiceRestart,
}
