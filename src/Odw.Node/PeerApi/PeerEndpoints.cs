using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Odw.Core.Contracts;
using Odw.Core.Hashing;
using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Core.Validation;
using Odw.Node.Contracts;
using Odw.Node.Hosting;
using Odw.Node.Peers;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Tasks;

namespace Odw.Node.PeerApi;

/// <summary>
/// API de pares v1 (subconjunto del H1): hello, status, alta de tarea y consulta de estado.
/// La autenticación y el bind se aplican en <see cref="NodeHost"/>; aquí solo se atiende.
/// </summary>
internal static class PeerEndpoints
{
    public const string PeerItem = "odw.peer";

    private const int MaxBodyBytes = 1_048_576;
    private const string OdwVersion = "0.1.0";

    public static void Map(WebApplication app, NodeRuntime runtime)
    {
        app.MapGet("/peer/v1/hello", () => Results.Json(Hello(runtime)));
        app.MapGet("/peer/v1/status", () => Results.Json(Status(runtime)));
        app.MapGet("/peer/v1/health", () => Results.Json(new JsonObject { ["status"] = "ok" }));
        app.MapPost("/peer/v1/tasks", (Delegate)((HttpContext context) => CreateAsync(context, runtime)));
        app.MapGet("/peer/v1/tasks/{taskId}", (HttpContext context, string taskId) => Get(context, runtime, taskId));
        app.MapGet("/peer/v1/tasks/{taskId}/result", (HttpContext context, string taskId) => GetResult(context, runtime, taskId));
    }

    private static JsonObject Hello(NodeRuntime runtime)
    {
        var options = runtime.Options;

        return new JsonObject
        {
            ["node_id"] = options.NodeId,
            ["protocol_version"] = 1,
            ["min_protocol"] = 1,
            ["max_protocol"] = 1,
            ["odw_version"] = OdwVersion,
            ["opencode_version"] = options.OpenCodeVersion,
            ["server_time"] = PersistenceJson.Timestamp(options.TimeProvider.GetUtcNow()),
            ["capabilities"] = Array(options.Capabilities),
            ["accepts"] = Array(options.Accepts),
        };
    }

    private static JsonObject Status(NodeRuntime runtime)
    {
        var options = runtime.Options;
        var active = runtime.Tasks.ListByState(
            TaskRole.Target,
            TaskState.Running,
            TaskState.Cancelling,
            TaskState.Queued).Count;
        var running = runtime.Tasks.ListByState(TaskRole.Target, TaskState.Running).Count;
        var ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024d * 1024d * 1024d);

        return new JsonObject
        {
            ["node_id"] = options.NodeId,
            ["protocol_version"] = 1,
            ["status"] = "online",
            ["hardware"] = new JsonObject
            {
                ["cpu_cores"] = Environment.ProcessorCount,
            },
            ["resources"] = new JsonObject
            {
                ["ram_available_gb"] = Math.Round(ram, 1),
                ["active_tasks"] = running,
                ["queued_tasks"] = Math.Max(0, active - running),
                ["queue_capacity"] = 8,
            },
            ["capabilities"] = Array(options.Capabilities),
            ["accepts"] = Array(options.Accepts),
            ["repos"] = new JsonArray(),
            ["load_score"] = Math.Round(Math.Min(1d, active / 8d), 2),
            ["measured_at"] = PersistenceJson.Timestamp(options.TimeProvider.GetUtcNow()),
        };
    }

    private static async Task<IResult> CreateAsync(HttpContext context, NodeRuntime runtime)
    {
        var peer = Peer(context);

        if (context.Request.ContentType is null
            || !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(415, "CONTRACT_INVALID", "Content-Type debe ser application/json.");
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            return Problem(413, "CONTRACT_INVALID", "Cuerpo demasiado grande.");
        }

        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
        {
            return Problem(413, "CONTRACT_INVALID", "Cuerpo demasiado grande.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Problem(400, "CONTRACT_INVALID", "JSON inválido.");
        }

        using (document)
        {
            TaskRequest request;
            try
            {
                request = TaskContractReader.Read(document.RootElement);
            }
            catch (TaskContractException exception)
            {
                return Problem(400, "CONTRACT_INVALID", $"{exception.Field}: {exception.Message}");
            }

            var validation = TaskValidator.Validate(request);
            if (!validation.IsValid)
            {
                var issue = validation.Issues[0];
                return Problem(StatusFor(issue.Code), issue.Code, $"{issue.Field}: {issue.Message}");
            }

            if (!string.Equals(request.TargetNode, runtime.Options.NodeId, StringComparison.Ordinal))
            {
                return Problem(400, "CONTRACT_INVALID", "target_node no es este nodo.");
            }

            var hash = TaskHash.Compute(request);
            var existing = runtime.Tasks.Get(request.TaskId, TaskRole.Target);
            if (existing is not null)
            {
                return ReplayOrConflict(existing, hash);
            }

            var now = runtime.Options.TimeProvider.GetUtcNow();
            var decision = runtime.Policy.Evaluate(new PolicyRequest(
                TaskRole.Target,
                peer.NodeId,
                peer.Trust,
                request,
                runtime.Options.Capabilities,
                now));

            if (decision.Effect == PolicyEffect.Deny)
            {
                return Problem(403, "POLICY_DENIED", decision.Reason);
            }

            var state = decision.Effect == PolicyEffect.Ask
                ? TaskState.AwaitingLocalApproval
                : TaskState.Queued;

            var payload = PersistenceJson.Serialize(new
            {
                policy_rule = decision.MatchedRuleId,
                effect = decision.Effect.ToString(),
                peer = peer.NodeId,
            });

            TaskSnapshot snapshot;
            try
            {
                snapshot = await runtime.Coordinator.AcceptAsync(
                    request,
                    TaskRole.Target,
                    body,
                    state,
                    "task.received",
                    payload,
                    context.RequestAborted).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
            {
                var raced = runtime.Tasks.Get(request.TaskId, TaskRole.Target);
                return raced is not null
                    ? ReplayOrConflict(raced, hash)
                    : Problem(409, "TASK_ID_CONFLICT", "Conflicto al crear la tarea.");
            }

            context.Response.Headers.Location = $"/peer/v1/tasks/{snapshot.TaskId}";
            return Results.Json(View(snapshot), statusCode: 202);
        }
    }

    private static IResult Get(HttpContext context, NodeRuntime runtime, string taskId)
    {
        var peer = Peer(context);

        if (!Ulid.TryParse(taskId, out _))
        {
            return Problem(404, "TASK_NOT_FOUND", "Tarea desconocida.");
        }

        var snapshot = runtime.Tasks.Get(taskId, TaskRole.Target);
        if (snapshot is null
            || !string.Equals(snapshot.CounterpartNode, peer.NodeId, StringComparison.Ordinal))
        {
            return Problem(404, "TASK_NOT_FOUND", "Tarea desconocida.");
        }

        var etag = $"\"seq-{snapshot.StateSeq}\"";
        context.Response.Headers.ETag = etag;

        if (string.Equals(context.Request.Headers.IfNoneMatch.ToString(), etag, StringComparison.Ordinal))
        {
            return Results.StatusCode(304);
        }

        return Results.Json(View(snapshot), statusCode: 200);
    }

    private static IResult GetResult(HttpContext context, NodeRuntime runtime, string taskId)
    {
        var peer = Peer(context);

        if (!Ulid.TryParse(taskId, out _))
        {
            return Problem(404, "TASK_NOT_FOUND", "Tarea desconocida.");
        }

        var snapshot = runtime.Tasks.Get(taskId, TaskRole.Target);
        if (snapshot is null
            || !string.Equals(snapshot.CounterpartNode, peer.NodeId, StringComparison.Ordinal))
        {
            return Problem(404, "TASK_NOT_FOUND", "Tarea desconocida.");
        }

        var available = snapshot.State is TaskState.Completed or TaskState.ReviewRequired or TaskState.Failed;
        if (!available)
        {
            return Problem(409, "TASK_STATE_CONFLICT",
                $"La tarea está en {snapshot.State.ToContractString()} y no tiene resultado.");
        }

        JsonNode? result = null;
        if (!string.IsNullOrWhiteSpace(snapshot.ResultJson))
        {
            try
            {
                result = JsonNode.Parse(snapshot.ResultJson);
            }
            catch (JsonException)
            {
                result = null;
            }
        }

        return Results.Json(new JsonObject
        {
            ["task_id"] = snapshot.TaskId,
            ["state"] = snapshot.State.ToContractString(),
            ["result_available"] = true,
            ["result"] = result,
        }, statusCode: 200);
    }

    private static IResult ReplayOrConflict(TaskSnapshot existing, string hash)
    {
        if (!string.Equals(existing.TaskHash, hash, StringComparison.Ordinal))
        {
            return Problem(409, "TASK_ID_CONFLICT", "El task_id ya existe con otro contenido.");
        }

        return new ReplayResult(View(existing));
    }

    private sealed class ReplayResult(JsonObject view) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers["Idempotent-Replay"] = "true";
            return Results.Json(view, statusCode: 200).ExecuteAsync(httpContext);
        }
    }

    private static JsonObject View(TaskSnapshot snapshot)
    {
        var json = new JsonObject
        {
            ["task_id"] = snapshot.TaskId,
            ["state"] = snapshot.State.ToContractString(),
            ["state_seq"] = snapshot.StateSeq,
            ["accepted_at"] = PersistenceJson.Timestamp(snapshot.CreatedAt),
            ["approval_required"] = snapshot.State == TaskState.AwaitingLocalApproval,
            ["next_action"] = NextAction(snapshot.State),
            ["state_changed_at"] = PersistenceJson.Timestamp(snapshot.UpdatedAt),
            ["result_available"] = snapshot.State is TaskState.Completed or TaskState.ReviewRequired,
            ["cancel_requested"] = snapshot.CancelRequested,
        };

        if (snapshot.LeaseExpiresAt is { } lease)
        {
            json["lease_expires_at"] = PersistenceJson.Timestamp(lease);
        }

        return json;
    }

    private static string NextAction(TaskState state) => state switch
    {
        TaskState.WaitingApproval or TaskState.AwaitingLocalApproval => "wait_for_human",
        TaskState.Unreachable => "retry_later",
        TaskState.Done or TaskState.Completed or TaskState.ReviewRequired => "read_result",
        _ => "none",
    };

    private static int StatusFor(string code) => code switch
    {
        ValidationCodes.LineageLoop or ValidationCodes.TaskIdConflict => 409,
        ValidationCodes.ApprovalInvalid or ValidationCodes.PolicyDenied => 403,
        ValidationCodes.RepoUnknown => 422,
        _ => 400,
    };

    private static IResult Problem(int status, string code, string detail)
    {
        var json = new JsonObject
        {
            ["type"] = $"https://odw.local/errors/{code}",
            ["title"] = code,
            ["status"] = status,
            ["code"] = code,
            ["detail"] = detail,
            ["retryable"] = false,
        };

        return Results.Json(json, statusCode: status);
    }

    private static PeerIdentity Peer(HttpContext context)
        => context.Items[PeerItem] as PeerIdentity
           ?? throw new InvalidOperationException("Falta la identidad del par.");

    private static JsonArray Array(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }
}
