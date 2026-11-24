using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Node.Persistence;

namespace Odw.Node.Approvals;

public static class ApprovalStatus
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Denied = "denied";
    public const string Expired = "expired";
}

public sealed record ApprovalRecord(
    string ApprovalId,
    string TaskId,
    TaskRole Role,
    string TaskHash,
    string Summary,
    string Nonce,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? DecidedAt,
    string? DecidedBy,
    string? Reason,
    DateTimeOffset? UsedAt);

public enum ApprovalDecisionStatus
{
    Approved,
    Denied,
    NotFound,
    Expired,
    AlreadyDecided,
    HashMismatch,
}

public sealed record ApprovalDecisionResult(ApprovalDecisionStatus Status, ApprovalRecord? Record);

/// <summary>
/// Solicitudes de aprobación humana vinculadas a <c>(task_id, task_hash, nonce)</c>, con
/// expiración y un solo uso. La decisión se registra en auditoría dentro de la transacción.
/// </summary>
public sealed class ApprovalService
{
    private readonly SqliteDatabase _database;
    private readonly TimeProvider _timeProvider;

    public ApprovalService(SqliteDatabase database, TimeProvider timeProvider)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ApprovalRecord> CreateAsync(
        string taskId,
        TaskRole role,
        string taskHash,
        string summary,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl));
        }

        var now = _timeProvider.GetUtcNow();
        var record = new ApprovalRecord(
            Ulid.New(_timeProvider).ToString(),
            taskId,
            role,
            taskHash,
            summary,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
            ApprovalStatus.Pending,
            now,
            now.Add(ttl),
            null,
            null,
            null,
            null);

        await _database.Writer.WriteAsync((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO approval_requests
                    (approval_id, task_id, role, task_hash, summary, nonce, status,
                     created_at, expires_at)
                VALUES
                    ($id, $task, $role, $hash, $summary, $nonce, 'pending',
                     $created, $expires);
                """;
            command.Parameters.AddWithValue("$id", record.ApprovalId);
            command.Parameters.AddWithValue("$task", record.TaskId);
            command.Parameters.AddWithValue("$role", record.Role.ToWireValue());
            command.Parameters.AddWithValue("$hash", record.TaskHash);
            command.Parameters.AddWithValue("$summary", record.Summary);
            command.Parameters.AddWithValue("$nonce", record.Nonce);
            command.Parameters.AddWithValue("$created", PersistenceJson.Timestamp(record.CreatedAt));
            command.Parameters.AddWithValue("$expires", PersistenceJson.Timestamp(record.ExpiresAt));
            command.ExecuteNonQuery();

            AuditLog.Append(
                connection,
                transaction,
                "approval.requested",
                $"{taskId}/{role.ToWireValue()}",
                "service",
                PersistenceJson.Serialize(new { approval_id = record.ApprovalId, expires_at = record.ExpiresAt }),
                now);
        }, cancellationToken).ConfigureAwait(false);

        return record;
    }

    public ApprovalRecord? Get(string approvalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);

        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM approval_requests WHERE approval_id = $id;";
        command.Parameters.AddWithValue("$id", approvalId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    public IReadOnlyList<ApprovalRecord> ListPending()
    {
        using var connection = _database.OpenReadConnection();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT {Columns} FROM approval_requests
              WHERE status = 'pending' AND expires_at > $now
              ORDER BY created_at;
             """;
        command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(_timeProvider.GetUtcNow()));

        var records = new List<ApprovalRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(Map(reader));
        }

        return records;
    }

    public async Task<ApprovalDecisionResult> DecideAsync(
        string approvalId,
        bool approved,
        string decidedBy,
        string? reason,
        string? expectedTaskHash = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(decidedBy);

        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync((connection, transaction) =>
        {
            var record = Read(connection, transaction, approvalId);
            if (record is null)
            {
                return new ApprovalDecisionResult(ApprovalDecisionStatus.NotFound, null);
            }

            if (record.Status != ApprovalStatus.Pending)
            {
                return new ApprovalDecisionResult(ApprovalDecisionStatus.AlreadyDecided, record);
            }

            if (record.ExpiresAt <= now)
            {
                UpdateStatus(connection, transaction, record.ApprovalId, ApprovalStatus.Expired, now, null, null);
                return new ApprovalDecisionResult(
                    ApprovalDecisionStatus.Expired,
                    record with { Status = ApprovalStatus.Expired });
            }

            if (expectedTaskHash is not null
                && !string.Equals(expectedTaskHash, record.TaskHash, StringComparison.Ordinal))
            {
                return new ApprovalDecisionResult(ApprovalDecisionStatus.HashMismatch, record);
            }

            var status = approved ? ApprovalStatus.Approved : ApprovalStatus.Denied;
            UpdateStatus(connection, transaction, record.ApprovalId, status, now, decidedBy, reason);

            AuditLog.Append(
                connection,
                transaction,
                approved ? "approval.granted" : "approval.denied",
                $"{record.TaskId}/{record.Role.ToWireValue()}",
                decidedBy,
                PersistenceJson.Serialize(new
                {
                    approval_id = record.ApprovalId,
                    task_hash = record.TaskHash,
                    reason,
                }),
                now);

            var updated = record with
            {
                Status = status,
                DecidedAt = now,
                DecidedBy = decidedBy,
                Reason = reason,
            };

            return new ApprovalDecisionResult(
                approved ? ApprovalDecisionStatus.Approved : ApprovalDecisionStatus.Denied,
                updated);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ConsumeAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE approval_requests
                   SET used_at = $now
                 WHERE approval_id = $id AND status = 'approved' AND used_at IS NULL;
                """;
            command.Parameters.AddWithValue("$id", approvalId);
            command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
            return command.ExecuteNonQuery() > 0;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> ExpireAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        return await _database.Writer.WriteAsync((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                UPDATE approval_requests
                   SET status = 'expired'
                 WHERE status = 'pending' AND expires_at <= $now;
                """;
            command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(now));
            return command.ExecuteNonQuery();
        }, cancellationToken).ConfigureAwait(false);
    }

    private const string Columns =
        "approval_id, task_id, role, task_hash, summary, nonce, status, created_at, expires_at, decided_at, decided_by, reason, used_at";

    private static ApprovalRecord? Read(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string approvalId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {Columns} FROM approval_requests WHERE approval_id = $id;";
        command.Parameters.AddWithValue("$id", approvalId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    private static void UpdateStatus(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string approvalId,
        string status,
        DateTimeOffset at,
        string? decidedBy,
        string? reason)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            UPDATE approval_requests
               SET status = $status, decided_at = $decided, decided_by = $by, reason = $reason
             WHERE approval_id = $id;
            """;
        command.Parameters.AddWithValue("$id", approvalId);
        command.Parameters.AddWithValue("$status", status);
        command.Parameters.AddWithValue("$decided", PersistenceJson.Timestamp(at));
        command.Parameters.AddWithValue("$by", (object?)decidedBy ?? DBNull.Value);
        command.Parameters.AddWithValue("$reason", (object?)reason ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static ApprovalRecord Map(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        TaskStateExtensions.TryParseRole(reader.GetString(2), out var role) ? role : TaskRole.Origin,
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        PersistenceJson.ParseTimestamp(reader.GetString(7)),
        PersistenceJson.ParseTimestamp(reader.GetString(8)),
        reader.IsDBNull(9) ? null : PersistenceJson.ParseTimestamp(reader.GetString(9)),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.IsDBNull(11) ? null : reader.GetString(11),
        reader.IsDBNull(12) ? null : PersistenceJson.ParseTimestamp(reader.GetString(12)));
}
