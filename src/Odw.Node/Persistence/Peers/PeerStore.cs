using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Odw.Node.Peers;

namespace Odw.Node.Persistence.Peers;

/// <summary>Sincroniza los pares configurados con SQLite para la fachada y el estado.</summary>
public sealed class PeerStore
{
    private readonly SqliteDatabase _database;
    private readonly TimeProvider _timeProvider;

    public PeerStore(SqliteDatabase database, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task SyncAsync(IEnumerable<PeerRegistration> peers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(peers);
        var now = _timeProvider.GetUtcNow();

        var snapshot = peers.ToArray();
        if (snapshot.Length == 0)
        {
            return;
        }

        await _database.Writer.WriteAsync((connection, transaction) =>
        {
            foreach (var peer in snapshot)
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(peer.Token));
                var keyId = Convert.ToHexStringLower(hash[..4]);

                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT INTO peers
                        (node_id, enabled, address, key_id, token_hash, trust, added_at)
                    VALUES
                        ($id, 1, $address, $key, $hash, $trust, $now)
                    ON CONFLICT(node_id) DO UPDATE SET
                        enabled = 1,
                        address = excluded.address,
                        key_id = excluded.key_id,
                        token_hash = excluded.token_hash,
                        trust = excluded.trust;
                    """;
                command.Parameters.AddWithValue("$id", peer.NodeId);
                command.Parameters.AddWithValue("$address", peer.Address ?? string.Empty);
                command.Parameters.AddWithValue("$key", keyId);
                command.Parameters.AddWithValue("$hash", hash);
                command.Parameters.AddWithValue("$trust", peer.Trust.ToLowerInvariant());
                command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
                command.ExecuteNonQuery();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task TouchAsync(
        string nodeId,
        string? statusJson,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);

        await _database.Writer.WriteAsync((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE peers
                   SET last_seen_at = $seen,
                       last_status = COALESCE($status, last_status)
                 WHERE node_id = $id;
                """;
            command.Parameters.AddWithValue("$seen", PersistenceJson.Timestamp(at));
            command.Parameters.AddWithValue("$status", (object?)statusJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$id", nodeId);
            command.ExecuteNonQuery();
        }, cancellationToken).ConfigureAwait(false);
    }
}
