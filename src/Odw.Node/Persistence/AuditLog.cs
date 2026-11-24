using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Odw.Node.Persistence;

/// <summary>Auditoría append-only con cadena de hash (docs/PERSISTENCE.md §7).</summary>
internal static class AuditLog
{
    public static void Append(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string entryType,
        string subject,
        string actor,
        string payloadJson,
        DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryType);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        var (lastSeq, previousHash) = Last(connection, transaction);
        var seq = lastSeq + 1;
        var createdAt = PersistenceJson.Timestamp(at);
        var hash = ChainHash.Compute(
            previousHash,
            seq.ToString(CultureInfo.InvariantCulture),
            entryType,
            subject,
            actor,
            payloadJson,
            createdAt);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_log (seq, entry_type, subject, actor, payload_json, created_at, prev_hash, hash)
            VALUES ($seq, $type, $subject, $actor, $payload, $created, $prev, $hash);
            """;
        command.Parameters.AddWithValue("$seq", seq);
        command.Parameters.AddWithValue("$type", entryType);
        command.Parameters.AddWithValue("$subject", subject);
        command.Parameters.AddWithValue("$actor", actor);
        command.Parameters.AddWithValue("$payload", payloadJson);
        command.Parameters.AddWithValue("$created", createdAt);
        command.Parameters.AddWithValue("$prev", previousHash);
        command.Parameters.AddWithValue("$hash", hash);
        command.ExecuteNonQuery();
    }

    public static ChainVerification Verify(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT seq, entry_type, subject, actor, payload_json, created_at, prev_hash, hash
            FROM audit_log
            ORDER BY seq;
            """;

        using var reader = command.ExecuteReader();
        var expectedSeq = 1L;
        var previous = ChainHash.Genesis;

        while (reader.Read())
        {
            var seq = reader.GetInt64(0);
            var entryType = reader.GetString(1);
            var subject = reader.GetString(2);
            var actor = reader.GetString(3);
            var payload = reader.GetString(4);
            var createdAt = reader.GetString(5);
            var storedPrev = reader.GetString(6);
            var storedHash = reader.GetString(7);

            if (seq != expectedSeq)
            {
                return new ChainVerification(false, seq, $"Secuencia inesperada: {seq} (esperada {expectedSeq}).");
            }

            if (!string.Equals(storedPrev, previous, StringComparison.Ordinal))
            {
                return new ChainVerification(false, seq, "prev_hash no coincide con el eslabón anterior.");
            }

            var computed = ChainHash.Compute(
                previous,
                seq.ToString(CultureInfo.InvariantCulture),
                entryType,
                subject,
                actor,
                payload,
                createdAt);

            if (!string.Equals(computed, storedHash, StringComparison.Ordinal))
            {
                return new ChainVerification(false, seq, "hash de auditoría no coincide.");
            }

            previous = storedHash;
            expectedSeq++;
        }

        return ChainVerification.Valid;
    }

    private static (long Seq, string Hash) Last(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT seq, hash FROM audit_log ORDER BY seq DESC LIMIT 1;";

        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetString(1)) : (0L, ChainHash.Genesis);
    }
}
