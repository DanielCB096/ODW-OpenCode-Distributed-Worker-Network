using System.Globalization;
using System.Text.Json;
using Odw.Core.Model;

namespace Odw.Node.Contracts;

public sealed class TaskContractException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>
/// Lee el contrato de tarea v1 desde JSON (nombres snake_case y enums wire). Tolera campos
/// desconocidos para permitir evolución, pero exige los obligatorios y valida sus tipos.
/// </summary>
public static class TaskContractReader
{
    public static TaskRequest Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new TaskContractException("$", "Se espera un objeto JSON.");
        }

        var protocolVersion = OptionalInt(root, "protocol_version", "$") ?? 1;
        var taskId = RequiredString(root, "task_id", "$");
        var origin = RequiredString(root, "origin_node", "$");
        var target = RequiredString(root, "target_node", "$");
        var parent = OptionalString(root, "parent_task_id", "$");
        var lineage = ReadLineage(root);
        var kind = ParseKind(RequiredString(root, "kind", "$"), "$.kind");
        var instructions = RequiredString(root, "instructions", "$");
        var workspace = ReadWorkspace(root);
        var limits = ReadLimits(root);
        var executor = ReadExecutor(root);
        var createdAt = RequiredDate(root, "created_at", "$");
        var expiresAt = RequiredDate(root, "expires_at", "$");
        var approval = ReadApproval(root);

        return new TaskRequest
        {
            ProtocolVersion = protocolVersion,
            TaskId = taskId,
            OriginNode = origin,
            TargetNode = target,
            ParentTaskId = parent,
            Lineage = lineage,
            Kind = kind,
            Instructions = instructions,
            Workspace = workspace,
            Limits = limits,
            Executor = executor,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            Approval = approval,
        };
    }

    private static Lineage ReadLineage(JsonElement root)
    {
        if (!TryGetObject(root, "lineage", out var lineage))
        {
            return new Lineage();
        }

        return new Lineage
        {
            Hops = ReadStrings(lineage, "hops", "$.lineage", required: false),
            MaxDepth = OptionalInt(lineage, "max_depth", "$.lineage") ?? 1,
        };
    }

    private static WorkspaceSpec ReadWorkspace(JsonElement root)
    {
        var workspace = RequiredObject(root, "workspace", "$");

        return new WorkspaceSpec
        {
            Repo = RequiredString(workspace, "repo", "$.workspace"),
            BaseRef = OptionalString(workspace, "base_ref", "$.workspace") ?? "main",
            Mode = ParseMode(RequiredString(workspace, "mode", "$.workspace"), "$.workspace.mode"),
            Strategy = ParseStrategy(
                OptionalString(workspace, "strategy", "$.workspace") ?? "clone",
                "$.workspace.strategy"),
            AllowedPaths = ReadStrings(workspace, "allowed_paths", "$.workspace", required: true),
            DeniedPaths = ReadStrings(workspace, "denied_paths", "$.workspace", required: false),
        };
    }

    private static TaskLimits ReadLimits(JsonElement root)
    {
        var limits = RequiredObject(root, "limits", "$");

        return new TaskLimits
        {
            MaxMinutes = RequiredInt(limits, "max_minutes", "$.limits"),
            MaxMemoryGb = RequiredInt(limits, "max_memory_gb", "$.limits"),
            MaxProcesses = RequiredInt(limits, "max_processes", "$.limits"),
            Network = ParseNetwork(RequiredString(limits, "network", "$.limits"), "$.limits.network"),
            MaxCostUsd = OptionalDecimal(limits, "max_cost_usd", "$.limits") ?? 5m,
        };
    }

    private static ExecutorSpec ReadExecutor(JsonElement root)
    {
        if (!TryGetObject(root, "executor", out var executor))
        {
            return new ExecutorSpec();
        }

        return new ExecutorSpec
        {
            Type = OptionalString(executor, "type", "$.executor") ?? "opencode",
            ModelHint = OptionalString(executor, "model_hint", "$.executor"),
        };
    }

    private static ApprovalEvidence? ReadApproval(JsonElement root)
    {
        if (!TryGetObject(root, "approval", out var approval) || approval.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new ApprovalEvidence
        {
            ApprovalId = RequiredString(approval, "approval_id", "$.approval"),
            TaskHash = RequiredString(approval, "task_hash", "$.approval"),
            ApprovedBy = RequiredString(approval, "approved_by", "$.approval"),
            ApprovedAt = RequiredDate(approval, "approved_at", "$.approval"),
            Nonce = RequiredString(approval, "nonce", "$.approval"),
            Signature = RequiredString(approval, "signature", "$.approval"),
        };
    }

    private static TaskKind ParseKind(string value, string path) => value switch
    {
        "analysis" => TaskKind.Analysis,
        "test_run" => TaskKind.TestRun,
        "code_change" => TaskKind.CodeChange,
        _ => throw new TaskContractException(path, $"kind inválido: {value}"),
    };

    private static WorkspaceMode ParseMode(string value, string path) => value switch
    {
        "read_only" => WorkspaceMode.ReadOnly,
        "read_write_worktree" => WorkspaceMode.ReadWriteWorktree,
        _ => throw new TaskContractException(path, $"mode inválido: {value}"),
    };

    private static WorkspaceStrategy ParseStrategy(string value, string path) => value switch
    {
        "snapshot" => WorkspaceStrategy.Snapshot,
        "clone" => WorkspaceStrategy.Clone,
        "worktree" => WorkspaceStrategy.Worktree,
        _ => throw new TaskContractException(path, $"strategy inválido: {value}"),
    };

    private static NetworkAccess ParseNetwork(string value, string path) => value switch
    {
        "none" => NetworkAccess.None,
        "allow" => NetworkAccess.Allow,
        _ => throw new TaskContractException(path, $"network inválido: {value}"),
    };

    private static JsonElement RequiredObject(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new TaskContractException($"{path}.{name}", "objeto requerido.");
        }

        return value;
    }

    private static bool TryGetObject(JsonElement parent, string name, out JsonElement value)
        => parent.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.Object;

    private static string RequiredString(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new TaskContractException($"{path}.{name}", "string requerido.");
        }

        return value.GetString()!;
    }

    private static string? OptionalString(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new TaskContractException($"{path}.{name}", "string esperado.");
        }

        return value.GetString();
    }

    private static int RequiredInt(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out var number))
        {
            throw new TaskContractException($"{path}.{name}", "entero requerido.");
        }

        return number;
    }

    private static int? OptionalInt(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!value.TryGetInt32(out var number))
        {
            throw new TaskContractException($"{path}.{name}", "entero esperado.");
        }

        return number;
    }

    private static decimal? OptionalDecimal(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!value.TryGetDecimal(out var number))
        {
            throw new TaskContractException($"{path}.{name}", "número esperado.");
        }

        return number;
    }

    private static IReadOnlyList<string> ReadStrings(
        JsonElement parent,
        string name,
        string path,
        bool required)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            if (required)
            {
                throw new TaskContractException($"{path}.{name}", "array requerido.");
            }

            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new TaskContractException($"{path}.{name}", "array de strings esperado.");
        }

        var items = new List<string>();
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new TaskContractException($"{path}.{name}[{index}]", "string esperado.");
            }

            items.Add(item.GetString()!);
            index++;
        }

        return items;
    }

    private static DateTimeOffset RequiredDate(JsonElement parent, string name, string path)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new TaskContractException($"{path}.{name}", "fecha RFC 3339 requerida.");
        }

        if (!DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var date))
        {
            throw new TaskContractException($"{path}.{name}", "fecha inválida.");
        }

        return date;
    }
}
