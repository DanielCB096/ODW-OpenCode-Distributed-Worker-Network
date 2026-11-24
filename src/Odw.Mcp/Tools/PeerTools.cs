using System.Text.Json.Nodes;
using Odw.Core.Primitives;
using Odw.Mcp.Abstractions;

namespace Odw.Mcp.Tools;

/// <summary>Herramientas de solo lectura sobre el directorio de pares.</summary>
public static class PeerTools
{
    public static IEnumerable<McpTool> Create(IPeerDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        yield return new McpTool(
            "odw_list_peers",
            "Lista los pares conocidos y su estado (online/offline), carga y capacidades.",
            JsonTools.Schema(
                """
                {
                  "type": "object",
                  "properties": {
                    "include_offline": { "type": "boolean", "default": false }
                  },
                  "additionalProperties": false
                }
                """),
            (arguments, cancellationToken) => ListAsync(directory, arguments, cancellationToken));

        yield return new McpTool(
            "odw_get_peer_status",
            "Devuelve recursos y capacidades de un par concreto. No contacta al par: usa el último estado conocido.",
            JsonTools.Schema(
                """
                {
                  "type": "object",
                  "properties": {
                    "node_id": { "type": "string", "pattern": "^[A-Z0-9][A-Z0-9-]{1,31}$" }
                  },
                  "required": ["node_id"],
                  "additionalProperties": false
                }
                """),
            (arguments, cancellationToken) => GetAsync(directory, arguments, cancellationToken));
    }

    private static async Task<McpToolResult> ListAsync(
        IPeerDirectory directory,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var includeOffline = JsonTools.GetBool(arguments, "include_offline") ?? false;
        var peers = await directory.ListAsync(includeOffline, cancellationToken).ConfigureAwait(false);

        var array = new JsonArray();
        foreach (var peer in peers)
        {
            array.Add(ToJson(peer));
        }

        var text = peers.Count == 0
            ? "No hay pares configurados."
            : $"{peers.Count} par(es): " + string.Join(", ", peers.Select(peer => $"{peer.NodeId} ({peer.Status})"));

        return McpToolResult.Ok(text, new JsonObject { ["peers"] = array });
    }

    private static async Task<McpToolResult> GetAsync(
        IPeerDirectory directory,
        JsonObject arguments,
        CancellationToken cancellationToken)
    {
        var nodeId = JsonTools.GetString(arguments, "node_id");
        if (!ContractFormat.IsValidNodeId(nodeId))
        {
            return McpToolResult.Error("CONTRACT_INVALID", "node_id inválido.");
        }

        var peer = await directory.GetAsync(nodeId!, cancellationToken).ConfigureAwait(false);
        if (peer is null)
        {
            return McpToolResult.Error("PEER_UNKNOWN", $"No hay un par configurado con node_id {nodeId}.");
        }

        if (!peer.IsOnline)
        {
            return McpToolResult.Error(
                "PEER_OFFLINE",
                $"El par {peer.NodeId} no está online (último contacto: {peer.LastSeenAt?.ToString("O") ?? "nunca"}).",
                retryable: true,
                extra: new JsonObject { ["node_id"] = peer.NodeId });
        }

        return McpToolResult.Ok(
            $"Par {peer.NodeId}: {peer.Status}, {peer.ActiveTasks} tarea(s) activa(s).",
            ToJson(peer));
    }

    internal static JsonObject ToJson(PeerStatusSnapshot peer)
    {
        var resources = new JsonObject();
        JsonTools.AddIfNotNull(resources, "ram_available_gb", JsonValue.Create(peer.RamAvailableGb));
        JsonTools.AddIfNotNull(resources, "cpu_usage_percent", JsonValue.Create(peer.CpuUsagePercent));
        JsonTools.AddIfNotNull(resources, "gpu_available", JsonValue.Create(peer.GpuAvailable));

        var result = new JsonObject
        {
            ["node_id"] = peer.NodeId,
            ["status"] = peer.Status,
            ["trust"] = peer.Trust,
            ["active_tasks"] = peer.ActiveTasks,
            ["queued_tasks"] = peer.QueuedTasks,
            ["accepts"] = JsonTools.ToArray(peer.Accepts),
            ["capabilities"] = JsonTools.ToArray(peer.Capabilities),
            ["resources"] = resources,
        };

        JsonTools.AddIfNotNull(result, "last_seen_at",
            peer.LastSeenAt is null ? null : JsonValue.Create(peer.LastSeenAt.Value.ToString("O")));
        JsonTools.AddIfNotNull(result, "load_score", JsonValue.Create(peer.LoadScore));

        return result;
    }
}
