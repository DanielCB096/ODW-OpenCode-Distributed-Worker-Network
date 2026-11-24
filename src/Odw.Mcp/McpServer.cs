using System.Text.Json.Nodes;

namespace Odw.Mcp;

/// <summary>
/// Servidor MCP sin estado: initialize, ping, tools/list y tools/call.
/// No hay mensajes iniciados por el servidor ni sesiones (GET /mcp no aplica).
/// </summary>
public sealed class McpServer
{
    public const string LatestProtocolVersion = "2025-06-18";

    private static readonly HashSet<string> SupportedProtocols =
    [
        "2025-06-18",
        "2024-11-05",
    ];

    private readonly Dictionary<string, McpTool> _tools;
    private readonly string _serverName;
    private readonly string _serverVersion;

    public McpServer(IEnumerable<McpTool> tools, string serverName = "odw", string serverVersion = "0.1.0")
    {
        ArgumentNullException.ThrowIfNull(tools);

        _tools = new Dictionary<string, McpTool>(StringComparer.Ordinal);
        foreach (var tool in tools)
        {
            if (!_tools.TryAdd(tool.Name, tool))
            {
                throw new InvalidOperationException($"Herramienta MCP duplicada: {tool.Name}.");
            }
        }

        _serverName = serverName;
        _serverVersion = serverVersion;
    }

    public IReadOnlyCollection<McpTool> Tools => _tools.Values;

    public async Task<JsonObject?> HandleAsync(JsonObject request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hasId = request.ContainsKey("id");
        var id = hasId ? request["id"] : null;

        if (request["jsonrpc"]?.GetValue<string>() != JsonRpc.Version)
        {
            return hasId ? JsonRpc.Error(id, JsonRpc.ErrorCodes.InvalidRequest, "jsonrpc debe ser \"2.0\".") : null;
        }

        var method = request["method"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(method))
        {
            return hasId ? JsonRpc.Error(id, JsonRpc.ErrorCodes.InvalidRequest, "Falta method.") : null;
        }

        var parameters = request["params"] as JsonObject;

        // Una notificación no lleva respuesta, aunque falle.
        if (!hasId)
        {
            return null;
        }

        try
        {
            return method switch
            {
                "initialize" => JsonRpc.Success(id, Initialize(parameters)),
                "ping" => JsonRpc.Success(id, new JsonObject()),
                "tools/list" => JsonRpc.Success(id, ListTools()),
                "tools/call" => await CallToolAsync(id, parameters, cancellationToken).ConfigureAwait(false),
                _ => JsonRpc.Error(id, JsonRpc.ErrorCodes.MethodNotFound, $"Método no soportado: {method}."),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return JsonRpc.Error(
                id,
                JsonRpc.ErrorCodes.InternalError,
                "Error interno del servidor MCP.",
                new JsonObject { ["code"] = "INTERNAL" });
        }
    }

    private JsonObject Initialize(JsonObject? parameters)
    {
        var requested = parameters?["protocolVersion"]?.GetValue<string>();
        var protocol = requested is not null && SupportedProtocols.Contains(requested)
            ? requested
            : LatestProtocolVersion;

        return new JsonObject
        {
            ["protocolVersion"] = protocol,
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject { ["listChanged"] = false },
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = _serverName,
                ["version"] = _serverVersion,
            },
            ["instructions"] =
                "ODW coordina tareas entre PCs. Usa las herramientas odw_* para consultar pares y "
                + "tareas. La aprobación de delegaciones es humana y nunca está disponible como herramienta.",
        };
    }

    private JsonObject ListTools()
    {
        var tools = new JsonArray();

        foreach (var tool in _tools.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            tools.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone(),
                ["annotations"] = new JsonObject { ["readOnlyHint"] = true },
            });
        }

        return new JsonObject { ["tools"] = tools };
    }

    private async Task<JsonObject> CallToolAsync(
        JsonNode? id,
        JsonObject? parameters,
        CancellationToken cancellationToken)
    {
        var name = parameters?["name"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(name) || !_tools.TryGetValue(name, out var tool))
        {
            return JsonRpc.Error(
                id,
                JsonRpc.ErrorCodes.InvalidParams,
                $"Herramienta desconocida: {name}.",
                new JsonObject { ["code"] = "TOOL_NOT_FOUND" });
        }

        var arguments = parameters?["arguments"] as JsonObject ?? new JsonObject();
        var result = await tool.Handler(arguments, cancellationToken).ConfigureAwait(false);

        var payload = new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = result.Text,
            }),
            ["isError"] = result.IsError,
        };

        if (result.StructuredContent is not null)
        {
            payload["structuredContent"] = result.StructuredContent.DeepClone();
        }

        return JsonRpc.Success(id, payload);
    }
}
