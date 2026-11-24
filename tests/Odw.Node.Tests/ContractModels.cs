using Odw.Core.Model;

namespace Odw.Node.Tests;

internal static class ContractModels
{
    public const string TaskId = "01JAZ5Z8Q4XK3V9C2M1N7P6R8T";

    public static TaskRequest Analysis(
        string? taskId = null,
        string origin = "PC-A",
        string target = "PC-B",
        string instructions = "Analizar la arquitectura y ejecutar las pruebas.",
        IReadOnlyList<string>? hops = null,
        int maxDepth = 1)
        => new()
        {
            TaskId = taskId ?? TaskId,
            OriginNode = origin,
            TargetNode = target,
            Lineage = new Lineage { Hops = hops ?? [], MaxDepth = maxDepth },
            Kind = TaskKind.Analysis,
            Instructions = instructions,
            Workspace = new WorkspaceSpec
            {
                Repo = "SistemaMaestro",
                Mode = WorkspaceMode.ReadOnly,
                AllowedPaths = ["src/", "tests/"],
            },
            Limits = new TaskLimits(),
            CreatedAt = TestTime.DefaultNow,
            ExpiresAt = TestTime.DefaultNow.AddHours(1),
        };
}
