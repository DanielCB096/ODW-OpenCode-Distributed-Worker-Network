using Microsoft.Data.Sqlite;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Tasks;
using Xunit;

namespace Odw.Node.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Open_applies_schema_and_pragmas()
    {
        await using var fixture = TestDatabase.Create();

        Assert.Equal(DatabaseMigrator.CurrentVersion, fixture.Database.SchemaVersion);

        using var connection = fixture.Database.OpenReadConnection();
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode;"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys;"));

        var tables = new HashSet<string>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        foreach (var expected in new[]
                 {
                     "schema_version", "peers", "repos", "tasks", "task_events", "messages", "approval_requests",
                     "approvals", "pending_actions", "audit_log", "artifacts",
                 })
        {
            Assert.Contains(expected, tables);
        }

        Assert.Equal(3L, Scalar(connection, "SELECT COUNT(*) FROM schema_version;"));
    }

    [Fact]
    public async Task Reopening_does_not_reapply_migrations()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odw-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, "odw.db");
        var options = new SqliteDatabaseOptions { Pooling = false, TimeProvider = new TestTime(TestTime.DefaultNow) };

        try
        {
            await using (var first = SqliteDatabase.Open(path, options))
            {
                Assert.Equal(DatabaseMigrator.CurrentVersion, first.SchemaVersion);
            }

            await using (var second = SqliteDatabase.Open(path, options))
            {
                Assert.Equal(DatabaseMigrator.CurrentVersion, second.SchemaVersion);
                using var connection = second.OpenReadConnection();
                Assert.Equal(3L, Scalar(connection, "SELECT COUNT(*) FROM schema_version;"));
            }
        }
        finally
        {
            System.IO.Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Failed_migration_rolls_back_completely()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "odw-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(folder);
        var path = System.IO.Path.Combine(folder, "odw.db");
        var time = new TestTime(TestTime.DefaultNow);

        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            connection.Open();

            var embedded = DatabaseMigrator.LoadEmbedded(typeof(SqliteDatabase).Assembly);
            var outcome = DatabaseMigrator.Migrate(connection, embedded, time);
            Assert.Equal(3, outcome.ToVersion);

            var broken = new[]
            {
                new Migration(4, "broken", "CREATE TABLE broken (x INTEGER); INSERT INTO missing VALUES (1);"),
            };

            Assert.Throws<SqliteException>(() => DatabaseMigrator.Migrate(connection, broken, time));
            Assert.Equal(3, DatabaseMigrator.GetUserVersion(connection));
            Assert.Equal(0L, Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'broken';"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            System.IO.Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Audit_and_events_are_append_only()
    {
        await using var fixture = TestDatabase.Create();
        var tasks = new TaskStore(fixture.Database, fixture.Time);
        await tasks.CreateAsync(TestRows.Origin());

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = fixture.Path, Pooling = false }.ToString());
        connection.Open();

        Assert.Throws<SqliteException>(() => Execute(connection, "UPDATE audit_log SET actor = 'attacker';"));
        Assert.Throws<SqliteException>(() => Execute(connection, "DELETE FROM audit_log;"));
        Assert.Throws<SqliteException>(() => Execute(connection, "UPDATE task_events SET type = 'x';"));
        Assert.Throws<SqliteException>(() => Execute(connection, "DELETE FROM task_events;"));
    }

    internal static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    internal static long ScalarLong(SqliteConnection connection, string sql)
        => Convert.ToInt64(Scalar(connection, sql), System.Globalization.CultureInfo.InvariantCulture);

    internal static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
