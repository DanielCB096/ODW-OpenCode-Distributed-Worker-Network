using System.Text;
using System.Text.Json.Nodes;
using Odw.Mcp.Abstractions;
using Odw.Mcp.Tools;
using Xunit;

namespace Odw.Mcp.Tests;

public class ArtifactToolTests
{
    private sealed class FakeArtifacts : IArtifactFetcher
    {
        public ArtifactContent? Content { get; set; }

        public Task<ArtifactContent?> FetchAsync(
            string taskId,
            string name,
            CancellationToken cancellationToken)
            => Task.FromResult(Content);
    }

    private static McpServer CreateServer(FakeArtifacts artifacts)
        => new(TaskTools.Create(new FakeTasks(), artifacts));

    private static JsonObject Arguments(string name = "diff.patch") => new()
    {
        ["task_id"] = McpTestData.TaskId,
        ["name"] = name,
    };

    [Fact]
    public async Task Lists_the_artifact_tool_when_a_fetcher_is_available()
    {
        var response = await CreateServer(new FakeArtifacts())
            .HandleAsync(McpTestData.Request("tools/list", 1), CancellationToken.None);

        var names = response!["result"]!["tools"]!.AsArray()
            .Select(tool => tool!["name"]!.GetValue<string>())
            .ToArray();

        Assert.Contains("odw_get_task_artifact", names);
    }

    [Fact]
    public async Task Returns_text_artifact_content()
    {
        var fetcher = new FakeArtifacts
        {
            Content = new ArtifactContent(
                "diff.patch",
                "text/x-diff",
                Encoding.UTF8.GetBytes("+HOLA")),
        };

        var response = await CreateServer(fetcher).HandleAsync(
            McpTestData.Request("tools/call", 1, new JsonObject
            {
                ["name"] = "odw_get_task_artifact",
                ["arguments"] = Arguments(),
            }),
            CancellationToken.None);

        var result = response!["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());

        var structured = result["structuredContent"]!;
        Assert.Equal("diff.patch", structured["name"]!.GetValue<string>());
        Assert.Equal("text/x-diff", structured["content_type"]!.GetValue<string>());
        Assert.Equal("utf8", structured["encoding"]!.GetValue<string>());
        Assert.Equal("+HOLA", structured["content"]!.GetValue<string>());
        Assert.Equal(5, structured["bytes"]!.GetValue<int>());
        Assert.Equal(64, structured["sha256"]!.GetValue<string>().Length);
    }

    [Fact]
    public async Task Missing_artifact_returns_error()
    {
        var response = await CreateServer(new FakeArtifacts()).HandleAsync(
            McpTestData.Request("tools/call", 1, new JsonObject
            {
                ["name"] = "odw_get_task_artifact",
                ["arguments"] = Arguments("missing.md"),
            }),
            CancellationToken.None);

        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Equal("ARTIFACT_NOT_FOUND", result["structuredContent"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Oversized_artifact_is_rejected()
    {
        var fetcher = new FakeArtifacts
        {
            Content = new ArtifactContent(
                "bundle.zip",
                "application/zip",
                new byte[512 * 1024 + 1]),
        };

        var response = await CreateServer(fetcher).HandleAsync(
            McpTestData.Request("tools/call", 1, new JsonObject
            {
                ["name"] = "odw_get_task_artifact",
                ["arguments"] = Arguments("bundle.zip"),
            }),
            CancellationToken.None);

        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Equal("ARTIFACT_TOO_LARGE", result["structuredContent"]!["code"]!.GetValue<string>());
    }
}
