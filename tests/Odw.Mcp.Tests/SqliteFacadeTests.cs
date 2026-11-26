using Microsoft.Data.Sqlite;
using Odw.Core.Model;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Peers;
using Odw.Node.Persistence.Queries;
using Odw.Node.Persistence.Tasks;
using Xunit;

namespace Odw.Mcp.Tests;

internal sealed class Clock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; private set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FacadeFixture : IAsyncDisposable
{
    private FacadeFixture(string folder, SqliteDatabase database, Clock clock)
    {
        Folder = folder;
        Database = database;
        Clock = clock;
    }

    public string Folder { get; }

    public SqliteDatabase Database { get; }

    public Clock Clock { get; }

    public static FacadeFixture Create()
    {
        var folder = Path.Combine(Path.GetTempPath(), "odw-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var clock = new Clock(new DateTimeOffset(2026, 11, 24, 18, 0, 0, TimeSpan.Zero));
        var database = SqliteDatabase.Open(
            Path.Combine(folder, "odw.db"),
            new SqliteDatabaseOptions { Pooling = false, TimeProvider = clock });
        return new FacadeFixture(folder, database, clock);
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync();
        try
        {
            Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

public class SqliteFacadeTests
{
    [Fact]
    public async Task Peer_directory_derives_online_from_last_seen()
    {
        await using var fixture = FacadeFixture.Create();
        var status =
            """
            {"resources":{"active_tasks":2,"queued_tasks":1,"ram_available_gb":21.5,"cpu_usage_percent":18,"gpu_available":true},
             "load_score":0.42,"accepts":["analysis","test_run"],"capabilities":["git","dotnet"]}
            """;

        await fixture.Database.Writer.WriteAsync((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO peers
                    (node_id, enabled, address, key_id, token_hash, trust, added_at, last_seen_at, last_status)
                VALUES
                    ('PC-B', 1, 'pc-b.tailnet.ts.net:7421', 'k1', $hash, 'trusted', $now, $now, $status);
                """;
            command.Parameters.AddWithValue("$hash", new byte[] { 1, 2, 3 });
            command.Parameters.AddWithValue("$now", PersistenceJson.Timestamp(fixture.Clock.Now));
            command.Parameters.AddWithValue("$status", status);
            command.ExecuteNonQuery();
        });

        var directory = new SqlitePeerDirectory(fixture.Database, fixture.Clock);

        var online = Assert.Single(await directory.ListAsync(includeOffline: false, CancellationToken.None));
        Assert.Equal("online", online.Status);
        Assert.Equal(2, online.ActiveTasks);
        Assert.Equal(1, online.QueuedTasks);
        Assert.Equal(0.42, online.LoadScore);
        Assert.Contains("dotnet", online.Capabilities);
        Assert.True(online.GpuAvailable);

        fixture.Clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Empty(await directory.ListAsync(includeOffline: false, CancellationToken.None));
        var all = Assert.Single(await directory.ListAsync(includeOffline: true, CancellationToken.None));
        Assert.Equal("offline", all.Status);
    }

    [Fact]
    public async Task Task_query_reads_status_and_result()
    {
        await using var fixture = FacadeFixture.Create();
        var store = new TaskStore(fixture.Database, fixture.Clock);

        var created = await store.CreateAsync(new CreateTaskRequest
        {
            TaskId = McpTestData.TaskId,
            Role = TaskRole.Target,
            CounterpartNode = "PC-A",
            Kind = TaskKind.Analysis,
            Mode = WorkspaceMode.ReadOnly,
            State = TaskState.Received,
            TaskHash = "sha256:" + new string('a', 64),
            RequestJson = "{}",
            OriginNode = "PC-A",
            TargetNode = "PC-B",
            CreatedAt = fixture.Clock.Now,
            ExpiresAt = fixture.Clock.Now.AddHours(1),
        });

        var resultJson =
            """
            {"outcome":"succeeded","summary_md":"Todo verde.","changed_paths":["src/Foo.cs"],
             "branch":"odw/x","commit":"9f1c2ab","artifacts":[{"name":"report.md"}],
             "requires_review":false,"model_profile":"free","model_provider":"opencode",
             "model_id":"deepseek-v4.1-flash-free","cost_usd":0.0}
            """;

        var transition = await store.TransitionAsync(new TransitionTaskRequest
        {
            TaskId = created.TaskId,
            Role = TaskRole.Target,
            ExpectedState = TaskState.Received,
            ExpectedSeq = created.StateSeq,
            NextState = TaskState.Completed,
            EventType = "task.completed",
            Now = fixture.Clock.Now,
            FinishedAt = fixture.Clock.Now,
            ResultJson = resultJson,
        });

        Assert.Equal(TransitionStatus.Applied, transition.Status);

        var query = new SqliteTaskQuery(store);

        var status = await query.GetStatusAsync(created.TaskId, CancellationToken.None);
        Assert.NotNull(status);
        Assert.Equal(TaskState.Completed, status!.State);
        Assert.True(status.ResultAvailable);
        Assert.Equal("read_result", status.NextAction);

        var result = await query.GetResultAsync(created.TaskId, includeEvents: true, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(TaskOutcome.Succeeded, result!.Outcome);
        Assert.Equal("Todo verde.", result.SummaryMarkdown);
        Assert.Contains("src/Foo.cs", result.ChangedPaths);
        Assert.Contains("report.md", result.Artifacts);
        Assert.Equal("free", result.ModelProfile);
        Assert.Equal("opencode", result.ModelProvider);
        Assert.Equal("deepseek-v4.1-flash-free", result.ModelId);
        Assert.Equal(0m, result.CostUsd);
        Assert.Equal(2, result.Events.Count);
    }

    [Fact]
    public async Task Unknown_task_returns_null()
    {
        await using var fixture = FacadeFixture.Create();
        var query = new SqliteTaskQuery(new TaskStore(fixture.Database, fixture.Clock));

        Assert.Null(await query.GetStatusAsync(McpTestData.TaskId, CancellationToken.None));
        Assert.Null(await query.GetResultAsync(McpTestData.TaskId, false, CancellationToken.None));
    }
}
