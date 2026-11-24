using System.Text.Json;
using Microsoft.Data.Sqlite;
using Odw.Mcp.Abstractions;

namespace Odw.Node.Persistence.Peers;

/// <summary>
/// Pares desde SQLite. El estado "online" se deriva de <c>last_seen_at</c>; no se contacta
/// al par (la fachada MCP es de solo lectura local).
/// </summary>
public sealed class SqlitePeerDirectory : IPeerDirectory
{
    private sealed record Details(
        int ActiveTasks,
        int QueuedTasks,
        double? LoadScore,
        IReadOnlyList<string> Accepts,
        IReadOnlyList<string> Capabilities,
        double? RamAvailableGb,
        double? CpuUsagePercent,
        bool? GpuAvailable);

    private readonly SqliteDatabase _database;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _onlineWindow;

    public SqlitePeerDirectory(SqliteDatabase database, TimeProvider timeProvider, TimeSpan? onlineWindow = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _onlineWindow = onlineWindow ?? TimeSpan.FromSeconds(90);

        if (_onlineWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(onlineWindow));
        }
    }

    public Task<IReadOnlyList<PeerStatusSnapshot>> ListAsync(bool includeOffline, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT node_id, trust, last_seen_at, last_status
              FROM peers
             WHERE enabled = 1
             ORDER BY node_id;
            """;

        var peers = new List<PeerStatusSnapshot>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var peer = Map(reader);
            if (includeOffline || peer.IsOnline)
            {
                peers.Add(peer);
            }
        }

        return Task.FromResult<IReadOnlyList<PeerStatusSnapshot>>(peers);
    }

    public Task<PeerStatusSnapshot?> GetAsync(string nodeId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        cancellationToken.ThrowIfCancellationRequested();

        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT node_id, trust, last_seen_at, last_status
              FROM peers
             WHERE node_id = $id AND enabled = 1;
            """;
        command.Parameters.AddWithValue("$id", nodeId);

        using var reader = command.ExecuteReader();
        return Task.FromResult(reader.Read() ? Map(reader) : null);
    }

    private PeerStatusSnapshot Map(SqliteDataReader reader)
    {
        var nodeId = reader.GetString(0);
        var trust = reader.GetString(1);
        var lastSeen = PersistenceJson.ParseTimestampOrNull(
            reader.IsDBNull(2) ? null : reader.GetString(2));
        var details = ParseDetails(reader.IsDBNull(3) ? null : reader.GetString(3));

        var online = lastSeen is not null && _timeProvider.GetUtcNow() - lastSeen.Value <= _onlineWindow;

        return new PeerStatusSnapshot(
            nodeId,
            online ? "online" : "offline",
            trust,
            lastSeen,
            details.ActiveTasks,
            details.QueuedTasks,
            details.LoadScore,
            details.Accepts,
            details.Capabilities,
            details.RamAvailableGb,
            details.CpuUsagePercent,
            details.GpuAvailable);
    }

    private static Details ParseDetails(string? json)
    {
        var empty = new Details(0, 0, null, [], [], null, null, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return empty;
            }

            var resources = root.TryGetProperty("resources", out var r) && r.ValueKind == JsonValueKind.Object
                ? r
                : default;

            return new Details(
                GetInt(resources, "active_tasks") ?? 0,
                GetInt(resources, "queued_tasks") ?? 0,
                GetDouble(root, "load_score"),
                GetStrings(root, "accepts"),
                GetStrings(root, "capabilities"),
                GetDouble(resources, "ram_available_gb"),
                GetDouble(resources, "cpu_usage_percent"),
                GetBool(resources, "gpu_available"));
        }
        catch (JsonException)
        {
            return empty;
        }
    }

    private static int? GetInt(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.TryGetInt32(out var result)
            ? result
            : null;

    private static double? GetDouble(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.TryGetDouble(out var result)
            ? result
            : null;

    private static bool? GetBool(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static IReadOnlyList<string> GetStrings(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                items.Add(item.GetString()!);
            }
        }

        return items;
    }
}
