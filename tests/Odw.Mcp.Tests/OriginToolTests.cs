using System.Text.Json.Nodes;
using Odw.Core.Model;
using Odw.Mcp.Abstractions;
using Odw.Mcp.Tools;
using Xunit;

namespace Odw.Mcp.Tests;

public class OriginToolTests
{
    private sealed class FakeProposals : ITaskProposalService
    {
        public ProposalInput? Last { get; private set; }

        public ProposalOutcome Outcome { get; set; } = new(
            "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
            "ACCEPTED",
            false,
            "sha256:" + new string('a', 64),
            "none");

        public Task<ProposalOutcome> ProposeAsync(ProposalInput input, CancellationToken cancellationToken)
        {
            Last = input;
            return Task.FromResult(Outcome);
        }
    }

    private static JsonObject Arguments() => new()
    {
        ["target_node"] = "PC-B",
        ["kind"] = "analysis",
        ["mode"] = "read_only",
        ["repo"] = "SistemaMaestro",
        ["instructions"] = "Analizar el repositorio.",
        ["allowed_paths"] = new JsonArray("src/"),
        ["limits"] = new JsonObject
        {
            ["max_minutes"] = 30,
            ["max_memory_gb"] = 8,
            ["max_processes"] = 32,
            ["network"] = "none",
            ["max_cost_usd"] = 2.5,
        },
    };

    private static async Task<JsonObject?> CallAsync(JsonObject arguments)
    {
        var server = new McpServer(OriginTools.Create(new FakeProposals()));
        var request = McpTestData.Request("tools/call", 1, new JsonObject
        {
            ["name"] = "odw_propose_task",
            ["arguments"] = arguments,
        });

        return await server.HandleAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Valid_arguments_are_mapped()
    {
        var proposals = new FakeProposals();
        var server = new McpServer(OriginTools.Create(proposals));

        var response = await server.HandleAsync(
            McpTestData.Request("tools/call", 1, new JsonObject
            {
                ["name"] = "odw_propose_task",
                ["arguments"] = Arguments(),
            }),
            CancellationToken.None);

        var result = response!["result"]!;
        Assert.False(result["isError"]!.GetValue<bool>());
        Assert.Equal("ACCEPTED", result["structuredContent"]!["state"]!.GetValue<string>());

        var input = proposals.Last;
        Assert.NotNull(input);
        Assert.Equal("PC-B", input!.TargetNode);
        Assert.Equal(TaskKind.Analysis, input.Kind);
        Assert.Equal(WorkspaceMode.ReadOnly, input.Mode);
        Assert.Equal(30, input.MaxMinutes);
        Assert.Equal(2.5m, input.MaxCostUsd);
    }

    [Fact]
    public async Task Missing_fields_are_rejected_without_calling_the_service()
    {
        var response = await CallAsync(new JsonObject { ["target_node"] = "PC-B" });

        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Equal("CONTRACT_INVALID", result["structuredContent"]!["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Service_errors_are_propagated()
    {
        var proposals = new FakeProposals
        {
            Outcome = new ProposalOutcome(
                "01JAZ5Z8Q4XK3V9C2M1N7P6R8T", "REJECTED", false, string.Empty, "none",
                "POLICY_DENIED", "regla"),
        };

        var server = new McpServer(OriginTools.Create(proposals));
        var response = await server.HandleAsync(
            McpTestData.Request("tools/call", 1, new JsonObject
            {
                ["name"] = "odw_propose_task",
                ["arguments"] = Arguments(),
            }),
            CancellationToken.None);

        var result = response!["result"]!;
        Assert.True(result["isError"]!.GetValue<bool>());
        Assert.Equal("POLICY_DENIED", result["structuredContent"]!["code"]!.GetValue<string>());
    }
}
