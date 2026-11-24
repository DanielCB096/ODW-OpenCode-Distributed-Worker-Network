using Odw.Core.Model;

namespace Odw.Core.Tests;

internal static class TestTasks
{
    public const string TaskId = "01JAZ5Z8Q4XK3V9C2M1N7P6R8T";
    public const string Instructions = "Analizar la arquitectura y ejecutar las pruebas.\nNo modificar archivos.";

    public static readonly DateTimeOffset CreatedAt =
        new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);

    public static TaskRequest Valid() => new()
    {
        TaskId = TaskId,
        OriginNode = "PC-A",
        TargetNode = "PC-B",
        Kind = TaskKind.Analysis,
        Instructions = Instructions,
        Workspace = new WorkspaceSpec
        {
            Repo = "SistemaMaestro",
            BaseRef = "main",
            Mode = WorkspaceMode.ReadOnly,
            Strategy = WorkspaceStrategy.Clone,
            AllowedPaths = ["src/", "tests/"],
            DeniedPaths = [],
        },
        Limits = new TaskLimits
        {
            MaxMinutes = 45,
            MaxMemoryGb = 12,
            MaxProcesses = 64,
            Network = NetworkAccess.None,
            MaxCostUsd = 5m,
        },
        CreatedAt = CreatedAt,
        ExpiresAt = CreatedAt.AddHours(1),
    };

    public static ApprovalEvidence ApprovalFor(TaskRequest request, string approver = "human:cli:tester") => new()
    {
        ApprovalId = "01JAZ6AAAAAAAAAAAAAAAAAAAAA",
        TaskHash = Odw.Core.Hashing.TaskHash.Compute(request),
        ApprovedBy = approver,
        ApprovedAt = CreatedAt.AddMinutes(2),
        Nonce = "0123456789abcdef0123456789abcdef",
        Signature = "hmac-sha256:" + new string('a', 64),
    };
}
