using System.Text.Json.Nodes;
using Odw.Core.Model;
using Odw.Mcp.Abstractions;

namespace Odw.Mcp.Tools;

/// <summary>Herramienta de propuesta de delegación (lado origen).</summary>
public static class OriginTools
{
    public static IEnumerable<McpTool> Create(ITaskProposalService proposals)
    {
        ArgumentNullException.ThrowIfNull(proposals);

        yield return new McpTool(
            "odw_propose_task",
            "Propone delegar una tarea a otro nodo. Puede quedar esperando aprobación humana; no ejecuta nada por sí misma.",
            JsonTools.Schema(
                """
                {
                  "type": "object",
                  "properties": {
                    "target_node": { "type": "string", "pattern": "^[A-Z0-9][A-Z0-9-]{1,31}$" },
                    "kind": { "enum": ["analysis", "test_run", "code_change"] },
                    "mode": { "enum": ["read_only", "read_write_worktree"] },
                    "repo": { "type": "string", "minLength": 1, "maxLength": 64 },
                    "base_ref": { "type": "string", "default": "main" },
                    "instructions": { "type": "string", "minLength": 1, "maxLength": 65536 },
                    "allowed_paths": {
                      "type": "array", "minItems": 1, "maxItems": 64,
                      "items": { "type": "string" }
                    },
                    "denied_paths": {
                      "type": "array", "maxItems": 64,
                      "items": { "type": "string" }
                    },
                    "limits": {
                      "type": "object",
                      "properties": {
                        "max_minutes": { "type": "integer", "minimum": 1, "maximum": 240 },
                        "max_memory_gb": { "type": "integer", "minimum": 1, "maximum": 1024 },
                        "max_processes": { "type": "integer", "minimum": 1, "maximum": 512 },
                        "network": { "enum": ["none", "allow"] },
                        "max_cost_usd": { "type": "number", "minimum": 0, "maximum": 100 }
                      },
                      "additionalProperties": false
                    },
                    "expires_in_minutes": { "type": "integer", "minimum": 1, "maximum": 1440 },
                    "task_id": { "type": "string", "pattern": "^[0-9A-HJKMNP-TV-Z]{26}$" }
                  },
                  "required": ["target_node", "kind", "mode", "repo", "instructions", "allowed_paths"],
                  "additionalProperties": false
                }
                """),
            (arguments, cancellationToken) => ProposeAsync(proposals, arguments, cancellationToken));
    }

    private static async Task<McpToolResult> ProposeAsync(
        ITaskProposalService proposals,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var target = JsonTools.GetString(arguments, "target_node");
        var repo = JsonTools.GetString(arguments, "repo");
        var instructions = JsonTools.GetString(arguments, "instructions");
        var allowed = JsonTools.GetStrings(arguments, "allowed_paths");

        if (string.IsNullOrWhiteSpace(target)
            || string.IsNullOrWhiteSpace(repo)
            || string.IsNullOrWhiteSpace(instructions)
            || allowed is null)
        {
            return McpToolResult.Error("CONTRACT_INVALID", "Faltan target_node, repo, instructions o allowed_paths.");
        }

        var kind = ParseKind(JsonTools.GetString(arguments, "kind"));
        var mode = ParseMode(JsonTools.GetString(arguments, "mode"));
        if (kind is null || mode is null)
        {
            return McpToolResult.Error("CONTRACT_INVALID", "kind o mode inválido.");
        }

        var limits = JsonTools.GetObject(arguments, "limits") ?? new JsonObject();
        var network = ParseNetwork(JsonTools.GetString(limits, "network"));
        if (network is null)
        {
            return McpToolResult.Error("CONTRACT_INVALID", "limits.network inválido.");
        }

        var input = new ProposalInput(
            target!,
            kind.Value,
            mode.Value,
            repo!,
            JsonTools.GetString(arguments, "base_ref") ?? "main",
            instructions!,
            allowed,
            JsonTools.GetStrings(arguments, "denied_paths") ?? [],
            JsonTools.GetInt(limits, "max_minutes") ?? 45,
            JsonTools.GetInt(limits, "max_memory_gb") ?? 12,
            JsonTools.GetInt(limits, "max_processes") ?? 64,
            network.Value,
            JsonTools.GetDecimal(limits, "max_cost_usd") ?? 5m,
            JsonTools.GetInt(arguments, "expires_in_minutes") ?? 60,
            JsonTools.GetString(arguments, "task_id"));

        var outcome = await proposals.ProposeAsync(input, cancellationToken).ConfigureAwait(false);

        if (outcome.IsError)
        {
            return McpToolResult.Error(
                outcome.ErrorCode!,
                outcome.Detail ?? "La propuesta fue rechazada.");
        }

        var structured = new JsonObject
        {
            ["task_id"] = outcome.TaskId,
            ["state"] = outcome.State,
            ["approval_required"] = outcome.ApprovalRequired,
            ["task_hash"] = outcome.TaskHash,
            ["next_action"] = outcome.NextAction,
        };

        if (outcome.ApprovalId is not null)
        {
            structured["approval_id"] = outcome.ApprovalId;
        }

        var text = $"{outcome.TaskId} en {outcome.State}. Siguiente acción: {outcome.NextAction}.";
        return McpToolResult.Ok(text, structured);
    }

    private static TaskKind? ParseKind(string? value) => value switch
    {
        "analysis" => TaskKind.Analysis,
        "test_run" => TaskKind.TestRun,
        "code_change" => TaskKind.CodeChange,
        _ => null,
    };

    private static WorkspaceMode? ParseMode(string? value) => value switch
    {
        "read_only" => WorkspaceMode.ReadOnly,
        "read_write_worktree" => WorkspaceMode.ReadWriteWorktree,
        _ => null,
    };

    private static NetworkAccess? ParseNetwork(string? value) => value switch
    {
        null or "none" => NetworkAccess.None,
        "allow" => NetworkAccess.Allow,
        _ => null,
    };
}
