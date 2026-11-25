using System.Text.Json.Nodes;
using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Mcp.Abstractions;

namespace Odw.Mcp.Tools;

/// <summary>Herramientas de solo lectura sobre tareas locales.</summary>
public static class TaskTools
{
    public static IEnumerable<McpTool> Create(ITaskQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        yield return new McpTool(
            "odw_get_task_status",
            "Devuelve el estado, la secuencia y el progreso de una tarea conocida por este nodo.",
            JsonTools.Schema(
                """
                {
                  "type": "object",
                  "properties": {
                    "task_id": { "type": "string", "pattern": "^[0-9A-HJKMNP-TV-Z]{26}$" }
                  },
                  "required": ["task_id"],
                  "additionalProperties": false
                }
                """),
            (arguments, cancellationToken) => StatusAsync(query, arguments, cancellationToken));

        yield return new McpTool(
            "odw_get_task_result",
            "Devuelve el resultado final de una tarea terminada: informe, cambios, rama y artefactos.",
            JsonTools.Schema(
                """
                {
                  "type": "object",
                  "properties": {
                    "task_id": { "type": "string", "pattern": "^[0-9A-HJKMNP-TV-Z]{26}$" },
                    "include_events": { "type": "boolean", "default": false },
                    "include_diff": { "type": "boolean", "default": false }
                  },
                  "required": ["task_id"],
                  "additionalProperties": false
                }
                """),
            (arguments, cancellationToken) => ResultAsync(query, arguments, cancellationToken));
    }

    private static async Task<McpToolResult> StatusAsync(
        ITaskQuery query,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var taskId = JsonTools.GetString(arguments, "task_id");
        if (!Ulid.TryParse(taskId, out _))
        {
            return McpToolResult.Error("CONTRACT_INVALID", "task_id no es un ULID válido.");
        }

        var status = await query.GetStatusAsync(taskId!, cancellationToken).ConfigureAwait(false);
        if (status is null)
        {
            return McpToolResult.Error("TASK_NOT_FOUND", $"Este nodo no conoce la tarea {taskId}.");
        }

        var text = $"{status.TaskId} en {status.State.ToContractString()} (seq {status.StateSeq}, "
                   + $"par {status.CounterpartNode}). Siguiente acción: {status.NextAction}.";

        return McpToolResult.Ok(text, ToJson(status));
    }

    private static async Task<McpToolResult> ResultAsync(
        ITaskQuery query,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var taskId = JsonTools.GetString(arguments, "task_id");
        if (!Ulid.TryParse(taskId, out _))
        {
            return McpToolResult.Error("CONTRACT_INVALID", "task_id no es un ULID válido.");
        }

        var includeEvents = JsonTools.GetBool(arguments, "include_events") ?? false;
        var includeDiff = JsonTools.GetBool(arguments, "include_diff") ?? false;
        var result = await query.GetResultAsync(taskId!, includeEvents, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return McpToolResult.Error("TASK_NOT_FOUND", $"Este nodo no conoce la tarea {taskId}.");
        }

        if (!result.ResultAvailable)
        {
            return McpToolResult.Error(
                "TASK_STATE_CONFLICT",
                $"La tarea {taskId} todavía no tiene resultado (estado {result.State.ToContractString()}).",
                retryable: true,
                extra: new JsonObject { ["state"] = result.State.ToContractString() });
        }

        var structured = ToJson(result, includeEvents, includeDiff);
        var summary = result.SummaryMarkdown.Length == 0
            ? $"{result.TaskId} terminó con resultado {result.Outcome.ToWireValue()}."
            : $"{result.TaskId}: {FirstLine(result.SummaryMarkdown)}";

        return McpToolResult.Ok(summary, structured);
    }

    internal static JsonObject ToJson(TaskStatusView status)
    {
        var json = new JsonObject
        {
            ["task_id"] = status.TaskId,
            ["role"] = status.Role.ToWireValue(),
            ["state"] = status.State.ToContractString(),
            ["state_seq"] = status.StateSeq,
            ["counterpart_node"] = status.CounterpartNode,
            ["updated_at"] = status.UpdatedAt.ToString("O"),
            ["next_action"] = status.NextAction,
            ["result_available"] = status.ResultAvailable,
            ["cancel_requested"] = status.CancelRequested,
        };

        JsonTools.AddIfNotNull(json, "lease_expires_at",
            status.LeaseExpiresAt is null ? null : JsonValue.Create(status.LeaseExpiresAt.Value.ToString("O")));
        JsonTools.AddIfNotNull(json, "phase",
            status.Phase is null ? null : JsonValue.Create(status.Phase));
        JsonTools.AddIfNotNull(json, "progress_percent", JsonValue.Create(status.ProgressPercent));

        return json;
    }

    internal static JsonObject ToJson(TaskResultView result, bool includeEvents, bool includeDiff = false)
    {
        var json = new JsonObject
        {
            ["task_id"] = result.TaskId,
            ["state"] = result.State.ToContractString(),
            ["outcome"] = result.Outcome.ToWireValue(),
            ["summary_md"] = result.SummaryMarkdown,
            ["changed_paths"] = JsonTools.ToArray(result.ChangedPaths),
            ["artifacts"] = JsonTools.ToArray(result.Artifacts),
            ["requires_review"] = result.RequiresReview,
        };

        JsonTools.AddIfNotNull(json, "branch",
            result.Branch is null ? null : JsonValue.Create(result.Branch));
        JsonTools.AddIfNotNull(json, "commit",
            result.Commit is null ? null : JsonValue.Create(result.Commit));

        if (includeDiff)
        {
            json["diff"] = result.Diff is null ? null : JsonValue.Create(result.Diff);
            json["diff_truncated"] = JsonValue.Create(result.DiffTruncated);
        }

        if (includeEvents)
        {
            var events = new JsonArray();
            foreach (var item in result.Events)
            {
                events.Add(new JsonObject
                {
                    ["seq"] = item.Seq,
                    ["type"] = item.Type,
                    ["created_at"] = item.CreatedAt.ToString("O"),
                });
            }

            json["events"] = events;
        }

        return json;
    }

    private static string FirstLine(string text)
    {
        var newline = text.IndexOf('\n');
        var line = newline < 0 ? text : text[..newline];
        return line.Length <= 240 ? line : line[..237] + "...";
    }
}
