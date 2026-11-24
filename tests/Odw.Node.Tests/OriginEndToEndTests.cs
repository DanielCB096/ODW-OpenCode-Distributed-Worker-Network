using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Mcp.Abstractions;
using Odw.Node.Hosting;
using Odw.Node.Peers;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Scheduling;
using Odw.Policy;
using Xunit;

namespace Odw.Node.Tests;

public class OriginEndToEndTests
{
    private const string PairToken = "pair-token-0123456789abcdef";
    private static readonly string McpToken = Convert.ToBase64String(new byte[32]);

    private static PolicyEngine InboundAuto() => PolicyEngine.FromJson(
        """
        {"version":1,"default":"ASK","rules":[
          {"id":"auto-in","priority":10,"effect":"AUTO","reason":"prueba",
           "when":{"direction":"in","kind":["analysis","test_run"],"mode":"read_only","peer_trust":"trusted"}}]}
        """);

    private static PolicyEngine OutboundAuto() => PolicyEngine.FromJson(
        """
        {"version":1,"default":"ASK","rules":[
          {"id":"auto-out","priority":10,"effect":"AUTO","reason":"prueba","when":{"direction":"out"}}]}
        """);

    private static PolicyEngine OutboundAsk() => PolicyEngine.FromJson(
        """{"version":1,"default":"ASK","rules":[]}""");

    private static NodeHostOptions Target(string folder) => new()
    {
        NodeId = "PC-B",
        DatabasePath = Path.Combine(folder, "odw.db"),
        McpToken = McpToken,
        PeerBind = "127.0.0.1",
        PeerPort = 0,
        McpBind = "127.0.0.1",
        McpPort = 0,
        AllowLoopbackPeerBind = true,
        Peers = [new PeerRegistration("PC-A", PairToken)],
        Capabilities = ["git", "dotnet"],
        Policy = InboundAuto(),
        EnableScheduler = true,
        WorkspacePreparer = new FakeWorkspacePreparer(),
        ExecutionRunner = new FakeExecutionRunner(),
        SchedulerOptions = new ExecutionSchedulerOptions
        {
            MaxConcurrentTasks = 1,
            PollInterval = TimeSpan.FromMilliseconds(50),
        },
        EnableOriginPoller = false,
        EnableApprovals = false,
        Pooling = false,
    };

    private static NodeHostOptions Origin(
        string folder,
        Uri peer,
        PolicyEngine policy,
        bool enableApprovals = false,
        string? pipeName = null) => new()
    {
        NodeId = "PC-A",
        DatabasePath = Path.Combine(folder, "odw.db"),
        McpToken = McpToken,
        PeerBind = "127.0.0.1",
        PeerPort = 0,
        McpBind = "127.0.0.1",
        McpPort = 0,
        AllowLoopbackPeerBind = true,
        Peers =
        [
            new PeerRegistration("PC-B", PairToken, Address: peer.Authority),
        ],
        Capabilities = ["git"],
        Policy = policy,
        EnableScheduler = false,
        EnableOriginPoller = false,
        EnableApprovals = enableApprovals,
        ApprovalPipeName = pipeName ?? "odw-approvals-" + Guid.NewGuid().ToString("N"),
        ApprovalPipeSecurityDescriptor = null,
        Pooling = false,
    };

    private static ProposalInput Input(string? taskId = null) => new(
        "PC-B",
        TaskKind.Analysis,
        WorkspaceMode.ReadOnly,
        "SistemaMaestro",
        "main",
        "Analizar el repositorio y ejecutar las pruebas.",
        ["src/", "tests/"],
        [],
        45,
        12,
        64,
        NetworkAccess.None,
        5m,
        60,
        taskId);

    private static string Folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "odw-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail(message);
    }

    private static async Task<(NodeHost Origin, NodeHost Target, string FolderA, string FolderB)> StartAsync(
        PolicyEngine? originPolicy = null,
        bool enableApprovals = false,
        string? pipeName = null)
    {
        var folderB = Folder();
        var folderA = Folder();

        var target = await NodeHost.StartAsync(Target(folderB));
        var origin = await NodeHost.StartAsync(Origin(
            folderA,
            target.PeerBaseAddress,
            originPolicy ?? OutboundAuto(),
            enableApprovals,
            pipeName));

        return (origin, target, folderA, folderB);
    }

    private static async Task DisposeAsync(NodeHost origin, NodeHost target, string folderA, string folderB)
    {
        await origin.DisposeAsync();
        await target.DisposeAsync();

        foreach (var folder in new[] { folderA, folderB })
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task Origin_delegates_executes_and_retrieves_the_result()
    {
        var (origin, target, folderA, folderB) = await StartAsync();

        try
        {
            var outcome = await origin.ProposalService.ProposeAsync(Input(), CancellationToken.None);

            Assert.False(outcome.IsError);
            Assert.Equal("ACCEPTED", outcome.State);

            await WaitUntilAsync(
                () => target.Tasks.Get(outcome.TaskId, TaskRole.Target)?.State == TaskState.Completed,
                "El destino no completó la tarea.");

            var refreshed = await origin.Poller!.RefreshOnceAsync();
            Assert.Equal(1, refreshed);

            var current = origin.Tasks.Get(outcome.TaskId, TaskRole.Origin);
            Assert.NotNull(current);
            Assert.Equal(TaskState.Done, current.State);
            Assert.Contains("succeeded", current.ResultJson);

            Assert.True(origin.Tasks.VerifyEvents(outcome.TaskId, TaskRole.Origin).IsValid);
            Assert.True(target.Tasks.VerifyEvents(outcome.TaskId, TaskRole.Target).IsValid);
            Assert.True(origin.Tasks.VerifyAudit().IsValid);
            Assert.True(target.Tasks.VerifyAudit().IsValid);
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }

    [Fact]
    public async Task Ask_policy_does_not_send_anything()
    {
        var (origin, target, folderA, folderB) = await StartAsync(OutboundAsk());

        try
        {
            var outcome = await origin.ProposalService.ProposeAsync(Input(), CancellationToken.None);

            Assert.False(outcome.IsError);
            Assert.Equal("WAITING_APPROVAL", outcome.State);
            Assert.True(outcome.ApprovalRequired);
            Assert.Equal("wait_for_human", outcome.NextAction);
            Assert.Null(target.Tasks.Get(outcome.TaskId, TaskRole.Target));
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }

    [Fact]
    public async Task Repeating_a_proposal_is_idempotent()
    {
        var (origin, target, folderA, folderB) = await StartAsync();

        try
        {
            var taskId = Ulid.New().ToString();
            var first = await origin.ProposalService.ProposeAsync(Input(taskId), CancellationToken.None);
            var second = await origin.ProposalService.ProposeAsync(Input(taskId), CancellationToken.None);

            Assert.False(first.IsError);
            Assert.Equal(first.State, second.State);
            Assert.Equal(taskId, second.TaskId);
            Assert.Single(origin.Tasks.ListByState(TaskRole.Origin));

            await WaitUntilAsync(
                () => target.Tasks.Get(taskId, TaskRole.Target) is not null,
                "El destino no recibió la tarea.");
            Assert.Single(target.Tasks.ListByState(TaskRole.Target));
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }

    [Fact]
    public async Task Ask_proposal_is_approved_and_executes()
    {
        var pipeName = "odw-approvals-test-" + Guid.NewGuid().ToString("N");
        var (origin, target, folderA, folderB) = await StartAsync(OutboundAsk(), enableApprovals: true, pipeName);

        try
        {
            var outcome = await origin.ProposalService.ProposeAsync(Input(), CancellationToken.None);

            Assert.Equal("WAITING_APPROVAL", outcome.State);
            Assert.NotNull(outcome.ApprovalId);
            Assert.Single(origin.Approvals.ListPending());

            var decision = await origin.ApprovalCoordinator.ApproveAsync(
                outcome.ApprovalId!, "human:cli:test", CancellationToken.None);

            Assert.Equal(Odw.Node.Approvals.ApprovalDecisionStatus.Approved, decision.Status);

            var current = origin.Tasks.Get(outcome.TaskId, TaskRole.Origin);
            Assert.NotNull(current);
            Assert.Equal(TaskState.Accepted, current.State);

            await WaitUntilAsync(
                () => target.Tasks.Get(outcome.TaskId, TaskRole.Target) is not null,
                "El destino no recibió la tarea aprobada.");

            await WaitUntilAsync(
                () => target.Tasks.Get(outcome.TaskId, TaskRole.Target)?.State == TaskState.Completed,
                "El destino no completó la tarea aprobada.");

            await origin.Poller!.RefreshOnceAsync();

            Assert.Equal(TaskState.Done, origin.Tasks.Get(outcome.TaskId, TaskRole.Origin)!.State);
            Assert.True(origin.Tasks.VerifyAudit().IsValid);
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }

    [Fact]
    public async Task Denied_proposal_is_rejected_and_never_sent()
    {
        var pipeName = "odw-approvals-test-" + Guid.NewGuid().ToString("N");
        var (origin, target, folderA, folderB) = await StartAsync(OutboundAsk(), enableApprovals: true, pipeName);

        try
        {
            var outcome = await origin.ProposalService.ProposeAsync(Input(), CancellationToken.None);

            var decision = await origin.ApprovalCoordinator.DenyAsync(
                outcome.ApprovalId!, "human:cli:test", "no procede", CancellationToken.None);

            Assert.Equal(Odw.Node.Approvals.ApprovalDecisionStatus.Denied, decision.Status);
            Assert.Equal(TaskState.Rejected, origin.Tasks.Get(outcome.TaskId, TaskRole.Origin)!.State);
            Assert.Null(target.Tasks.Get(outcome.TaskId, TaskRole.Target));
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }

    [Fact]
    public async Task Approval_pipe_lists_and_approves()
    {
        var pipeName = "odw-approvals-test-" + Guid.NewGuid().ToString("N");
        var (origin, target, folderA, folderB) = await StartAsync(OutboundAsk(), enableApprovals: true, pipeName);

        try
        {
            var outcome = await origin.ProposalService.ProposeAsync(Input(), CancellationToken.None);

            var list = await Odw.Approvals.ApprovalPipeClient.SendAsync(
                pipeName,
                new JsonObject { ["command"] = "list" },
                TimeSpan.FromSeconds(5));

            Assert.True(list!["ok"]!.GetValue<bool>());
            var items = list["data"]!.AsArray();
            Assert.Single(items);
            Assert.Equal(outcome.ApprovalId, items[0]!["approval_id"]!.GetValue<string>());

            var approve = await Odw.Approvals.ApprovalPipeClient.SendAsync(
                pipeName,
                new JsonObject
                {
                    ["command"] = "approve",
                    ["id"] = outcome.ApprovalId,
                    ["by"] = "human:cli:test",
                },
                TimeSpan.FromSeconds(5));

            Assert.True(approve!["ok"]!.GetValue<bool>());
            Assert.Equal(
                TaskState.Accepted,
                origin.Tasks.Get(outcome.TaskId, TaskRole.Origin)!.State);

            await WaitUntilAsync(
                () => target.Tasks.Get(outcome.TaskId, TaskRole.Target) is not null,
                "El destino no recibió la tarea aprobada por pipe.");
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }

    [Fact]
    public async Task Mcp_propose_tool_creates_and_sends_the_task()
    {
        var (origin, target, folderA, folderB) = await StartAsync();

        try
        {
            using var client = new HttpClient { BaseAddress = origin.McpBaseAddress };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", McpToken);

            var body = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "tools/call",
                ["params"] = new JsonObject
                {
                    ["name"] = "odw_propose_task",
                    ["arguments"] = new JsonObject
                    {
                        ["target_node"] = "PC-B",
                        ["kind"] = "analysis",
                        ["mode"] = "read_only",
                        ["repo"] = "SistemaMaestro",
                        ["base_ref"] = "main",
                        ["instructions"] = "Analizar el repositorio.",
                        ["allowed_paths"] = new JsonArray("src/"),
                        ["limits"] = new JsonObject { ["max_minutes"] = 30 },
                    },
                },
            };

            using var response = await client.PostAsync(
                "/mcp",
                new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            var result = json["result"]!;
            Assert.False(result["isError"]!.GetValue<bool>());

            var structured = result["structuredContent"]!;
            var taskId = structured["task_id"]!.GetValue<string>();
            Assert.Equal("ACCEPTED", structured["state"]!.GetValue<string>());

            await WaitUntilAsync(
                () => target.Tasks.Get(taskId, TaskRole.Target) is not null,
                "El destino no recibió la tarea del MCP.");
        }
        finally
        {
            await DisposeAsync(origin, target, folderA, folderB);
        }
    }
}
