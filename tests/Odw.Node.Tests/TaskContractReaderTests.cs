using System.Text.Json;
using Odw.Core.Hashing;
using Odw.Core.Model;
using Odw.Node.Contracts;
using Xunit;

namespace Odw.Node.Tests;

public class TaskContractReaderTests
{
    private const string Body =
        """
        {
          "protocol_version": 1,
          "task_id": "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
          "origin_node": "PC-A",
          "target_node": "PC-B",
          "parent_task_id": null,
          "lineage": { "hops": ["PC-A"], "max_depth": 1 },
          "kind": "analysis",
          "instructions": "Analizar la arquitectura y ejecutar las pruebas.\nNo modificar archivos.",
          "workspace": {
            "repo": "SistemaMaestro",
            "base_ref": "main",
            "mode": "read_only",
            "strategy": "clone",
            "allowed_paths": ["src/", "tests/"],
            "denied_paths": []
          },
          "limits": {
            "max_minutes": 45,
            "max_memory_gb": 12,
            "max_processes": 64,
            "network": "none",
            "max_cost_usd": 5
          },
          "executor": { "type": "opencode", "model_hint": null },
          "created_at": "2026-11-24T18:00:00Z",
          "expires_at": "2026-11-24T19:00:00Z",
          "approval": null
        }
        """;

    [Fact]
    public void Reads_the_full_contract()
    {
        using var document = JsonDocument.Parse(Body);

        var request = TaskContractReader.Read(document.RootElement);

        Assert.Equal(1, request.ProtocolVersion);
        Assert.Equal(TaskKind.Analysis, request.Kind);
        Assert.Equal(WorkspaceMode.ReadOnly, request.Workspace.Mode);
        Assert.Equal(["src/", "tests/"], request.Workspace.AllowedPaths);
        Assert.Equal(NetworkAccess.None, request.Limits.Network);
        Assert.Equal(5m, request.Limits.MaxCostUsd);
        Assert.Equal(["PC-A"], request.Lineage.Hops);
        Assert.Null(request.Approval);
        Assert.Equal(
            "sha256:1811ced43c7c77ec46a771902cdd039b5c8985bfdd70997f92696f3f6d4721d0",
            TaskHash.Compute(request));
    }

    [Fact]
    public void Reads_and_writes_model_profile()
    {
        var body = Body.Replace(
            "\"executor\": { \"type\": \"opencode\", \"model_hint\": null }",
            "\"executor\": { \"type\": \"opencode\", \"model_profile\": \"free\", \"model_hint\": null }");

        using var document = JsonDocument.Parse(body);
        var request = TaskContractReader.Read(document.RootElement);

        Assert.Equal("free", request.Executor.ModelProfile);

        using var written = JsonDocument.Parse(TaskContractWriter.Write(request));
        Assert.Equal(
            "free",
            written.RootElement.GetProperty("executor").GetProperty("model_profile").GetString());
    }

    [Theory]
    [InlineData("""{"task_id":"01JAZ5Z8Q4XK3V9C2M1N7P6R8T","origin_node":"PC-A","target_node":"PC-B","kind":"otro","instructions":"x","workspace":{"repo":"r","mode":"read_only","allowed_paths":["src"]},"limits":{"max_minutes":1,"max_memory_gb":1,"max_processes":1,"network":"none"},"created_at":"2026-11-24T18:00:00Z","expires_at":"2026-11-24T19:00:00Z"}""", "$.kind")]
    [InlineData("""{"task_id":"01JAZ5Z8Q4XK3V9C2M1N7P6R8T","origin_node":"PC-A","target_node":"PC-B","kind":"analysis","instructions":"x","limits":{"max_minutes":1,"max_memory_gb":1,"max_processes":1,"network":"none"},"created_at":"2026-11-24T18:00:00Z","expires_at":"2026-11-24T19:00:00Z"}""", "$.workspace")]
    [InlineData("""{"task_id":"01JAZ5Z8Q4XK3V9C2M1N7P6R8T","origin_node":"PC-A","target_node":"PC-B","kind":"analysis","instructions":"x","workspace":{"repo":"r","mode":"read_only","allowed_paths":["src"]},"created_at":"2026-11-24T18:00:00Z","expires_at":"2026-11-24T19:00:00Z"}""", "$.limits")]
    [InlineData("""{"task_id":"01JAZ5Z8Q4XK3V9C2M1N7P6R8T","origin_node":"PC-A","target_node":"PC-B","kind":"analysis","instructions":"x","workspace":{"repo":"r","mode":"read_only","allowed_paths":["src"]},"limits":{"max_minutes":1,"max_memory_gb":1,"max_processes":1,"network":"none"},"created_at":"no-fecha","expires_at":"2026-11-24T19:00:00Z"}""", "$.created_at")]
    public void Invalid_contracts_report_the_field(string json, string field)
    {
        using var document = JsonDocument.Parse(json);

        var exception = Assert.Throws<TaskContractException>(
            () => TaskContractReader.Read(document.RootElement));

        Assert.Equal(field, exception.Field);
    }
}
