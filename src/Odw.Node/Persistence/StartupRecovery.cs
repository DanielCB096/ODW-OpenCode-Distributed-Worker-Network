using System.Globalization;
using Microsoft.Data.Sqlite;
using Odw.Core.Model;
using Odw.Core.Primitives;

namespace Odw.Node.Persistence;

public sealed record RecoveryResult(int InterruptedTargets, int UnreachableOrigins, int ResetOutboxActions);

/// <summary>
/// Reconciliación al arrancar (docs/PERSISTENCE.md §5.2). Nada se reejecuta:
/// las tareas de destino en curso pasan a <c>INTERRUPTED</c> y las del origen con lease
/// vencido a <c>UNREACHABLE</c>, encolando un sondeo.
/// </summary>
public sealed class StartupRecovery
{
    private readonly SqliteDatabase _database;
    private readonly TimeProvider _timeProvider;

    public StartupRecovery(SqliteDatabase database, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<RecoveryResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync(
            (connection, transaction) => Recover(connection, transaction, now),
            cancellationToken).ConfigureAwait(false);
    }

    private RecoveryResult Recover(SqliteConnection connection, SqliteTransaction transaction, DateTimeOffset now)
    {
        var interrupted = InterruptRunningTargets(connection, transaction, now);
        var unreachable = MarkExpiredOrigins(connection, transaction, now);
        var reset = ResetStuckOutbox(connection, transaction, now);

        AuditLog.Append(
            connection,
            transaction,
            "service.start",
            "node",
            "service",
            PersistenceJson.Serialize(new
            {
                interrupted_targets = interrupted,
                unreachable_origins = unreachable,
                reset_outbox_actions = reset,
            }),
            now);

        return new RecoveryResult(interrupted, unreachable, reset);
    }

    private int InterruptRunningTargets(SqliteConnection connection, SqliteTransaction transaction, DateTimeOffset now)
    {
        var rows = new List<(string TaskId, long Seq)>();

        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                "SELECT task_id, state_seq FROM tasks WHERE role = 'target' AND state = 'RUNNING';";

            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetInt64(1)));
            }
        }

        foreach (var (taskId, seq) in rows)
        {
            TransitionRaw(connection, transaction, taskId, TaskRole.Target, "INTERRUPTED", seq + 1, now);

            TaskEventLog.Append(
                connection,
                transaction,
                taskId,
                TaskRole.Target,
                seq + 1,
                Ulid.New(_timeProvider).ToString(),
                "task.interrupted",
                PersistenceJson.Serialize(new { reason = "service_restart" }),
                now);

            AuditLog.Append(
                connection,
                transaction,
                "recovery.interrupted",
                $"{taskId}/target",
                "service",
                PersistenceJson.Serialize(new { from = "RUNNING", to = "INTERRUPTED" }),
                now);
        }

        return rows.Count;
    }

    private int MarkExpiredOrigins(SqliteConnection connection, SqliteTransaction transaction, DateTimeOffset now)
    {
        var rows = new List<(string TaskId, string State, long Seq)>();
        var nowText = PersistenceJson.Timestamp(now);

        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                """
                SELECT task_id, state, state_seq
                  FROM tasks
                 WHERE role = 'origin'
                   AND state IN ('ACCEPTED', 'RUNNING')
                   AND lease_expires_at IS NOT NULL
                   AND lease_expires_at < $now;
                """;
            select.Parameters.AddWithValue("$now", nowText);

            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
            }
        }

        foreach (var (taskId, state, seq) in rows)
        {
            TransitionRaw(connection, transaction, taskId, TaskRole.Origin, "UNREACHABLE", seq + 1, now);

            TaskEventLog.Append(
                connection,
                transaction,
                taskId,
                TaskRole.Origin,
                seq + 1,
                Ulid.New(_timeProvider).ToString(),
                "task.unreachable",
                PersistenceJson.Serialize(new { reason = "lease_expired", previous_state = state }),
                now);

            AuditLog.Append(
                connection,
                transaction,
                "recovery.unreachable",
                $"{taskId}/origin",
                "service",
                PersistenceJson.Serialize(new { from = state, to = "UNREACHABLE" }),
                now);

            Outbox.OutboxSql.Insert(
                connection,
                transaction,
                new NewPendingAction(
                    OutboxActions.PollTask,
                    taskId,
                    TaskRole.Origin,
                    PersistenceJson.Serialize(new { reason = "lease_expired" }),
                    now),
                now);
        }

        return rows.Count;
    }

    private static int ResetStuckOutbox(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateTimeOffset now)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE pending_actions
               SET status = 'pending', next_attempt_at = $now, updated_at = $now
             WHERE status = 'running';
            """;
        command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
        return command.ExecuteNonQuery();
    }

    private static void TransitionRaw(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string taskId,
        TaskRole role,
        string state,
        long seq,
        DateTimeOffset now)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE tasks
               SET state = $state, state_seq = $seq, updated_at = $now
             WHERE task_id = $id AND role = $role;
            """;
        command.Parameters.AddWithValue("$state", state);
        command.Parameters.AddWithValue("$seq", seq);
        command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
        command.Parameters.AddWithValue("$id", taskId);
        command.Parameters.AddWithValue("$role", role.ToWireValue());

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException($"No se pudo reconciliar la tarea {taskId} ({role}).");
        }
    }
}
