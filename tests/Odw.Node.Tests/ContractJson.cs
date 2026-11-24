namespace Odw.Node.Tests;

internal static class ContractJson
{
    public static string Analysis(
        string taskId,
        string repo = "SistemaMaestro",
        string baseRef = "main",
        string mode = "read_only",
        int maxMinutes = 45)
        => $$"""
            {
              "protocol_version": 1,
              "task_id": "{{taskId}}",
              "origin_node": "PC-A",
              "target_node": "PC-B",
              "parent_task_id": null,
              "lineage": { "hops": [], "max_depth": 1 },
              "kind": "analysis",
              "instructions": "Analizar la arquitectura y ejecutar las pruebas.",
              "workspace": {
                "repo": "{{repo}}",
                "base_ref": "{{baseRef}}",
                "mode": "{{mode}}",
                "strategy": "clone",
                "allowed_paths": ["src/", "tests/"],
                "denied_paths": []
              },
              "limits": {
                "max_minutes": {{maxMinutes}},
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
}
