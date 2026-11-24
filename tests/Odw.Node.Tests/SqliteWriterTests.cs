using Microsoft.Data.Sqlite;
using Odw.Node.Persistence;
using Xunit;

namespace Odw.Node.Tests;

public class SqliteWriterTests
{
    [Fact]
    public async Task Concurrent_writes_are_serialized_and_all_committed()
    {
        await using var fixture = TestDatabase.Create();

        await fixture.Database.Writer.WriteAsync((connection, transaction) =>
            Execute(connection, transaction, "CREATE TABLE counters (id INTEGER PRIMARY KEY AUTOINCREMENT);"));

        var writes = Enumerable.Range(0, 100)
            .Select(_ => fixture.Database.Writer.WriteAsync((connection, transaction) =>
            {
                Execute(connection, transaction, "INSERT INTO counters DEFAULT VALUES;");
                return 0;
            }).AsTask())
            .ToArray();

        await Task.WhenAll(writes);

        using var read = fixture.Database.OpenReadConnection();
        Assert.Equal(100L, MigrationTests.ScalarLong(read, "SELECT COUNT(*) FROM counters;"));
        Assert.Equal(0, fixture.Database.Writer.QueueDepth);
        Assert.Equal(0, fixture.Database.Writer.FailedCount);
        Assert.True(fixture.Database.Writer.CommittedCount >= 101);
    }

    [Fact]
    public async Task Failed_write_rolls_back_and_writer_stays_usable()
    {
        await using var fixture = TestDatabase.Create();

        await fixture.Database.Writer.WriteAsync((connection, transaction) =>
            Execute(connection, transaction, "CREATE TABLE items (id INTEGER PRIMARY KEY AUTOINCREMENT);"));

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await fixture.Database.Writer.WriteAsync((connection, transaction) =>
            {
                Execute(connection, transaction, "INSERT INTO items DEFAULT VALUES;");
                throw new InvalidOperationException("boom");
            }));

        using (var read = fixture.Database.OpenReadConnection())
        {
            Assert.Equal(0L, MigrationTests.ScalarLong(read, "SELECT COUNT(*) FROM items;"));
        }

        await fixture.Database.Writer.WriteAsync((connection, transaction) =>
            Execute(connection, transaction, "INSERT INTO items DEFAULT VALUES;"));

        using (var read = fixture.Database.OpenReadConnection())
        {
            Assert.Equal(1L, MigrationTests.ScalarLong(read, "SELECT COUNT(*) FROM items;"));
        }

        Assert.Equal(1, fixture.Database.Writer.FailedCount);
    }

    [Fact]
    public async Task Cancelled_write_is_not_executed()
    {
        await using var fixture = TestDatabase.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await fixture.Database.Writer.WriteAsync((connection, transaction) =>
            {
                Execute(connection, transaction, "CREATE TABLE never (id INTEGER);");
            }, cts.Token));

        using var read = fixture.Database.OpenReadConnection();
        Assert.Equal(0L, MigrationTests.ScalarLong(
            read,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'never';"));
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
