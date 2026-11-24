using System.Globalization;
using Microsoft.Data.Sqlite;
using Odw.Core.Model;
using Odw.Core.Primitives;

namespace Odw.Node.Persistence.Tasks;

/// <summary>
/// Acceso a tareas y eventos. Toda escritura pasa por <see cref="SqliteWriter"/> y cada
/// transición es compare-and-set sobre <c>(state, state_seq)</c>.
/// </summary>
public sealed class TaskStore
{
    private const string TaskColumns =
        """
        task_id, role, counterpart_node, parent_task_id, kind, mode, state, state_seq, task_hash,
        request_json, origin_node, target_node, created_at, updated_at, expires_at, lease_expires_at,
        cancel_requested, attempts, workspace_path, executor_ref, result_json, finished_at
        """;

    private readonly SqliteDatabase _database;
    private readonly TimeProvider _timeProvider;

    public TaskStore(SqliteDatabase database, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<TaskSnapshot> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreate(request);

        return await _database.Writer.WriteAsync((connection, transaction) =>
        {
            InsertTask(connection, transaction, request);
            TaskEventLog.Append(
                connection,
                transaction,
                request.TaskId,
                request.Role,
                0,
                Ulid.New(_timeProvider).ToString(),
                request.InitialEventType,
                request.InitialEventPayloadJson,
                request.CreatedAt);

            AuditLog.Append(
                connection,
                transaction,
                "task.created",
                $"{request.TaskId}/{request.Role.ToWireValue()}",
                "service",
                request.InitialEventPayloadJson,
                request.CreatedAt);

            if (request.NextAction is not null)
            {
                Outbox.OutboxSql.Insert(connection, transaction, request.NextAction, request.CreatedAt);
            }

            return Read(connection, transaction, request.TaskId, request.Role)
                ?? throw new InvalidOperationException("La tarea no se pudo leer tras crearla.");
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TransitionResult> TransitionAsync(
        TransitionTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await _database.Writer.WriteAsync((connection, transaction) =>
        {
            var current = Read(connection, transaction, request.TaskId, request.Role);
            if (current is null)
            {
                return new TransitionResult(TransitionStatus.NotFound, 0);
            }

            if (current.State != request.ExpectedState || current.StateSeq != request.ExpectedSeq)
            {
                return new TransitionResult(TransitionStatus.Conflict, current.StateSeq);
            }

            var nextSeq = current.StateSeq + 1;

            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText =
                    """
                    UPDATE tasks
                       SET state = $state,
                           state_seq = $seq,
                           updated_at = $now,
                           lease_expires_at = COALESCE($lease, lease_expires_at),
                           finished_at = COALESCE($finished, finished_at),
                           cancel_requested = COALESCE($cancel, cancel_requested),
                           executor_ref = COALESCE($executor, executor_ref),
                           result_json = COALESCE($result, result_json)
                     WHERE task_id = $id AND role = $role AND state = $expected AND state_seq = $expectedSeq;
                    """;
                update.Parameters.AddWithValue("$state", request.NextState.ToContractString());
                update.Parameters.AddWithValue("$seq", nextSeq);
                update.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(request.Now));
                update.Parameters.AddWithValue("$lease", (object?)PersistenceJson.TimestampOrNull(request.LeaseExpiresAt) ?? DBNull.Value);
                update.Parameters.AddWithValue("$finished", (object?)PersistenceJson.TimestampOrNull(request.FinishedAt) ?? DBNull.Value);
                update.Parameters.AddWithValue("$cancel", request.CancelRequested is null ? DBNull.Value : request.CancelRequested.Value ? 1 : 0);
                update.Parameters.AddWithValue("$executor", (object?)request.ExecutorRef ?? DBNull.Value);
                update.Parameters.AddWithValue("$result", (object?)request.ResultJson ?? DBNull.Value);
                update.Parameters.AddWithValue("$id", request.TaskId);
                update.Parameters.AddWithValue("$role", request.Role.ToWireValue());
                update.Parameters.AddWithValue("$expected", request.ExpectedState.ToContractString());
                update.Parameters.AddWithValue("$expectedSeq", request.ExpectedSeq);

                if (update.ExecuteNonQuery() != 1)
                {
                    return new TransitionResult(TransitionStatus.Conflict, current.StateSeq);
                }
            }

            TaskEventLog.Append(
                connection,
                transaction,
                request.TaskId,
                request.Role,
                nextSeq,
                Ulid.New(_timeProvider).ToString(),
                request.EventType,
                request.EventPayloadJson,
                request.Now);

            AuditLog.Append(
                connection,
                transaction,
                "task.transition",
                $"{request.TaskId}/{request.Role.ToWireValue()}",
                "service",
                PersistenceJson.Serialize(new
                {
                    from = current.State.ToContractString(),
                    to = request.NextState.ToContractString(),
                    trigger = request.EventType,
                    seq = nextSeq,
                }),
                request.Now);

            if (request.NextAction is not null)
            {
                Outbox.OutboxSql.Insert(connection, transaction, request.NextAction, request.Now);
            }

            return new TransitionResult(TransitionStatus.Applied, nextSeq);
        }, cancellationToken).ConfigureAwait(false);
    }

    public TaskSnapshot? Get(string taskId, TaskRole role)
    {
        using var connection = _database.OpenReadConnection();
        return Read(connection, null, taskId, role);
    }

    public IReadOnlyList<TaskSnapshot> ListByState(TaskRole role, params TaskState[] states)
    {
        ArgumentNullException.ThrowIfNull(states);

        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();

        var placeholders = new List<string>(states.Length);
        for (var i = 0; i < states.Length; i++)
        {
            placeholders.Add($"$state{i}");
            command.Parameters.AddWithValue($"$state{i}", states[i].ToContractString());
        }

        command.CommandText = states.Length == 0
            ? $"SELECT {TaskColumns} FROM tasks WHERE role = $role ORDER BY updated_at;"
            : $"SELECT {TaskColumns} FROM tasks WHERE role = $role AND state IN ({string.Join(", ", placeholders)}) ORDER BY updated_at;";
        command.Parameters.AddWithValue("$role", role.ToWireValue());

        var results = new List<TaskSnapshot>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(Map(reader));
        }

        return results;
    }

    public IReadOnlyList<TaskEventRecord> GetEvents(string taskId, TaskRole role)
    {
        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT seq, event_id, type, payload_json, created_at, hash
            FROM task_events
            WHERE task_id = $task AND role = $role
            ORDER BY seq;
            """;
        command.Parameters.AddWithValue("$task", taskId);
        command.Parameters.AddWithValue("$role", role.ToWireValue());

        var events = new List<TaskEventRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new TaskEventRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                PersistenceJson.ParseTimestamp(reader.GetString(4)),
                reader.GetString(5)));
        }

        return events;
    }

    public ChainVerification VerifyEvents(string taskId, TaskRole role)
    {
        using var connection = _database.OpenReadConnection();
        return TaskEventLog.Verify(connection, taskId, role);
    }

    public ChainVerification VerifyAudit()
    {
        using var connection = _database.OpenReadConnection();
        return AuditLog.Verify(connection);
    }

    private static TaskSnapshot? Read(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string taskId,
        TaskRole role)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {TaskColumns} FROM tasks WHERE task_id = $id AND role = $role;";
        command.Parameters.AddWithValue("$id", taskId);
        command.Parameters.AddWithValue("$role", role.ToWireValue());

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    private static TaskSnapshot Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        TaskStateExtensions.TryParseRole(reader.GetString(1), out var role)
            ? role
            : throw new InvalidDataException($"Rol desconocido: {reader.GetString(1)}."),
        reader.IsDBNull(2) ? null : reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        ParseKind(reader.GetString(4)),
        ParseMode(reader.GetString(5)),
        TaskStateExtensions.TryParseContractState(reader.GetString(6), out var state)
            ? state
            : throw new InvalidDataException($"Estado desconocido: {reader.GetString(6)}."),
        reader.GetInt64(7),
        reader.GetString(8),
        reader.GetString(9),
        reader.GetString(10),
        reader.GetString(11),
        PersistenceJson.ParseTimestamp(reader.GetString(12)),
        PersistenceJson.ParseTimestamp(reader.GetString(13)),
        PersistenceJson.ParseTimestampOrNull(reader.IsDBNull(14) ? null : reader.GetString(14)),
        PersistenceJson.ParseTimestampOrNull(reader.IsDBNull(15) ? null : reader.GetString(15)),
        reader.GetInt64(16) != 0,
        reader.GetInt32(17),
        reader.IsDBNull(18) ? null : reader.GetString(18),
        reader.IsDBNull(19) ? null : reader.GetString(19),
        reader.IsDBNull(20) ? null : reader.GetString(20),
        PersistenceJson.ParseTimestampOrNull(reader.IsDBNull(21) ? null : reader.GetString(21)));

    private static void InsertTask(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CreateTaskRequest request)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO tasks
                (task_id, role, counterpart_node, parent_task_id, kind, mode, state, state_seq,
                 task_hash, request_json, origin_node, target_node, created_at, updated_at,
                 expires_at, lease_expires_at, cancel_requested, attempts, workspace_path,
                 executor_ref, result_json, finished_at)
            VALUES
                ($id, $role, $counterpart, $parent, $kind, $mode, $state, 0,
                 $hash, $request, $origin, $target, $created, $created,
                 $expires, $lease, 0, 0, $workspace,
                 NULL, NULL, NULL);
            """;
        command.Parameters.AddWithValue("$id", request.TaskId);
        command.Parameters.AddWithValue("$role", request.Role.ToWireValue());
        command.Parameters.AddWithValue("$counterpart", (object?)request.CounterpartNode ?? DBNull.Value);
        command.Parameters.AddWithValue("$parent", (object?)request.ParentTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("$kind", request.Kind.ToWireValue());
        command.Parameters.AddWithValue("$mode", request.Mode.ToWireValue());
        command.Parameters.AddWithValue("$state", request.State.ToContractString());
        command.Parameters.AddWithValue("$hash", request.TaskHash);
        command.Parameters.AddWithValue("$request", request.RequestJson);
        command.Parameters.AddWithValue("$origin", request.OriginNode);
        command.Parameters.AddWithValue("$target", request.TargetNode);
        command.Parameters.AddWithValue("$created", PersistenceJson.Timestamp(request.CreatedAt));
        command.Parameters.AddWithValue("$expires", (object?)PersistenceJson.TimestampOrNull(request.ExpiresAt) ?? DBNull.Value);
        command.Parameters.AddWithValue("$lease", (object?)PersistenceJson.TimestampOrNull(request.LeaseExpiresAt) ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace", (object?)request.WorkspacePath ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void ValidateCreate(CreateTaskRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TaskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestJson);

        var validRole = request.Role == TaskRole.Origin
            ? request.State.IsOriginState()
            : request.State.IsTargetState();

        if (!validRole)
        {
            throw new ArgumentException($"El estado {request.State} no pertenece al rol {request.Role}.");
        }

        if (request.Role == TaskRole.Target && string.IsNullOrWhiteSpace(request.CounterpartNode))
        {
            throw new ArgumentException("Una tarea de destino requiere counterpart_node.");
        }
    }

    private static TaskKind ParseKind(string value) => value switch
    {
        "analysis" => TaskKind.Analysis,
        "test_run" => TaskKind.TestRun,
        "code_change" => TaskKind.CodeChange,
        _ => throw new InvalidDataException($"Kind desconocido: {value}."),
    };

    private static WorkspaceMode ParseMode(string value) => value switch
    {
        "read_only" => WorkspaceMode.ReadOnly,
        "read_write_worktree" => WorkspaceMode.ReadWriteWorktree,
        _ => throw new InvalidDataException($"Modo desconocido: {value}."),
    };
}
