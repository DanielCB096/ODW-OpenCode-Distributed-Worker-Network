using Odw.Core.Delegation;
using Odw.Core.Hashing;
using Odw.Core.Model;
using Odw.Core.Primitives;

namespace Odw.Core.Validation;

/// <summary>
/// Validación local del contrato (docs/DOMAIN-MODEL.md §2.1). No consulta la base de datos:
/// la existencia de repos, la política y los recursos se comprueban en capas superiores.
/// </summary>
public static class TaskValidator
{
    private const int MaxPathEntries = 64;
    private const int MaxHops = 4;
    private static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(24);

    public static ValidationResult Validate(TaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<ValidationIssue>();

        if (request.ProtocolVersion != 1)
        {
            Add(issues, ValidationCodes.ProtocolUnsupported, "protocol_version",
                $"Versión {request.ProtocolVersion} no soportada (esperada 1).");
        }

        if (!Ulid.TryParse(request.TaskId, out _))
        {
            Add(issues, ValidationCodes.ContractInvalid, "task_id", "task_id no es un ULID válido.");
        }

        if (!ContractFormat.IsValidNodeId(request.OriginNode))
        {
            Add(issues, ValidationCodes.ContractInvalid, "origin_node", "origin_node inválido.");
        }

        if (!ContractFormat.IsValidNodeId(request.TargetNode))
        {
            Add(issues, ValidationCodes.ContractInvalid, "target_node", "target_node inválido.");
        }

        if (request.ParentTaskId is not null && !Ulid.TryParse(request.ParentTaskId, out _))
        {
            Add(issues, ValidationCodes.ContractInvalid, "parent_task_id", "parent_task_id inválido.");
        }

        ValidateLineage(request, issues);
        ValidateKindAndMode(request, issues);
        ValidateWorkspace(request, issues);
        ValidateLimits(request, issues);
        ValidateInstructions(request, issues);
        ValidateWindow(request, issues);
        ValidateApproval(request, issues);

        return issues.Count == 0 ? ValidationResult.Valid : new ValidationResult(issues);
    }

    private static void ValidateLineage(TaskRequest request, List<ValidationIssue> issues)
    {
        if (request.Lineage.Hops.Count > MaxHops)
        {
            Add(issues, ValidationCodes.ContractInvalid, "lineage.hops",
                $"Demasiados saltos ({request.Lineage.Hops.Count}); máximo {MaxHops}.");
            return;
        }

        switch (LineageGuard.Evaluate(request.OriginNode, request.TargetNode, request.Lineage))
        {
            case LineageCheck.Ok:
                break;
            case LineageCheck.SelfDelegation:
                Add(issues, ValidationCodes.SelfDelegation, "target_node",
                    "origin_node y target_node no pueden coincidir.");
                break;
            case LineageCheck.Loop:
                Add(issues, ValidationCodes.LineageLoop, "lineage.hops",
                    "target_node ya aparece en lineage.hops.");
                break;
            case LineageCheck.DepthExceeded:
                Add(issues, ValidationCodes.LineageLoop, "lineage.hops",
                    "Se excede lineage.max_depth.");
                break;
            default:
                Add(issues, ValidationCodes.ContractInvalid, "lineage",
                    "lineage inválido (nodos o max_depth).");
                break;
        }
    }

    private static void ValidateKindAndMode(TaskRequest request, List<ValidationIssue> issues)
    {
        if (request.Kind == TaskKind.CodeChange && request.Workspace.Mode == WorkspaceMode.ReadOnly)
        {
            Add(issues, ValidationCodes.ContractInvalid, "workspace.mode",
                "code_change requiere read_write_worktree.");
        }
    }

    private static void ValidateWorkspace(TaskRequest request, List<ValidationIssue> issues)
    {
        if (!ContractFormat.IsValidRepoName(request.Workspace.Repo))
        {
            Add(issues, ValidationCodes.ContractInvalid, "workspace.repo", "Nombre de repo inválido.");
        }

        if (!ContractFormat.IsValidGitRef(request.Workspace.BaseRef))
        {
            Add(issues, ValidationCodes.ContractInvalid, "workspace.base_ref", "base_ref inválido.");
        }

        ValidatePaths(request.Workspace.AllowedPaths, "workspace.allowed_paths", required: true, issues);
        ValidatePaths(request.Workspace.DeniedPaths, "workspace.denied_paths", required: false, issues);
    }

    private static void ValidatePaths(
        IReadOnlyList<string> paths,
        string field,
        bool required,
        List<ValidationIssue> issues)
    {
        if (required && paths.Count == 0)
        {
            Add(issues, ValidationCodes.PathInvalid, field, "Debe declarar al menos una ruta permitida.");
            return;
        }

        if (paths.Count > MaxPathEntries)
        {
            Add(issues, ValidationCodes.PathInvalid, field,
                $"Demasiadas rutas ({paths.Count}); máximo {MaxPathEntries}.");
            return;
        }

        for (var i = 0; i < paths.Count; i++)
        {
            if (!ContractFormat.TryNormalizeRelativePath(paths[i], out _))
            {
                Add(issues, ValidationCodes.PathInvalid, $"{field}[{i}]",
                    "Ruta no relativa, con traversal o con caracteres no permitidos.");
            }
        }
    }

    private static void ValidateLimits(TaskRequest request, List<ValidationIssue> issues)
    {
        if (request.Limits.MaxMinutes is < 1 or > 240)
        {
            Add(issues, ValidationCodes.LimitsInvalid, "limits.max_minutes", "Rango permitido: 1..240.");
        }

        if (request.Limits.MaxMemoryGb is < 1 or > 1024)
        {
            Add(issues, ValidationCodes.LimitsInvalid, "limits.max_memory_gb", "Rango permitido: 1..1024.");
        }

        if (request.Limits.MaxProcesses is < 1 or > 512)
        {
            Add(issues, ValidationCodes.LimitsInvalid, "limits.max_processes", "Rango permitido: 1..512.");
        }

        if (request.Limits.MaxCostUsd is < 0m or > 100m)
        {
            Add(issues, ValidationCodes.LimitsInvalid, "limits.max_cost_usd", "Rango permitido: 0..100.");
        }
    }

    private static void ValidateInstructions(TaskRequest request, List<ValidationIssue> issues)
    {
        var instructions = request.Instructions;
        if (instructions is null)
        {
            Add(issues, ValidationCodes.ContractInvalid, "instructions", "Faltan instrucciones.");
            return;
        }

        if (instructions.Contains('\0'))
        {
            Add(issues, ValidationCodes.ContractInvalid, "instructions", "Contiene NUL.");
        }

        if (instructions.Length > ContractFormat.MaxInstructionsLength)
        {
            Add(issues, ValidationCodes.ContractInvalid, "instructions",
                $"Excede {ContractFormat.MaxInstructionsLength} caracteres.");
        }

        if (ContractFormat.HasLoneSurrogate(instructions))
        {
            Add(issues, ValidationCodes.ContractInvalid, "instructions", "Contiene Unicode inválido.");
        }

        if (ContractFormat.NormalizeInstructions(instructions).Length == 0)
        {
            Add(issues, ValidationCodes.ContractInvalid, "instructions", "Las instrucciones están vacías.");
        }
    }

    private static void ValidateWindow(TaskRequest request, List<ValidationIssue> issues)
    {
        if (request.CreatedAt == default || request.ExpiresAt == default)
        {
            Add(issues, ValidationCodes.ContractInvalid, "expires_at", "created_at y expires_at son obligatorios.");
            return;
        }

        if (request.ExpiresAt <= request.CreatedAt)
        {
            Add(issues, ValidationCodes.ContractInvalid, "expires_at",
                "expires_at debe ser posterior a created_at.");
            return;
        }

        if (request.ExpiresAt - request.CreatedAt > MaxLifetime)
        {
            Add(issues, ValidationCodes.ContractInvalid, "expires_at",
                "expires_at no puede superar 24 horas desde created_at.");
        }
    }

    private static void ValidateApproval(TaskRequest request, List<ValidationIssue> issues)
    {
        if (request.Executor.Type != "opencode")
        {
            Add(issues, ValidationCodes.ContractInvalid, "executor.type", "Solo se soporta 'opencode'.");
        }

        if (request.Executor.ModelHint is { Length: > 128 })
        {
            Add(issues, ValidationCodes.ContractInvalid, "executor.model_hint", "Máximo 128 caracteres.");
        }

        if (request.Executor.ModelProfile is { } profile)
        {
            if (profile.Length is 0 or > 64
                || !profile.All(character => char.IsAsciiLetterOrDigit(character)
                                             || character is '-' or '_' or '.'))
            {
                Add(issues, ValidationCodes.ContractInvalid, "executor.model_profile",
                    "Nombre de perfil inválido (1-64: letras, dígitos, '-', '_' o '.').");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Executor.ModelProfile)
            && !string.IsNullOrWhiteSpace(request.Executor.ModelHint))
        {
            Add(issues, ValidationCodes.ContractInvalid, "executor.model_profile",
                "Usa model_profile o model_hint, no ambos.");
        }

        var approval = request.Approval;
        if (approval is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(approval.ApprovalId)
            || string.IsNullOrWhiteSpace(approval.Nonce)
            || string.IsNullOrWhiteSpace(approval.Signature)
            || approval.ApprovedAt == default)
        {
            Add(issues, ValidationCodes.ApprovalInvalid, "approval", "Evidencia de aprobación incompleta.");
            return;
        }

        if (!IsHumanApprover(approval.ApprovedBy))
        {
            Add(issues, ValidationCodes.ApprovalInvalid, "approval.approved_by",
                "approved_by debe ser human:telegram:* o human:cli:*.");
        }

        if (approval.Nonce.Length is < 16 or > 128)
        {
            Add(issues, ValidationCodes.ApprovalInvalid, "approval.nonce", "Longitud de nonce inválida.");
        }

        if (!approval.Signature.StartsWith("hmac-sha256:", StringComparison.Ordinal)
            || approval.Signature.Length != "hmac-sha256:".Length + 64)
        {
            Add(issues, ValidationCodes.ApprovalInvalid, "approval.signature", "Firma inválida.");
        }

        if (!ContractFormat.IsValidTaskHash(approval.TaskHash))
        {
            Add(issues, ValidationCodes.ApprovalInvalid, "approval.task_hash", "task_hash inválido.");
            return;
        }

        if (request.Instructions is null)
        {
            return;
        }

        var computed = TaskHash.Compute(request);
        if (!string.Equals(computed, approval.TaskHash, StringComparison.Ordinal))
        {
            Add(issues, ValidationCodes.ApprovalInvalid, "approval.task_hash",
                "El hash no coincide con el contrato: la aprobación no es válida.");
        }
    }

    private static bool IsHumanApprover(string? approvedBy)
        => approvedBy is not null
           && (approvedBy.StartsWith("human:telegram:", StringComparison.Ordinal)
               || approvedBy.StartsWith("human:cli:", StringComparison.Ordinal));

    private static void Add(List<ValidationIssue> issues, string code, string field, string message)
        => issues.Add(new ValidationIssue(code, field, message));
}
