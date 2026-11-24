using System.Globalization;
using Microsoft.Data.Sqlite;
using Odw.Core.Model;

namespace Odw.Node.Persistence;

/// <summary>Eventos de tarea append-only con cadena de hash por (task_id, role).</summary>
internal static class TaskEventLog
{
    public static void Append(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string taskId,
        TaskRole role,
        long seq,
        string eventId,
        string type,
        string payloadJson,
        DateTimeOffset at)
    {
        var previous = LastHash(connection, transaction, taskId, role);
        var createdAt = PersistenceJson.Timestamp(at);
        var hash = ChainHash.Compute(
            previous,
            taskId,
            role.ToWireValue(),
            seq.ToString(CultureInfo.InvariantCulture),
            eventId,
            type,
            payloadJson,
            createdAt);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO task_events (task_id, role, seq, event_id, type, payload_json, created_at, prev_hash, hash)
            VALUES ($task, $role, $seq, $event, $type, $payload, $created, $prev, $hash);
            """;
        command.Parameters.AddWithValue("$task", taskId);
        command.Parameters.AddWithValue("$role", role.ToWireValue());
        command.Parameters.AddWithValue("$seq", seq);
        command.Parameters.AddWithValue("$event", eventId);
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$payload", payloadJson);
        command.Parameters.AddWithValue("$created", createdAt);
        command.Parameters.AddWithValue("$prev", previous);
        command.Parameters.AddWithValue("$hash", hash);
        command.ExecuteNonQuery();
    }

    public static ChainVerification Verify(SqliteConnection connection, string taskId, TaskRole role)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT seq, event_id, type, payload_json, created_at, prev_hash, hash
            FROM task_events
            WHERE task_id = $task AND role = $role
            ORDER BY seq;
            """;
        command.Parameters.AddWithValue("$task", taskId);
        command.Parameters.AddWithValue("$role", role.ToWireValue());

        using var reader = command.ExecuteReader();
        var expectedSeq = 0L;
        var previous = ChainHash.Genesis;

        while (reader.Read())
        {
            var seq = reader.GetInt64(0);
            var eventId = reader.GetString(1);
            var type = reader.GetString(2);
            var payload = reader.GetString(3);
            var createdAt = reader.GetString(4);
            var storedPrev = reader.GetString(5);
            var storedHash = reader.GetString(6);

            if (seq != expectedSeq)
            {
                return new ChainVerification(false, seq, $"Secuencia inesperada: {seq} (esperada {expectedSeq}).");
            }

            if (!string.Equals(storedPrev, previous, StringComparison.Ordinal))
            {
                return new ChainVerification(false, seq, "prev_hash no coincide con el evento anterior.");
            }

            var computed = ChainHash.Compute(
                previous,
                taskId,
                role.ToWireValue(),
                seq.ToString(CultureInfo.InvariantCulture),
                eventId,
                type,
                payload,
                createdAt);

            if (!string.Equals(computed, storedHash, StringComparison.Ordinal))
            {
                return new ChainVerification(false, seq, "hash del evento no coincide.");
            }

            previous = storedHash;
            expectedSeq++;
        }

        return ChainVerification.Valid;
    }

    private static string LastHash(SqliteConnection connection, SqliteTransaction transaction, string taskId, TaskRole role)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT hash FROM task_events WHERE task_id = $task AND role = $role ORDER BY seq DESC LIMIT 1;";
        command.Parameters.AddWithValue("$task", taskId);
        command.Parameters.AddWithValue("$role", role.ToWireValue());

        var value = command.ExecuteScalar();
        return value is string hash ? hash : ChainHash.Genesis;
    }
}
