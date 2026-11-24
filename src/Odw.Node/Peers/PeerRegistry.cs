using System.Security.Cryptography;
using System.Text;
using Odw.Core.Primitives;

namespace Odw.Node.Peers;

public sealed record PeerRegistration(
    string NodeId,
    string Token,
    string Trust = "trusted",
    IReadOnlyList<string>? Capabilities = null,
    string? Address = null);

public sealed record PeerIdentity(string NodeId, string Trust, IReadOnlyList<string> Capabilities);

/// <summary>
/// Pares configurados y autenticación por token. Solo se guarda el hash del token; la
/// comparación es en tiempo constante y exige que <c>X-Odw-Node</c> coincida con el par.
/// </summary>
public sealed class PeerRegistry
{
    private sealed record Entry(string NodeId, byte[] TokenHash, string Trust, IReadOnlyList<string> Capabilities);

    private static readonly HashSet<string> TrustLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "trusted", "limited", "blocked", "unknown",
    };

    private readonly Dictionary<string, Entry> _peers;
    private readonly Dictionary<string, PeerRegistration> _registrations;

    public PeerRegistry(IEnumerable<PeerRegistration> peers)
    {
        ArgumentNullException.ThrowIfNull(peers);

        _peers = new Dictionary<string, Entry>(StringComparer.Ordinal);
        _registrations = new Dictionary<string, PeerRegistration>(StringComparer.Ordinal);
        foreach (var peer in peers)
        {
            if (!ContractFormat.IsValidNodeId(peer.NodeId))
            {
                throw new ArgumentException($"node_id inválido: {peer.NodeId}.", nameof(peers));
            }

            if (!TrustLevels.Contains(peer.Trust))
            {
                throw new ArgumentException($"trust inválido para {peer.NodeId}: {peer.Trust}.", nameof(peers));
            }

            if (string.IsNullOrWhiteSpace(peer.Token))
            {
                throw new ArgumentException($"Token vacío para {peer.NodeId}.", nameof(peers));
            }

            if (!_peers.TryAdd(peer.NodeId, new Entry(
                    peer.NodeId,
                    SHA256.HashData(Encoding.UTF8.GetBytes(peer.Token)),
                    peer.Trust.Trim().ToLowerInvariant(),
                    peer.Capabilities ?? [])))
            {
                throw new ArgumentException($"Par duplicado: {peer.NodeId}.", nameof(peers));
            }

            _registrations[peer.NodeId] = peer;
        }
    }

    public IReadOnlyCollection<string> KnownNodes => _peers.Keys;

    public PeerIdentity? Authenticate(string? authorizationHeader, string? nodeHeader)
    {
        if (string.IsNullOrWhiteSpace(nodeHeader)
            || !_peers.TryGetValue(nodeHeader.Trim(), out var entry))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return null;
        }

        const string scheme = "Bearer ";
        if (!authorizationHeader.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var provided = authorizationHeader[scheme.Length..].Trim();
        if (provided.Length == 0)
        {
            return null;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(providedHash, entry.TokenHash)
            ? new PeerIdentity(entry.NodeId, entry.Trust, entry.Capabilities)
            : null;
    }

    public PeerIdentity? Get(string nodeId)
        => _peers.TryGetValue(nodeId, out var entry)
            ? new PeerIdentity(entry.NodeId, entry.Trust, entry.Capabilities)
            : null;

    public PeerRegistration? GetRegistration(string nodeId)
        => _registrations.TryGetValue(nodeId, out var registration) ? registration : null;
}
