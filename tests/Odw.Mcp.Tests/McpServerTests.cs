using System.Text.Json.Nodes;
using Odw.Mcp.Tools;
using Xunit;

namespace Odw.Mcp.Tests;

public class McpServerTests
{
    private static McpServer CreateServer(FakePeers? peers = null, FakeTasks? tasks = null)
    {
        peers ??= new FakePeers();
        tasks ??= new FakeTasks();

        return new McpServer(PeerTools.Create(peers).Concat(TaskTools.Create(tasks)));
    }

    [Fact]
    public async Task Initialize_negotiates_protocol()
    {
        var server = CreateServer();
        var request = McpTestData.Request("initialize", 1, new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "1.0" },
        });

        var response = await server.HandleAsync(request, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("2.0", response!["jsonrpc"]!.GetValue<string>());
        Assert.Equal("2024-11-05", response["result"]!["protocolVersion"]!.GetValue<string>());
        Assert.NotNull(response["result"]!["capabilities"]!["tools"]);
    }

    [Fact]
    public async Task Initialize_falls_back_to_latest_version()
    {
        var server = CreateServer();
        var request = McpTestData.Request("initialize", 1, new JsonObject
        {
            ["protocolVersion"] = "1999-01-01",
        });

        var response = await server.HandleAsync(request, CancellationToken.None);

        Assert.Equal(
            McpServer.LatestProtocolVersion,
            response!["result"]!["protocolVersion"]!.GetValue<string>());
    }

    [Fact]
    public async Task Tools_list_exposes_read_only_tools()
    {
        var server = CreateServer();
        var response = await server.HandleAsync(McpTestData.Request("tools/list", 2), CancellationToken.None);

        var tools = response!["result"]!["tools"]!.AsArray();
        Assert.Equal(4, tools.Count);
        Assert.All(tools, tool => Assert.True(tool!["annotations"]!["readOnlyHint"]!.GetValue<bool>()));

        var names = tools.Select(tool => tool!["name"]!.GetValue<string>()).ToArray();
        Assert.Contains("odw_list_peers", names);
        Assert.Contains("odw_get_task_result", names);
    }

    [Fact]
    public async Task Notification_has_no_response()
    {
        var server = CreateServer();
        var request = McpTestData.Request("notifications/initialized", null);

        Assert.Null(await server.HandleAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Ping_returns_empty_result()
    {
        var response = await CreateServer().HandleAsync(McpTestData.Request("ping", 3), CancellationToken.None);

        Assert.NotNull(response!["result"]);
        Assert.Empty(response["result"]!.AsObject());
    }

    [Fact]
    public async Task Unknown_method_is_reported()
    {
        var response = await CreateServer().HandleAsync(
            McpTestData.Request("resources/list", 4),
            CancellationToken.None);

        Assert.Equal(JsonRpc.ErrorCodes.MethodNotFound, response!["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task Invalid_jsonrpc_version_is_rejected()
    {
        var request = McpTestData.Request("ping", 5);
        request["jsonrpc"] = "1.0";

        var response = await CreateServer().HandleAsync(request, CancellationToken.None);

        Assert.Equal(JsonRpc.ErrorCodes.InvalidRequest, response!["error"]!["code"]!.GetValue<int>());
    }

    [Fact]
    public async Task Unknown_tool_is_invalid_params()
    {
        var request = McpTestData.Request("tools/call", 6, new JsonObject
        {
            ["name"] = "odw_approve_task",
            ["arguments"] = new JsonObject(),
        });

        var response = await CreateServer().HandleAsync(request, CancellationToken.None);

        Assert.Equal(JsonRpc.ErrorCodes.InvalidParams, response!["error"]!["code"]!.GetValue<int>());
        Assert.Equal("TOOL_NOT_FOUND", response["error"]!["data"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task List_peers_returns_structured_content()
    {
        var peers = new FakePeers();
        peers.Peers.Add(McpTestData.Peer("PC-B", online: true));
        peers.Peers.Add(McpTestData.Peer("PC-C", online: false));
        var request = McpTestData.Request("tools/call", 7, new JsonObject
        {
            ["name"] = "odw_list_peers",
            ["arguments"] = new JsonObject { ["include_offline"] = false },
        });

        var response = await CreateServer(peers).HandleAsync(request, CancellationToken.None);

        var result = response!["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        var structured = result["structuredContent"]!;
        Assert.Single(structured["peers"]!.AsArray());
        Assert.Equal("PC-B", structured["peers"]![0]!["node_id"]!.GetValue<string>());
        Assert.Equal("online", structured["peers"]![0]!["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Get_unknown_peer_is_tool_error()
    {
        var response = await CallToolAsync(
            new FakePeers(),
            new FakeTasks(),
            "odw_get_peer_status",
            new JsonObject { ["node_id"] = "PC-Z" });

        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Equal("PEER_UNKNOWN", result["structuredContent"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Task_status_invalid_id_is_tool_error()
    {
        var response = await CallToolAsync(
            new FakePeers(),
            new FakeTasks(),
            "odw_get_task_status",
            new JsonObject { ["task_id"] = "no-es-ulid" });

        Assert.Equal(
            "CONTRACT_INVALID",
            response!["result"]!["structuredContent"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Task_result_without_result_is_conflict()
    {
        var tasks = new FakeTasks();
        tasks.Results[McpTestData.TaskId] = McpTestData.Result(available: false);

        var response = await CallToolAsync(
            new FakePeers(),
            tasks,
            "odw_get_task_result",
            new JsonObject { ["task_id"] = McpTestData.TaskId });

        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Equal("TASK_STATE_CONFLICT", result["structuredContent"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Task_result_returns_structured_result()
    {
        var tasks = new FakeTasks();
        tasks.Results[McpTestData.TaskId] = McpTestData.Result();

        var response = await CallToolAsync(
            new FakePeers(),
            tasks,
            "odw_get_task_result",
            new JsonObject { ["task_id"] = McpTestData.TaskId, ["include_events"] = true });

        var result = response!["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        Assert.Equal("succeeded", result["structuredContent"]!["outcome"]!.GetValue<string>());
        Assert.Single(result["structuredContent"]!["events"]!.AsArray());
    }

    private static async Task<JsonObject?> CallToolAsync(
        FakePeers peers,
        FakeTasks tasks,
        string name,
        JsonObject arguments)
    {
        var request = McpTestData.Request("tools/call", 10, new JsonObject
        {
            ["name"] = name,
            ["arguments"] = arguments,
        });

        return await CreateServer(peers, tasks).HandleAsync(request, CancellationToken.None);
    }
}
