using System.Globalization;
using System.Text.Json.Nodes;
using Odw.Core.Model;

namespace Odw.Node.Contracts;

/// <summary>Serializa un contrato al JSON wire (snake_case) que espera la API de pares.</summary>
public static class TaskContractWriter
{
    public static string Write(TaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lineage = new JsonObject
        {
            ["hops"] = Array(request.Lineage.Hops),
            ["max_depth"] = request.Lineage.MaxDepth,
        };

        var workspace = new JsonObject
        {
            ["repo"] = request.Workspace.Repo,
            ["base_ref"] = request.Workspace.BaseRef,
            ["mode"] = request.Workspace.Mode.ToWireValue(),
            ["strategy"] = request.Workspace.Strategy.ToWireValue(),
            ["allowed_paths"] = Array(request.Workspace.AllowedPaths),
            ["denied_paths"] = Array(request.Workspace.DeniedPaths),
        };

        var limits = new JsonObject
        {
            ["max_minutes"] = request.Limits.MaxMinutes,
            ["max_memory_gb"] = request.Limits.MaxMemoryGb,
            ["max_processes"] = request.Limits.MaxProcesses,
            ["network"] = request.Limits.Network.ToWireValue(),
            ["max_cost_usd"] = request.Limits.MaxCostUsd,
        };

        var executor = new JsonObject
        {
            ["type"] = request.Executor.Type,
            ["model_hint"] = request.Executor.ModelHint,
        };

        if (request.Executor.ModelProfile is not null)
        {
            executor["model_profile"] = request.Executor.ModelProfile;
        }

        JsonNode? approval = null;
        if (request.Approval is { } evidence)
        {
            approval = new JsonObject
            {
                ["approval_id"] = evidence.ApprovalId,
                ["task_hash"] = evidence.TaskHash,
                ["approved_by"] = evidence.ApprovedBy,
                ["approved_at"] = Timestamp(evidence.ApprovedAt),
                ["nonce"] = evidence.Nonce,
                ["signature"] = evidence.Signature,
            };
        }

        var json = new JsonObject
        {
            ["protocol_version"] = request.ProtocolVersion,
            ["task_id"] = request.TaskId,
            ["origin_node"] = request.OriginNode,
            ["target_node"] = request.TargetNode,
            ["parent_task_id"] = request.ParentTaskId,
            ["lineage"] = lineage,
            ["kind"] = request.Kind.ToWireValue(),
            ["instructions"] = request.Instructions,
            ["workspace"] = workspace,
            ["limits"] = limits,
            ["executor"] = executor,
            ["created_at"] = Timestamp(request.CreatedAt),
            ["expires_at"] = Timestamp(request.ExpiresAt),
            ["approval"] = approval,
        };

        return json.ToJsonString();
    }

    private static JsonArray Array(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static string Timestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
