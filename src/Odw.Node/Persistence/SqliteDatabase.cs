using Microsoft.Data.Sqlite;

namespace Odw.Node.Persistence;

public sealed record SqliteDatabaseOptions
{
    public bool Pooling { get; init; } = true;

    public string? MigrationBackupDirectory { get; init; }

    public string AppVersion { get; init; } = "0.1.0";

    public int WriteQueueCapacity { get; init; } = 1024;

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

/// <summary>
/// Base SQLite de un nodo: PRAGMAs, migraciones y un único escritor.
/// Las lecturas usan conexiones de solo lectura (WAL no bloquea al escritor).
/// </summary>
public sealed class SqliteDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _writeConnection;
    private readonly SqliteWriter _writer;
    private readonly bool _pooling;
    private bool _disposed;

    private SqliteDatabase(
        string path,
        SqliteConnection writeConnection,
        SqliteWriter writer,
        bool pooling)
    {
        Path = path;
        _writeConnection = writeConnection;
        _writer = writer;
        _pooling = pooling;
    }

    public string Path { get; }

    public SqliteWriter Writer => _writer;

    public int SchemaVersion => DatabaseMigrator.GetUserVersion(_writeConnection);

    public static SqliteDatabase Open(string path, SqliteDatabaseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        options ??= new SqliteDatabaseOptions();

        var fullPath = System.IO.Path.GetFullPath(path);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = options.Pooling,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        ApplyWritePragmas(connection);

        var migrations = DatabaseMigrator.LoadEmbedded(typeof(SqliteDatabase).Assembly);
        DatabaseMigrator.Migrate(
            connection,
            migrations,
            options.TimeProvider,
            options.MigrationBackupDirectory,
            options.AppVersion);

        var writer = new SqliteWriter(connection, options.WriteQueueCapacity);
        return new SqliteDatabase(fullPath, connection, writer, options.Pooling);
    }

    public SqliteConnection OpenReadConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = _pooling,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA query_only = ON; PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private static void ApplyWritePragmas(SqliteConnection connection)
    {
        Execute(connection, "PRAGMA journal_mode = WAL;");
        Execute(connection, "PRAGMA synchronous = NORMAL;");
        Execute(connection, "PRAGMA foreign_keys = ON;");
        Execute(connection, "PRAGMA busy_timeout = 5000;");
        Execute(connection, "PRAGMA wal_autocheckpoint = 1000;");
        Execute(connection, "PRAGMA temp_store = MEMORY;");
        Execute(connection, "PRAGMA trusted_schema = OFF;");
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _writer.DisposeAsync().ConfigureAwait(false);

        try
        {
            Execute(_writeConnection, "PRAGMA wal_checkpoint(TRUNCATE);");
        }
        catch (SqliteException)
        {
            // Un checkpoint en apagado es best-effort; el WAL se recupera al reabrir.
        }

        _writeConnection.Dispose();
    }
}
