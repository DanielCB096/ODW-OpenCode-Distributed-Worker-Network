using System.Globalization;
using Microsoft.Data.Sqlite;
using Odw.Core.Model;

namespace Odw.Node.Persistence.Outbox;

internal static class OutboxSql
{
    public static long Insert(
        SqliteConnection connection,
        SqliteTransaction transaction,
        NewPendingAction action,
        DateTimeOffset createdAt)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO pending_actions
                (action, task_id, role, payload_json, status, attempts, next_attempt_at, created_at, updated_at)
            VALUES
                ($action, $task, $role, $payload, 'pending', 0, $next, $created, $created);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$action", action.Action);
        command.Parameters.AddWithValue("$task", (object?)action.TaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$role", action.Role is null ? DBNull.Value : action.Role.Value.ToWireValue());
        command.Parameters.AddWithValue("$payload", action.PayloadJson);
        command.Parameters.AddWithValue("$next", PersistenceJson.Timestamp(action.NextAttemptAt));
        command.Parameters.AddWithValue("$created", PersistenceJson.Timestamp(createdAt));
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static PendingAction Read(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.IsDBNull(3) ? null : ParseRole(reader.GetString(3)),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetInt32(6),
        PersistenceJson.ParseTimestamp(reader.GetString(7)),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        PersistenceJson.ParseTimestamp(reader.GetString(9)),
        PersistenceJson.ParseTimestamp(reader.GetString(10)));

    public const string Columns =
        "id, action, task_id, role, payload_json, status, attempts, next_attempt_at, last_error, created_at, updated_at";

    private static TaskRole? ParseRole(string value)
        => TaskStateExtensions.TryParseRole(value, out var role) ? role : null;
}
