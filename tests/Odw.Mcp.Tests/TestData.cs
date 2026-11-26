using System.Text.Json.Nodes;
using Odw.Core.Model;
using Odw.Mcp.Abstractions;

namespace Odw.Mcp.Tests;

internal sealed class FakePeers : IPeerDirectory
{
    public List<PeerStatusSnapshot> Peers { get; } = [];

    public Task<IReadOnlyList<PeerStatusSnapshot>> ListAsync(bool includeOffline, CancellationToken cancellationToken)
    {
        IReadOnlyList<PeerStatusSnapshot> result = Peers
            .Where(peer => includeOffline || peer.IsOnline)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<PeerStatusSnapshot?> GetAsync(string nodeId, CancellationToken cancellationToken)
        => Task.FromResult(Peers.FirstOrDefault(peer => peer.NodeId == nodeId));
}

internal sealed class FakeTasks : ITaskQuery
{
    public Dictionary<string, TaskStatusView> Statuses { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, TaskResultView> Results { get; } = new(StringComparer.Ordinal);

    public Task<TaskStatusView?> GetStatusAsync(string taskId, CancellationToken cancellationToken)
        => Task.FromResult(Statuses.TryGetValue(taskId, out var status) ? status : null);

    public Task<TaskResultView?> GetResultAsync(string taskId, bool includeEvents, CancellationToken cancellationToken)
        => Task.FromResult(Results.TryGetValue(taskId, out var result) ? result : null);
}

internal static class McpTestData
{
    public const string TaskId = "01JAZ5Z8Q4XK3V9C2M1N7P6R8T";

    public static PeerStatusSnapshot Peer(string nodeId = "PC-B", bool online = true) => new(
        nodeId,
        online ? "online" : "offline",
        "trusted",
        online ? DateTimeOffset.UnixEpoch.AddDays(1) : null,
        1,
        0,
        0.2,
        ["analysis"],
        ["git", "dotnet"],
        21.0,
        18.0,
        true);

    public static TaskStatusView Status(TaskState state = TaskState.Running) => new(
        TaskId,
        TaskRole.Origin,
        state,
        3,
        "PC-B",
        DateTimeOffset.UnixEpoch.AddHours(1),
        DateTimeOffset.UnixEpoch.AddHours(2),
        "tests",
        42,
        "none",
        false,
        false);

    public static TaskResultView Result(bool available = true) => new(
        TaskId,
        TaskState.Completed,
        TaskOutcome.Succeeded,
        "Todo verde.",
        ["src/Foo.cs"],
        "odw/" + TaskId,
        "9f1c2ab",
        ["report.md"],
        false,
        available,
        null,
        false,
        [new TaskEventView(1, "task.completed", DateTimeOffset.UnixEpoch, "{}")],
        "free",
        "opencode",
        "deepseek-v4.1-flash-free",
        0m);

    public static JsonObject Request(string method, JsonNode? id, JsonObject? parameters = null)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };

        if (id is not null)
        {
            request["id"] = id.DeepClone();
        }

        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        return request;
    }
}
