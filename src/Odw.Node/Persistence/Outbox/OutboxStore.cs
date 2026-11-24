using System.Globalization;
using Microsoft.Data.Sqlite;
using Odw.Core.Model;

namespace Odw.Node.Persistence.Outbox;

public enum OutboxFailOutcome
{
    RetryScheduled,
    DeadLettered,
    NotFound,
}

/// <summary>
/// Outbox durable: se persiste antes de intentar el efecto remoto, se reclama de forma
/// atómica y se reintenta con backoff. Un fallo definitivo pasa a <c>dead</c> (no se pierde).
/// </summary>
public sealed class OutboxStore
{
    private readonly SqliteDatabase _database;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxAttempts;

    public OutboxStore(SqliteDatabase database, TimeProvider timeProvider, int maxAttempts = 5)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        _maxAttempts = maxAttempts;
    }

    public int MaxAttempts => _maxAttempts;

    public async Task<long> EnqueueAsync(NewPendingAction action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync(
            (connection, transaction) => OutboxSql.Insert(connection, transaction, action, now),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PendingAction>> ClaimDueAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "batchSize debe estar en 1..100.");
        }

        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync(
            (connection, transaction) => Claim(connection, transaction, batchSize, now),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CompleteAsync(long id, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE pending_actions
                   SET status = 'done', updated_at = $now
                 WHERE id = $id AND status = 'running';
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
            return command.ExecuteNonQuery() > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OutboxFailOutcome> FailAsync(
        long id,
        string error,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync(
            (connection, transaction) => Fail(connection, transaction, id, error, now),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Al arrancar, una acción que quedó <c>running</c> se devuelve a la cola.</summary>
    public async Task<int> ResetStuckAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync((connection, transaction) =>
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
        }, cancellationToken).ConfigureAwait(false);
    }

    public PendingAction? Get(long id)
    {
        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {OutboxSql.Columns} FROM pending_actions WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? OutboxSql.Read(reader) : null;
    }

    public int Count(string status)
    {
        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pending_actions WHERE status = $status;";
        command.Parameters.AddWithValue("$status", status);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static TimeSpan BackoffFor(int attempts)
        => TimeSpan.FromSeconds(Math.Pow(2, Math.Clamp(attempts - 1, 0, 4)));

    private static IReadOnlyList<PendingAction> Claim(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int batchSize,
        DateTimeOffset now)
    {
        var nowText = PersistenceJson.Timestamp(now);
        var ids = new List<long>(batchSize);

        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                """
                SELECT id FROM pending_actions
                 WHERE status = 'pending' AND next_attempt_at <= $now
                 ORDER BY next_attempt_at, id
                 LIMIT $limit;
                """;
            select.Parameters.AddWithValue("$now", nowText);
            select.Parameters.AddWithValue("$limit", batchSize);

            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt64(0));
            }
        }

        if (ids.Count == 0)
        {
            return [];
        }

        var claimed = new List<PendingAction>(ids.Count);
        foreach (var id in ids)
        {
            using (var update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText =
                    """
                    UPDATE pending_actions
                       SET status = 'running', attempts = attempts + 1, updated_at = $now
                     WHERE id = $id AND status = 'pending';
                    """;
                update.Parameters.AddWithValue("$id", id);
                update.Parameters.AddWithValue("$now", nowText);
                if (update.ExecuteNonQuery() == 0)
                {
                    continue;
                }
            }

            using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = $"SELECT {OutboxSql.Columns} FROM pending_actions WHERE id = $id;";
            read.Parameters.AddWithValue("$id", id);

            using var reader = read.ExecuteReader();
            if (reader.Read())
            {
                claimed.Add(OutboxSql.Read(reader));
            }
        }

        return claimed;
    }

    private OutboxFailOutcome Fail(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long id,
        string error,
        DateTimeOffset now)
    {
        int attempts;

        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT attempts, status FROM pending_actions WHERE id = $id;";
            select.Parameters.AddWithValue("$id", id);

            using var reader = select.ExecuteReader();
            if (!reader.Read() || reader.GetString(1) != OutboxStatus.Running)
            {
                return OutboxFailOutcome.NotFound;
            }

            attempts = reader.GetInt32(0);
        }

        var dead = attempts >= _maxAttempts;
        var nextAttempt = dead ? now : now + BackoffFor(attempts);

        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE pending_actions
               SET status = $status,
                   last_error = $error,
                   next_attempt_at = $next,
                   updated_at = $now
             WHERE id = $id;
            """;
        update.Parameters.AddWithValue("$id", id);
        update.Parameters.AddWithValue("$status", dead ? OutboxStatus.Dead : OutboxStatus.Pending);
        update.Parameters.AddWithValue("$error", error);
        update.Parameters.AddWithValue("$next", PersistenceJson.Timestamp(nextAttempt));
        update.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
        update.ExecuteNonQuery();

        return dead ? OutboxFailOutcome.DeadLettered : OutboxFailOutcome.RetryScheduled;
    }
}
