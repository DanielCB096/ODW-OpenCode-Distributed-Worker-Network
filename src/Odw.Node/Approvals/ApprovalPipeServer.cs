using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Odw.Approvals;
using Odw.Core.Model;
using Odw.Node.Persistence;

namespace Odw.Node.Approvals;

public sealed record ApprovalPipeOptions
{
    public string Name { get; init; } = ApprovalPipeClient.DefaultPipeName;

    /// <summary>SDDL para la DACL del pipe. `null` usa la ACL por defecto del proceso (tests).</summary>
    public string? SecurityDescriptor { get; init; } = "D:(A;;GA;;;BA)(A;;GA;;;SY)";
}

/// <summary>
/// Servidor de aprobaciones por named pipe. Cada conexión envía una línea JSON con un comando
/// (`list`, `approve`, `deny`, `expire`) y recibe una línea de respuesta.
/// </summary>
public sealed class ApprovalPipeServer : IAsyncDisposable
{
    private readonly ApprovalService _approvals;
    private readonly ApprovalCoordinator _coordinator;
    private readonly ApprovalPipeOptions _options;
    private readonly Action<string>? _log;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private bool _disposed;

    public ApprovalPipeServer(
        ApprovalService approvals,
        ApprovalCoordinator coordinator,
        ApprovalPipeOptions options,
        Action<string>? log = null)
    {
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log;

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            throw new ArgumentException("Falta el nombre del pipe.", nameof(options));
        }
    }

    public string PipeName => _options.Name;

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _loop = Task.Run(() => AcceptLoopAsync(_stopping.Token));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping.Cancel();

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stopping.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();

                try
                {
                    await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await HandleAsync(pipe, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log?.Invoke($"pipe: {exception.Message}");
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(_options.SecurityDescriptor))
        {
            var security = new PipeSecurity();
            security.SetSecurityDescriptorSddlForm(_options.SecurityDescriptor);
            return NamedPipeServerStreamAcl.Create(
                _options.Name,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                security);
        }

        return new NamedPipeServerStream(
            _options.Name,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    private async Task HandleAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true)
        {
            AutoFlush = true,
        };

        var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (line is null)
        {
            return;
        }

        JsonObject response;
        try
        {
            response = await DispatchAsync(line, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            response = Error("INTERNAL", exception.Message);
        }

        await writer.WriteLineAsync(response.ToJsonString()).ConfigureAwait(false);
    }

    private async Task<JsonObject> DispatchAsync(string line, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        var command = root.TryGetProperty("command", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

        switch (command)
        {
            case "list":
                return Ok(ListJson());

            case "expire":
                return Ok(new JsonObject { ["expired"] = await _approvals.ExpireAsync(cancellationToken) });

            case "approve":
            {
                var id = Required(root, "id");
                var by = Optional(root, "by") ?? "human:cli";
                var result = await _coordinator.ApproveAsync(id, by, cancellationToken).ConfigureAwait(false);
                return Decision(result);
            }

            case "deny":
            {
                var id = Required(root, "id");
                var by = Optional(root, "by") ?? "human:cli";
                var reason = Optional(root, "reason");
                var result = await _coordinator.DenyAsync(id, by, reason, cancellationToken).ConfigureAwait(false);
                return Decision(result);
            }

            default:
                return Error("CONTRACT_INVALID", $"Comando desconocido: {command}.");
        }
    }

    private JsonArray ListJson()
    {
        var array = new JsonArray();
        foreach (var record in _approvals.ListPending())
        {
            array.Add(RecordJson(record));
        }

        return array;
    }

    private JsonObject Decision(ApprovalDecisionResult result) => result.Status switch
    {
        ApprovalDecisionStatus.Approved or ApprovalDecisionStatus.Denied =>
            Ok(result.Record is null ? new JsonObject() : RecordJson(result.Record)),

        ApprovalDecisionStatus.NotFound => Error("APPROVAL_NOT_FOUND", "No existe la aprobación."),
        ApprovalDecisionStatus.Expired => Error("APPROVAL_EXPIRED", "La aprobación expiró."),
        ApprovalDecisionStatus.AlreadyDecided => Error("APPROVAL_ALREADY_DECIDED", "Ya fue decidida."),
        _ => Error("APPROVAL_INVALID", "El hash no coincide."),
    };

    private static JsonObject RecordJson(ApprovalRecord record) => new()
    {
        ["approval_id"] = record.ApprovalId,
        ["task_id"] = record.TaskId,
        ["role"] = record.Role.ToWireValue(),
        ["task_hash"] = record.TaskHash,
        ["summary"] = record.Summary,
        ["status"] = record.Status,
        ["created_at"] = PersistenceJson.Timestamp(record.CreatedAt),
        ["expires_at"] = PersistenceJson.Timestamp(record.ExpiresAt),
        ["decided_at"] = record.DecidedAt is null ? null : PersistenceJson.Timestamp(record.DecidedAt.Value),
        ["decided_by"] = record.DecidedBy,
        ["reason"] = record.Reason,
    };

    private static JsonObject Ok(JsonNode? data) => new()
    {
        ["ok"] = true,
        ["data"] = data,
    };

    private static JsonObject Error(string code, string detail) => new()
    {
        ["ok"] = false,
        ["code"] = code,
        ["detail"] = detail,
    };

    private static string Required(JsonElement root, string name)
        => Optional(root, name) ?? throw new InvalidOperationException($"Falta {name}.");

    private static string? Optional(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
