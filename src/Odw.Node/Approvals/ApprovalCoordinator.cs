using Odw.Core.Model;
using Odw.Core.StateMachines;
using Odw.Node.Origins;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Tasks;

namespace Odw.Node.Approvals;

/// <summary>
/// Aplica una decisión humana: transiciona la tarea, envía si se aprobó y consume el nonce.
/// La aprobación está vinculada al `task_hash`; un hash distinto no decide nada.
/// </summary>
public sealed class ApprovalCoordinator
{
    private readonly ApprovalService _approvals;
    private readonly TaskStore _store;
    private readonly TaskCoordinator _coordinator;
    private readonly OriginProposalService _origin;
    private readonly TimeProvider _timeProvider;

    public ApprovalCoordinator(
        ApprovalService approvals,
        TaskStore store,
        TaskCoordinator coordinator,
        OriginProposalService origin,
        TimeProvider timeProvider)
    {
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _origin = origin ?? throw new ArgumentNullException(nameof(origin));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApprovalDecisionResult> ApproveAsync(
        string approvalId,
        string decidedBy,
        CancellationToken cancellationToken = default)
    {
        var record = _approvals.Get(approvalId);
        if (record is null)
        {
            return new ApprovalDecisionResult(ApprovalDecisionStatus.NotFound, null);
        }

        var result = await _approvals.DecideAsync(
            approvalId,
            approved: true,
            decidedBy,
            reason: null,
            expectedTaskHash: record.TaskHash,
            cancellationToken).ConfigureAwait(false);

        if (result.Status != ApprovalDecisionStatus.Approved)
        {
            return result;
        }

        var snapshot = _store.Get(record.TaskId, record.Role);
        if (snapshot is null)
        {
            return result;
        }

        if (snapshot.State == TaskState.WaitingApproval)
        {
            var transition = await _coordinator.TransitionAsync(
                record.TaskId,
                record.Role,
                TaskTrigger.ApprovalGranted,
                new TransitionContext { ApprovalMatches = true, ApprovalExpired = false },
                _timeProvider.GetUtcNow(),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (transition.Status != TransitionStatus.Applied)
            {
                return result;
            }
        }

        if (record.Role == TaskRole.Origin)
        {
            var current = _store.Get(record.TaskId, TaskRole.Origin);
            if (current?.State == TaskState.Approved)
            {
                await _origin.SendApprovedAsync(record.TaskId, cancellationToken).ConfigureAwait(false);
            }

            await _approvals.ConsumeAsync(approvalId, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    public async Task<ApprovalDecisionResult> DenyAsync(
        string approvalId,
        string decidedBy,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var record = _approvals.Get(approvalId);
        if (record is null)
        {
            return new ApprovalDecisionResult(ApprovalDecisionStatus.NotFound, null);
        }

        var result = await _approvals.DecideAsync(
            approvalId,
            approved: false,
            decidedBy,
            reason,
            expectedTaskHash: record.TaskHash,
            cancellationToken).ConfigureAwait(false);

        if (result.Status != ApprovalDecisionStatus.Denied)
        {
            return result;
        }

        var snapshot = _store.Get(record.TaskId, record.Role);
        if (snapshot?.State == TaskState.WaitingApproval)
        {
            await _coordinator.TransitionAsync(
                record.TaskId,
                record.Role,
                TaskTrigger.ApprovalDenied,
                TransitionContext.Default,
                _timeProvider.GetUtcNow(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
