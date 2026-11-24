namespace Odw.Mcp.Abstractions;

/// <summary>Vista de un par para la fachada MCP. Solo lectura: no dispara llamadas de red.</summary>
public sealed record PeerStatusSnapshot(
    string NodeId,
    string Status,
    string Trust,
    DateTimeOffset? LastSeenAt,
    int ActiveTasks,
    int QueuedTasks,
    double? LoadScore,
    IReadOnlyList<string> Accepts,
    IReadOnlyList<string> Capabilities,
    double? RamAvailableGb,
    double? CpuUsagePercent,
    bool? GpuAvailable)
{
    public bool IsOnline => string.Equals(Status, "online", StringComparison.Ordinal);
}

public interface IPeerDirectory
{
    Task<IReadOnlyList<PeerStatusSnapshot>> ListAsync(bool includeOffline, CancellationToken cancellationToken);

    Task<PeerStatusSnapshot?> GetAsync(string nodeId, CancellationToken cancellationToken);
}
