using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Odw.Core.Model;
using Odw.Core.Primitives;
using Odw.Node.Artifacts;
using Odw.Node.Hosting;
using Odw.Node.Peers;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Scheduling;
using Odw.Policy;
using Xunit;

namespace Odw.Node.Tests;

public class NodeHostTests
{
    private static readonly string McpToken = Convert.ToBase64String(new byte[32]);
    private const string PeerToken = "peer-token-0123456789";
    private const string OtherPeerToken = "peer-c-token-0123456789";

    private static PolicyEngine AutoPolicy() => PolicyEngine.FromJson(
        """
        {
          "version": 1,
          "default": "ASK",
          "rules": [
            {"id": "auto-read", "priority": 10, "effect": "AUTO", "reason": "prueba",
             "when": {"direction": "in", "kind": ["analysis","test_run"], "mode": "read_only",
                      "peer_trust": "trusted"}}
          ]
        }
        """);

    private static PolicyEngine AskPolicy() => PolicyEngine.FromJson(
        """{"version":1,"default":"ASK","rules":[]}""");

    private static PolicyEngine DenyPolicy() => PolicyEngine.FromJson(
        """{"version":1,"default":"ASK","rules":[{"id":"deny","priority":1,"effect":"DENY","reason":"no"}]}""");

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(NodeHost host, string folder)
        {
            Host = host;
            Folder = folder;
        }

        public NodeHost Host { get; }

        public string Folder { get; }

        public static async Task<Fixture> StartAsync(
            PolicyEngine? policy = null,
            Func<NodeHostOptions, NodeHostOptions>? configure = null)
        {
            var folder = Path.Combine(Path.GetTempPath(), "odw-host-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            var options = new NodeHostOptions
            {
                NodeId = "PC-B",
                DatabasePath = Path.Combine(folder, "odw.db"),
                McpToken = McpToken,
                PeerBind = "127.0.0.1",
                PeerPort = 0,
                McpBind = "127.0.0.1",
                McpPort = 0,
                AllowLoopbackPeerBind = true,
                Peers =
                [
                    new PeerRegistration("PC-A", PeerToken),
                    new PeerRegistration("PC-C", OtherPeerToken),
                ],
                Capabilities = ["git", "dotnet"],
                Accepts = ["analysis", "test_run"],
                Policy = policy ?? AutoPolicy(),
                EnableApprovals = false,
                Pooling = false,
            };

            if (configure is not null)
            {
                options = configure(options);
            }

            var host = await NodeHost.StartAsync(options);
            return new Fixture(host, folder);
        }

        public HttpClient PeerClient(string token = PeerToken, string node = "PC-A")
        {
            var client = new HttpClient { BaseAddress = Host.PeerBaseAddress };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Odw-Node", node);
            return client;
        }

        public HttpClient McpClient(string? token = null)
        {
            var client = new HttpClient { BaseAddress = Host.McpBaseAddress };
            if (token is not null)
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();

            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static JsonObject Body(
        string taskId,
        string target = "PC-B",
        string instructions = "Analizar y ejecutar pruebas.",
        int maxDepth = 1,
        params string[] hops)
    {
        var hopsArray = new JsonArray();
        foreach (var hop in hops)
        {
            hopsArray.Add(hop);
        }

        return new JsonObject
        {
            ["protocol_version"] = 1,
            ["task_id"] = taskId,
            ["origin_node"] = "PC-A",
            ["target_node"] = target,
            ["parent_task_id"] = null,
            ["lineage"] = new JsonObject
            {
                ["hops"] = hopsArray,
                ["max_depth"] = maxDepth,
            },
            ["kind"] = "analysis",
            ["instructions"] = instructions,
            ["workspace"] = new JsonObject
            {
                ["repo"] = "SistemaMaestro",
                ["base_ref"] = "main",
                ["mode"] = "read_only",
                ["strategy"] = "clone",
                ["allowed_paths"] = new JsonArray("src/", "tests/"),
                ["denied_paths"] = new JsonArray(),
            },
            ["limits"] = new JsonObject
            {
                ["max_minutes"] = 45,
                ["max_memory_gb"] = 12,
                ["max_processes"] = 64,
                ["network"] = "none",
                ["max_cost_usd"] = 5,
            },
            ["executor"] = new JsonObject
            {
                ["type"] = "opencode",
                ["model_hint"] = null,
            },
            ["created_at"] = "2026-11-24T18:00:00Z",
            ["expires_at"] = "2026-11-24T19:00:00Z",
            ["approval"] = null,
        };
    }

    private static StringContent Json(JsonObject body)
        => new(body.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task Host_starts_on_loopback_and_answers_hello()
    {
        await using var fixture = await Fixture.StartAsync();
        using var client = fixture.PeerClient();

        var response = await client.GetAsync("/peer/v1/hello");
        response.EnsureSuccessStatusCode();

        var json = await ReadAsync(response);
        Assert.Equal("PC-B", json["node_id"]!.GetValue<string>());
        Assert.Equal(1, json["protocol_version"]!.GetValue<int>());
        Assert.NotEqual(0, fixture.Host.PeerBaseAddress.Port);
        Assert.NotEqual(0, fixture.Host.McpBaseAddress.Port);
        Assert.True(fixture.Host.PeerBaseAddress.IsLoopback);
        Assert.True(fixture.Host.McpBaseAddress.IsLoopback);
    }

    [Fact]
    public async Task Peer_auth_is_required()
    {
        await using var fixture = await Fixture.StartAsync();

        using (var anonymous = new HttpClient { BaseAddress = fixture.Host.PeerBaseAddress })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/peer/v1/hello")).StatusCode);
        }

        using (var wrongToken = fixture.PeerClient("otro-token"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await wrongToken.GetAsync("/peer/v1/hello")).StatusCode);
        }

        using (var wrongNode = fixture.PeerClient(PeerToken, "PC-Z"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await wrongNode.GetAsync("/peer/v1/hello")).StatusCode);
        }
    }

    [Fact]
    public async Task Post_task_is_idempotent_and_readable()
    {
        await using var fixture = await Fixture.StartAsync();
        using var client = fixture.PeerClient();
        var taskId = Ulid.New().ToString();
        var body = Body(taskId, hops: "PC-A");

        using (var first = await client.PostAsync("/peer/v1/tasks", Json(body)))
        {
            Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
            Assert.Equal($"/peer/v1/tasks/{taskId}", first.Headers.Location!.ToString());

            var json = await ReadAsync(first);
            Assert.Equal("QUEUED", json["state"]!.GetValue<string>());
            Assert.Equal(0, json["state_seq"]!.GetValue<int>());
        }

        using (var replay = await client.PostAsync("/peer/v1/tasks", Json(body)))
        {
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.True(replay.Headers.Contains("Idempotent-Replay"));
            Assert.Equal("QUEUED", (await ReadAsync(replay))["state"]!.GetValue<string>());
        }

        Assert.Single(fixture.Host.Tasks.ListByState(TaskRole.Target, TaskState.Queued));
        Assert.Single(fixture.Host.Tasks.GetEvents(taskId, TaskRole.Target));

        using var get = await client.GetAsync($"/peer/v1/tasks/{taskId}");
        get.EnsureSuccessStatusCode();
        var etag = get.Headers.ETag!.ToString();
        Assert.Contains("seq-0", etag);

        using var notModified = new HttpRequestMessage(HttpMethod.Get, $"/peer/v1/tasks/{taskId}");
        notModified.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag));
        Assert.Equal(HttpStatusCode.NotModified, (await client.SendAsync(notModified)).StatusCode);
    }

    [Fact]
    public async Task Same_id_with_other_content_conflicts()
    {
        await using var fixture = await Fixture.StartAsync();
        using var client = fixture.PeerClient();
        var taskId = Ulid.New().ToString();

        using var first = await client.PostAsync("/peer/v1/tasks", Json(Body(taskId)));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        using var second = await client.PostAsync(
            "/peer/v1/tasks",
            Json(Body(taskId, instructions: "Otra cosa.")));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("TASK_ID_CONFLICT", (await ReadAsync(second))["code"]!.GetValue<string>());
        Assert.Single(fixture.Host.Tasks.ListByState(TaskRole.Target));
    }

    [Fact]
    public async Task Ask_policy_leaves_task_awaiting_approval()
    {
        await using var fixture = await Fixture.StartAsync(AskPolicy());
        using var client = fixture.PeerClient();

        using var response = await client.PostAsync(
            "/peer/v1/tasks",
            Json(Body(Ulid.New().ToString())));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await ReadAsync(response);
        Assert.Equal("AWAITING_LOCAL_APPROVAL", json["state"]!.GetValue<string>());
        Assert.True(json["approval_required"]!.GetValue<bool>());
        Assert.Equal("wait_for_human", json["next_action"]!.GetValue<string>());
    }

    [Fact]
    public async Task Deny_policy_prevents_persistence()
    {
        await using var fixture = await Fixture.StartAsync(DenyPolicy());
        using var client = fixture.PeerClient();

        using var response = await client.PostAsync(
            "/peer/v1/tasks",
            Json(Body(Ulid.New().ToString())));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("POLICY_DENIED", (await ReadAsync(response))["code"]!.GetValue<string>());
        Assert.Empty(fixture.Host.Tasks.ListByState(TaskRole.Target));
    }

    [Fact]
    public async Task Lineage_loop_and_wrong_target_are_rejected()
    {
        await using var fixture = await Fixture.StartAsync();
        using var client = fixture.PeerClient();

        using var loop = await client.PostAsync(
            "/peer/v1/tasks",
            Json(Body(Ulid.New().ToString(), maxDepth: 2, hops: ["PC-A", "PC-B"])));

        Assert.Equal(HttpStatusCode.Conflict, loop.StatusCode);
        Assert.Equal("LINEAGE_LOOP", (await ReadAsync(loop))["code"]!.GetValue<string>());

        using var wrongTarget = await client.PostAsync(
            "/peer/v1/tasks",
            Json(Body(Ulid.New().ToString(), target: "PC-Z")));

        Assert.Equal(HttpStatusCode.BadRequest, wrongTarget.StatusCode);
    }

    [Fact]
    public async Task Another_peer_cannot_read_the_task()
    {
        await using var fixture = await Fixture.StartAsync();
        var taskId = Ulid.New().ToString();

        using (var owner = fixture.PeerClient())
        {
            using var created = await owner.PostAsync("/peer/v1/tasks", Json(Body(taskId)));
            Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);
        }

        using var stranger = fixture.PeerClient(OtherPeerToken, "PC-C");
        using var get = await stranger.GetAsync($"/peer/v1/tasks/{taskId}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Status_reports_queue()
    {
        await using var fixture = await Fixture.StartAsync();
        using var client = fixture.PeerClient();

        using var created = await client.PostAsync("/peer/v1/tasks", Json(Body(Ulid.New().ToString())));
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);

        var status = await ReadAsync(await client.GetAsync("/peer/v1/status"));
        Assert.Equal(1, status["resources"]!["queued_tasks"]!.GetValue<int>());
        Assert.Equal("PC-B", status["node_id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Mcp_requires_token_and_initializes()
    {
        await using var fixture = await Fixture.StartAsync();

        var body = """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18"}}""";

        using (var anonymous = fixture.McpClient())
        {
            using var response = await anonymous.PostAsync(
                "/mcp",
                new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var client = fixture.McpClient(McpToken);
        using var initialized = await client.PostAsync(
            "/mcp",
            new StringContent(body, Encoding.UTF8, "application/json"));

        initialized.EnsureSuccessStatusCode();
        var json = await ReadAsync(initialized);
        Assert.Equal("2025-06-18", json["result"]!["protocolVersion"]!.GetValue<string>());
    }

    [Fact]
    public async Task Scheduler_completes_a_queued_task_end_to_end()
    {
        var workspaces = new FakeWorkspacePreparer();
        var runner = new FakeExecutionRunner();

        await using var fixture = await Fixture.StartAsync(configure: options => options with
        {
            EnableScheduler = true,
            ExecutionRunner = runner,
            WorkspacePreparer = workspaces,
            SchedulerOptions = new ExecutionSchedulerOptions
            {
                MaxConcurrentTasks = 1,
                PollInterval = TimeSpan.FromMilliseconds(50),
            },
        });

        using var client = fixture.PeerClient();
        var taskId = Ulid.New().ToString();

        using var created = await client.PostAsync("/peer/v1/tasks", Json(Body(taskId)));
        Assert.Equal(HttpStatusCode.Accepted, created.StatusCode);

        JsonNode? view = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

        while (DateTimeOffset.UtcNow < deadline)
        {
            using var get = await client.GetAsync($"/peer/v1/tasks/{taskId}");
            view = await ReadAsync(get);

            if (view["state"]!.GetValue<string>() == "COMPLETED")
            {
                break;
            }

            await Task.Delay(50);
        }

        Assert.NotNull(view);
        Assert.Equal("COMPLETED", view!["state"]!.GetValue<string>());
        Assert.True(view["result_available"]!.GetValue<bool>());
        Assert.Single(runner.Requests);
        Assert.Single(workspaces.Requests);
    }

    [Theory]
    [InlineData("0.0.0.0", "127.0.0.1", true)]
    [InlineData("127.0.0.1", "127.0.0.1", false)]
    [InlineData("127.0.0.1", "100.64.0.10", true)]
    public async Task Invalid_binds_are_rejected(string peerBind, string mcpBind, bool allowLoopback)
    {
        var options = new NodeHostOptions
        {
            NodeId = "PC-B",
            DatabasePath = Path.Combine(Path.GetTempPath(), "odw-host-tests", Guid.NewGuid().ToString("N"), "odw.db"),
            McpToken = McpToken,
            PeerBind = peerBind,
            PeerPort = 0,
            McpBind = mcpBind,
            McpPort = 0,
            AllowLoopbackPeerBind = allowLoopback,
        };

        await Assert.ThrowsAsync<NodeConfigurationException>(() => NodeHost.StartAsync(options));
    }

    [Fact]
    public async Task Serves_artifacts_listed_in_the_result()
    {
        await using var fixture = await Fixture.StartAsync(configure: options => options with
        {
            ArtifactsRoot = Path.Combine(Path.GetDirectoryName(options.DatabasePath)!, "artifacts"),
        });

        var taskId = Ulid.New().ToString();
        var created = await fixture.Host.Tasks.CreateAsync(new CreateTaskRequest
        {
            TaskId = taskId,
            Role = TaskRole.Target,
            CounterpartNode = "PC-A",
            Kind = TaskKind.Analysis,
            Mode = WorkspaceMode.ReadOnly,
            State = TaskState.Received,
            TaskHash = "sha256:" + new string('a', 64),
            RequestJson = "{}",
            OriginNode = "PC-A",
            TargetNode = "PC-B",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        });

        var resultJson =
            """{"outcome":"succeeded","summary_md":"ok","artifacts":["diff.patch"],"requires_review":false}""";
        var transition = await fixture.Host.Tasks.TransitionAsync(new TransitionTaskRequest
        {
            TaskId = taskId,
            Role = TaskRole.Target,
            ExpectedState = TaskState.Received,
            ExpectedSeq = created.StateSeq,
            NextState = TaskState.Completed,
            EventType = "task.completed",
            Now = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
            ResultJson = resultJson,
        });
        Assert.Equal(TransitionStatus.Applied, transition.Status);

        var store = new ArtifactStore(fixture.Host.Options.ArtifactsRoot!);
        await store.SaveAsync(
            taskId,
            "diff.patch",
            Encoding.UTF8.GetBytes("+HOLA"),
            "text/x-diff",
            CancellationToken.None);

        using var client = fixture.PeerClient();
        var response = await client.GetAsync($"/peer/v1/tasks/{taskId}/artifacts/diff.patch");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("+HOLA", await response.Content.ReadAsStringAsync());
        Assert.Equal("text/x-diff", response.Content.Headers.ContentType?.MediaType);

        var missing = await client.GetAsync($"/peer/v1/tasks/{taskId}/artifacts/missing.md");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
