using Odw.Core.Contracts;
using Odw.Core.Model;

namespace Odw.Policy.Tests;

internal static class PolicyFixtures
{
    public const string TaskId = "01JAZ5Z8Q4XK3V9C2M1N7P6R8T";

    public static readonly DateTimeOffset Now = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);

    public static TaskRequest TaskRequest(
        TaskKind kind = TaskKind.Analysis,
        WorkspaceMode mode = WorkspaceMode.ReadOnly,
        string repo = "SistemaMaestro",
        string? parent = null,
        int minutes = 45,
        int memoryGb = 12,
        decimal cost = 5m,
        NetworkAccess network = NetworkAccess.None)
        => new()
        {
            TaskId = TaskId,
            OriginNode = "PC-A",
            TargetNode = "PC-B",
            ParentTaskId = parent,
            Kind = kind,
            Instructions = "Analizar y ejecutar pruebas.",
            Workspace = new WorkspaceSpec
            {
                Repo = repo,
                Mode = mode,
                AllowedPaths = ["src/"],
            },
            Limits = new TaskLimits
            {
                MaxMinutes = minutes,
                MaxMemoryGb = memoryGb,
                MaxCostUsd = cost,
                Network = network,
            },
            CreatedAt = Now,
            ExpiresAt = Now.AddHours(1),
        };

    public static PolicyRequest Request(
        TaskRequest? task = null,
        TaskRole role = TaskRole.Target,
        string trust = "trusted",
        string[]? capabilities = null,
        string[]? actions = null,
        DateTimeOffset? now = null)
        => new(
            role,
            "PC-A",
            trust,
            task ?? TaskRequest(),
            capabilities ?? ["git", "dotnet"],
            now ?? Now)
        {
            Actions = actions ?? [],
        };

    public static string Document(string rules, string @default = "ASK")
        => $$"""
            {
              "version": 1,
              "default": "{{@default}}",
              "rules": [{{rules}}]
            }
            """;
}
