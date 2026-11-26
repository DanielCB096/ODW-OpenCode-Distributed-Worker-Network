using System.Net;
using Odw.Core.Contracts;
using Odw.Core.Hashing;
using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Core.StateMachines;
using Odw.Core.Validation;
using Odw.Mcp.Abstractions;
using Odw.Node.Approvals;
using Odw.Node.Contracts;
using Odw.Node.Hosting;
using Odw.Node.Peers;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Tasks;
using Odw.PeerApi;
using Odw.Policy;

namespace Odw.Node.Origins;

/// <summary>
/// Lado origen: valida, aplica política saliente, persiste la tarea y la envía al par.
/// Un `ASK` deja la tarea esperando aprobación humana; no se envía nada.
/// </summary>
public sealed class OriginProposalService : ITaskProposalService
{
    private readonly NodeHostOptions _options;
    private readonly TaskStore _store;
    private readonly TaskCoordinator _coordinator;
    private readonly PeerRegistry _peers;
    private readonly PolicyEngine _policy;
    private readonly PeerApiClientFactory _clients;
    private readonly TimeProvider _timeProvider;
    private readonly ApprovalService _approvals;
    private readonly ApprovalOptions _approvalOptions;

    public OriginProposalService(
        NodeHostOptions options,
        TaskStore store,
        TaskCoordinator coordinator,
        PeerRegistry peers,
        PolicyEngine policy,
        PeerApiClientFactory clients,
        TimeProvider timeProvider,
        ApprovalService approvals,
        ApprovalOptions? approvalOptions = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _peers = peers ?? throw new ArgumentNullException(nameof(peers));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _clients = clients ?? throw new ArgumentNullException(nameof(clients));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _approvalOptions = approvalOptions ?? new ApprovalOptions();
    }

    public async Task<ProposalOutcome> ProposeAsync(
        ProposalInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var registration = _peers.GetRegistration(input.TargetNode);
        if (registration is null)
        {
            return Error(input.TargetNode, "PEER_UNKNOWN", "Par no configurado.");
        }

        var now = _timeProvider.GetUtcNow();
        var taskId = input.TaskId ?? Ulid.New(_timeProvider).ToString();
        var task = Build(input, taskId, now);

        var validation = TaskValidator.Validate(task);
        if (!validation.IsValid)
        {
            var issue = validation.Issues[0];
            return Error(taskId, issue.Code, $"{issue.Field}: {issue.Message}");
        }

        var hash = TaskHash.Compute(task);
        var existing = _store.Get(taskId, TaskRole.Origin);
        if (existing is not null)
        {
            return string.Equals(existing.TaskHash, hash, StringComparison.Ordinal)
                ? Outcome(existing)
                : Error(taskId, ValidationCodes.TaskIdConflict, "El task_id ya existe con otro contenido.");
        }

        var decision = _policy.Evaluate(new PolicyRequest(
            TaskRole.Origin,
            input.TargetNode,
            registration.Trust,
            task,
            registration.Capabilities ?? [],
            now));

        if (decision.Effect == PolicyEffect.Deny)
        {
            return Error(taskId, ValidationCodes.PolicyDenied, decision.Reason);
        }

        var requestJson = TaskContractWriter.Write(task);
        var initialState = decision.Effect == PolicyEffect.Ask
            ? TaskState.WaitingApproval
            : TaskState.Approved;

        var eventType = initialState == TaskState.WaitingApproval
            ? "task.awaiting_approval"
            : "task.proposed";

        var snapshot = await _coordinator.AcceptAsync(
            task,
            TaskRole.Origin,
            requestJson,
            initialState,
            eventType,
            PersistenceJson.Serialize(new
            {
                policy_rule = decision.MatchedRuleId,
                effect = decision.Effect.ToString(),
                peer = input.TargetNode,
            }),
            cancellationToken).ConfigureAwait(false);

        if (initialState == TaskState.WaitingApproval)
        {
            var approval = await _approvals.CreateAsync(
                taskId,
                TaskRole.Origin,
                hash,
                Summary(input),
                _approvalOptions.Ttl,
                cancellationToken).ConfigureAwait(false);

            return Outcome(snapshot, approval.ApprovalId);
        }

        return await SendAsync(snapshot, requestJson, registration, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Envía una tarea que ya fue aprobada por un humano.</summary>
    public async Task<ProposalOutcome> SendApprovedAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        var snapshot = _store.Get(taskId, TaskRole.Origin);
        if (snapshot is null)
        {
            return Error(taskId, "TASK_NOT_FOUND", "La tarea no existe en este nodo.");
        }

        if (snapshot.State != TaskState.Approved)
        {
            return snapshot.State is TaskState.Sent or TaskState.Accepted or TaskState.Running or TaskState.Done
                ? Outcome(snapshot)
                : Error(taskId, "TASK_STATE_CONFLICT", $"La tarea está en {snapshot.State.ToContractString()}.");
        }

        var registration = _peers.GetRegistration(snapshot.TargetNode);
        if (registration is null)
        {
            return Error(taskId, "PEER_UNKNOWN", "Par no configurado.");
        }

        return await SendAsync(snapshot, snapshot.RequestJson, registration, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ProposalOutcome> SendAsync(
        TaskSnapshot snapshot,
        string requestJson,
        PeerRegistration registration,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var sent = await _coordinator.TransitionAsync(
            snapshot.TaskId,
            TaskRole.Origin,
            TaskTrigger.TransportSent,
            TransitionContext.Default,
            now,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (sent.Status != TransitionStatus.Applied)
        {
            return Outcome(_store.Get(snapshot.TaskId, TaskRole.Origin)!);
        }

        try
        {
            using var client = _clients.Create(registration);
            var result = await client.PostTaskAsync(requestJson, cancellationToken).ConfigureAwait(false);

            if (result.Accepted)
            {
                await _coordinator.TransitionAsync(
                    snapshot.TaskId,
                    TaskRole.Origin,
                    TaskTrigger.TransportAccepted,
                    TransitionContext.Default,
                    now,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                return Outcome(_store.Get(snapshot.TaskId, TaskRole.Origin)!);
            }

            await _coordinator.TransitionAsync(
                snapshot.TaskId,
                TaskRole.Origin,
                TaskTrigger.TransportRejected,
                TransitionContext.Default,
                now,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Error(
                snapshot.TaskId,
                result.ErrorCode ?? ValidationCodes.PolicyDenied,
                result.Detail ?? $"El par respondió {(int)result.StatusCode}.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            await _coordinator.TransitionAsync(
                snapshot.TaskId,
                TaskRole.Origin,
                TaskTrigger.TransportFailed,
                TransitionContext.Default,
                now,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Error(snapshot.TaskId, "PEER_UNREACHABLE", exception.Message);
        }
    }

    private TaskRequest Build(ProposalInput input, string taskId, DateTimeOffset now) => new()
    {
        ProtocolVersion = 1,
        TaskId = taskId,
        OriginNode = _options.NodeId,
        TargetNode = input.TargetNode,
        Lineage = new Lineage { Hops = [_options.NodeId], MaxDepth = 1 },
        Kind = input.Kind,
        Instructions = input.Instructions,
        Workspace = new WorkspaceSpec
        {
            Repo = input.Repo,
            BaseRef = input.BaseRef,
            Mode = input.Mode,
            AllowedPaths = input.AllowedPaths,
            DeniedPaths = input.DeniedPaths,
        },
        Limits = new TaskLimits
        {
            MaxMinutes = input.MaxMinutes,
            MaxMemoryGb = input.MaxMemoryGb,
            MaxProcesses = input.MaxProcesses,
            Network = input.Network,
            MaxCostUsd = input.MaxCostUsd,
        },
        Executor = new ExecutorSpec
        {
            Type = "opencode",
            ModelProfile = input.ModelProfile,
        },
        CreatedAt = now,
        ExpiresAt = now.AddMinutes(input.ExpiresInMinutes),
    };

    private static ProposalOutcome Outcome(TaskSnapshot snapshot, string? approvalId = null)
        => new(
            snapshot.TaskId,
            snapshot.State.ToContractString(),
            snapshot.State == TaskState.WaitingApproval,
            snapshot.TaskHash,
            NextAction(snapshot.State),
            ApprovalId: approvalId);

    private static string Summary(ProposalInput input)
    {
        var line = input.Instructions
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "(sin instrucciones)";

        if (line.Length > 200)
        {
            line = line[..197] + "...";
        }

        return $"{input.Kind.ToWireValue()} {input.Mode.ToWireValue()} {input.Repo}: {line}";
    }

    private static ProposalOutcome Error(string taskId, string code, string detail)
        => new(taskId, "REJECTED", false, string.Empty, "none", code, detail);

    private static string NextAction(TaskState state) => state switch
    {
        TaskState.WaitingApproval or TaskState.AwaitingLocalApproval => "wait_for_human",
        TaskState.Unreachable => "retry_later",
        TaskState.Done or TaskState.Completed or TaskState.ReviewRequired => "read_result",
        _ => "none",
    };
}
