using Odw.Core.Contracts;
using Odw.Core.Model;
using Xunit;

namespace Odw.Executors.OpenCode.Tests;

public class OpenCodeExecutorTests
{
    private static OpenCodeExecutor Create(
        FakeOpenCodeClient client,
        ToolPermissionHandler? handler = null)
        => new(client, handler, TimeProvider.System);

    [Fact]
    public async Task Start_creates_session_with_policy_and_model()
    {
        var client = new FakeOpenCodeClient();
        var executor = Create(client);

        var session = await executor.StartAsync(
            TestRequests.Start(ToolPolicy.AskAll, "anthropic/claude"), CancellationToken.None);

        Assert.Equal("ses_test", session.SessionId);
        Assert.Equal(4242, session.ProcessId);
        Assert.Equal("http://127.0.0.1:9999", session.BaseUrl);

        var request = client.LastSessionRequest;
        Assert.NotNull(request);
        Assert.Equal("ODW 01JAZ5Z8Q4XK3V9C2M1N7P6R8T", request!.Title);
        Assert.Equal("anthropic", request.ModelProvider);
        Assert.Equal("claude", request.ModelId);
        Assert.NotNull(request.Permissions);
        var rule = Assert.Single(request.Permissions!);
        Assert.Equal(OpenCodePermissionEffect.Ask, rule.Effect);
    }

    [Fact]
    public async Task Start_ignores_invalid_model_hint()
    {
        var client = new FakeOpenCodeClient();
        var executor = Create(client);

        await executor.StartAsync(TestRequests.Start(modelHint: "sin-separador"), CancellationToken.None);

        Assert.Null(client.LastSessionRequest!.ModelProvider);
        Assert.Null(client.LastSessionRequest.ModelId);
    }

    [Fact]
    public async Task Send_maps_delivery()
    {
        var client = new FakeOpenCodeClient();
        var executor = Create(client);

        await executor.SendAsync("ses_test", "hola", MessageDelivery.Steer, CancellationToken.None);
        await executor.SendAsync("ses_test", "más", MessageDelivery.Queue, CancellationToken.None);

        Assert.Equal("steer", client.Prompts[0].Delivery);
        Assert.Equal("queue", client.Prompts[1].Delivery);
    }

    [Fact]
    public async Task Stream_maps_events_and_stops_at_terminal()
    {
        var client = new FakeOpenCodeClient();
        client.Events.Add(EventFactory.Create("session.execution.started", "ses_test", "{}"));
        client.Events.Add(EventFactory.Create("session.tool.called", "ses_test",
            """{"id":"call_1","name":"shell"}"""));
        client.Events.Add(EventFactory.Create("session.tool.failed", "ses_test",
            """{"id":"call_2","error":{"message":"denegado"}}"""));
        client.Events.Add(EventFactory.Create("session.text.delta", "ses_test", """{"delta":"hola"}"""));
        client.Events.Add(EventFactory.Create("session.execution.succeeded", "ses_test", "{}"));
        client.Events.Add(EventFactory.Create("session.text.delta", "ses_test", """{"delta":"tarde"}"""));

        var executor = Create(client);
        var received = new List<ExecutorEvent>();

        await foreach (var item in executor.StreamAsync("ses_test", CancellationToken.None))
        {
            received.Add(item);
        }

        Assert.Equal(
            [
                ExecutorEventKind.Started,
                ExecutorEventKind.ToolCalled,
                ExecutorEventKind.ToolFailed,
                ExecutorEventKind.TextDelta,
                ExecutorEventKind.Completed,
            ],
            received.Select(item => item.Kind));

        Assert.Equal("shell", received[1].Tool);
        Assert.Equal("denegado", received[2].Detail);
        Assert.Equal("hola", received[3].Detail);
    }

    [Fact]
    public async Task Permission_is_brokered_once_and_never_always()
    {
        var client = new FakeOpenCodeClient();
        client.Events.Add(EventFactory.Create("permission.asked", "ses_test",
            """{"id":"per_1","sessionID":"ses_test","action":"shell","resources":["echo x"],"save":["echo *"]}"""));
        client.Events.Add(EventFactory.Create("session.execution.succeeded", "ses_test", "{}"));

        var calls = 0;
        var executor = Create(client, (permission, _) =>
        {
            calls++;
            Assert.Equal("ses_test", permission.SessionId);
            return Task.FromResult(ToolPermissionDecision.AllowOnce);
        });

        var received = new List<ExecutorEvent>();
        await foreach (var item in executor.StreamAsync("ses_test", CancellationToken.None))
        {
            received.Add(item);
        }

        Assert.Equal(1, calls);
        var reply = Assert.Single(client.Replies);
        Assert.Equal("per_1", reply.PermissionId);
        Assert.Equal(OpenCodePermissionReply.Once, reply.Reply);
        Assert.DoesNotContain(client.Replies, item => item.Reply == OpenCodePermissionReply.Always);
        Assert.Equal(ExecutorEventKind.PermissionAsked, received[0].Kind);
    }

    [Fact]
    public async Task Permission_is_rejected_without_handler()
    {
        var client = new FakeOpenCodeClient();
        client.Events.Add(EventFactory.Create("permission.asked", "ses_test",
            """{"id":"per_1","sessionID":"ses_test","action":"shell","resources":["rm -rf /"]}"""));
        client.Events.Add(EventFactory.Create("session.execution.succeeded", "ses_test", "{}"));

        var executor = Create(client);
        await foreach (var _ in executor.StreamAsync("ses_test", CancellationToken.None))
        {
        }

        var reply = Assert.Single(client.Replies);
        Assert.Equal(OpenCodePermissionReply.Reject, reply.Reply);
    }

    [Fact]
    public async Task Permission_handler_exception_rejects()
    {
        var client = new FakeOpenCodeClient();
        client.Events.Add(EventFactory.Create("permission.asked", "ses_test",
            """{"id":"per_1","sessionID":"ses_test","action":"shell","resources":["x"]}"""));
        client.Events.Add(EventFactory.Create("session.execution.succeeded", "ses_test", "{}"));

        var executor = Create(client, (_, _) => throw new InvalidOperationException("boom"));

        await foreach (var _ in executor.StreamAsync("ses_test", CancellationToken.None))
        {
        }

        Assert.Equal(OpenCodePermissionReply.Reject, Assert.Single(client.Replies).Reply);
    }

    [Fact]
    public async Task Wait_returns_success_on_terminal_event()
    {
        var client = new FakeOpenCodeClient();
        client.Events.Add(EventFactory.Create("session.execution.started", "ses_test", "{}"));
        client.Events.Add(EventFactory.Create("session.execution.succeeded", "ses_test", "{}"));

        var outcome = await Create(client).WaitAsync(
            "ses_test", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(ExecutorOutcomeKind.Succeeded, outcome.Kind);
    }

    [Fact]
    public async Task Wait_times_out()
    {
        var client = new FakeOpenCodeClient { BlockAfterEvents = true };
        client.Events.Add(EventFactory.Create("session.execution.started", "ses_test", "{}"));

        var outcome = await Create(client).WaitAsync(
            "ses_test", TimeSpan.FromMilliseconds(80), CancellationToken.None);

        Assert.Equal(ExecutorOutcomeKind.Failed, outcome.Kind);
        Assert.Equal("TIMEOUT", outcome.Code);
    }

    [Fact]
    public async Task Wait_reports_stream_end_without_terminal()
    {
        var client = new FakeOpenCodeClient();
        client.Events.Add(EventFactory.Create("session.execution.started", "ses_test", "{}"));

        var outcome = await Create(client).WaitAsync(
            "ses_test", TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal("STREAM_ENDED", outcome.Code);
    }

    [Fact]
    public async Task Snapshot_reflects_busy_state()
    {
        var client = new FakeOpenCodeClient
        {
            Session = new OpenCodeSession(
                "ses_test",
                "ODW",
                "C:\\ws",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddMinutes(1),
                null),
        };

        var snapshot = await Create(client).GetSnapshotAsync("ses_test", CancellationToken.None);

        Assert.Equal("running", snapshot.State);
        Assert.False(snapshot.IsIdle);

        client.SessionMissing = true;
        var missing = await Create(client).GetSnapshotAsync("ses_test", CancellationToken.None);
        Assert.Equal("missing", missing.State);
    }

    [Fact]
    public async Task Abort_interrupts_the_session()
    {
        var client = new FakeOpenCodeClient();

        await Create(client).AbortAsync("ses_test", TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(["ses_test"], client.Interrupts);
    }
}
