using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Outbox;
using Odw.Node.Persistence.Tasks;

namespace Odw.Node.Tests;

internal sealed class TestTime(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow =
        new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; private set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string folder, SqliteDatabase database, TestTime time)
    {
        Folder = folder;
        Database = database;
        Time = time;
    }

    public string Folder { get; }

    public SqliteDatabase Database { get; }

    public TestTime Time { get; }

    public string Path => System.IO.Path.Combine(Folder, "odw.db");

    public static TestDatabase Create()
    {
        var folder = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "odw-tests",
            Guid.NewGuid().ToString("N"));

        System.IO.Directory.CreateDirectory(folder);

        var time = new TestTime(TestTime.DefaultNow);
        var database = SqliteDatabase.Open(
            System.IO.Path.Combine(folder, "odw.db"),
            new SqliteDatabaseOptions
            {
                Pooling = false,
                TimeProvider = time,
            });

        return new TestDatabase(folder, database, time);
    }

    public async ValueTask DisposeAsync()
    {
        await Database.DisposeAsync().ConfigureAwait(false);

        try
        {
            System.IO.Directory.Delete(Folder, recursive: true);
        }
        catch (IOException)
        {
            // Windows puede retener el archivo un instante; el directorio temporal se limpia aparte.
        }
    }
}

internal static class TestRows
{
    public static string NewId() => Ulid.New().ToString();    public static CreateTaskRequest Origin(
        TaskState state = TaskState.Proposed,
        DateTimeOffset? leaseExpiresAt = null,
        NewPendingAction? nextAction = null,
        string? taskId = null)
        => new()
        {
            TaskId = taskId ?? NewId(),
            Role = TaskRole.Origin,
            Kind = TaskKind.Analysis,
            Mode = WorkspaceMode.ReadOnly,
            State = state,
            TaskHash = "sha256:" + new string('a', 64),
            RequestJson = "{\"task_id\":\"test\"}",
            OriginNode = "PC-A",
            TargetNode = "PC-B",
            CreatedAt = TestTime.DefaultNow,
            ExpiresAt = TestTime.DefaultNow.AddHours(1),
            LeaseExpiresAt = leaseExpiresAt,
            NextAction = nextAction,
        };

    public static CreateTaskRequest Target(
        TaskState state = TaskState.Received,
        DateTimeOffset? leaseExpiresAt = null,
        NewPendingAction? nextAction = null,
        string? taskId = null)
        => new()
        {
            TaskId = taskId ?? NewId(),
            Role = TaskRole.Target,
            CounterpartNode = "PC-A",
            Kind = TaskKind.Analysis,
            Mode = WorkspaceMode.ReadOnly,
            State = state,
            TaskHash = "sha256:" + new string('a', 64),
            RequestJson = "{\"task_id\":\"test\"}",
            OriginNode = "PC-A",
            TargetNode = "PC-B",
            CreatedAt = TestTime.DefaultNow,
            ExpiresAt = TestTime.DefaultNow.AddHours(1),
            LeaseExpiresAt = leaseExpiresAt,
            NextAction = nextAction,
        };

    public static TransitionTaskRequest Transition(
        TaskSnapshot current,
        TaskState next,
        DateTimeOffset now,
        NewPendingAction? nextAction = null)
        => new()
        {
            TaskId = current.TaskId,
            Role = current.Role,
            ExpectedState = current.State,
            ExpectedSeq = current.StateSeq,
            NextState = next,
            EventType = "test.transition",
            EventPayloadJson = "{\"why\":\"test\"}",
            Now = now,
            NextAction = nextAction,
        };
}
