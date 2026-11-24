using Odw.Core.Model;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Scheduling;
using Odw.Node.Tasks;
using Odw.Sandbox.Workspaces;
using Xunit;

namespace Odw.Node.Tests;

/// <summary>
/// Integración real: workspace Git + OpenCode bajo Job Object + prompt de solo lectura.
/// Se activa con ODW_OPENCODE_EXE; sin la variable no hace nada.
/// </summary>
public class OpenCodeTaskRunnerIntegrationTests
{
    [Fact]
    public async Task Runs_a_real_task_in_an_isolated_workspace()
    {
        var executable = Environment.GetEnvironmentVariable("ODW_OPENCODE_EXE");
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        var repo = CreateRepo();
        var root = Path.Combine(Path.GetTempPath(), "odw-runner-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);
        var taskId = TestRows.NewId();

        var contract = ContractModels.Analysis(
            taskId,
            instructions: "Responde exactamente: OK. No uses herramientas.") with
        {
            Executor = new ExecutorSpec { ModelHint = TestModels.FreeOrFallback },
        };
        var contractJson = ContractJson.Analysis(taskId);
        var snapshot = await coordinator.AcceptAsync(
            contract, TaskRole.Target, contractJson, TaskState.Queued, "task.received", "{}");

        var manager = new WorkspaceManager(new WorkspaceOptions
        {
            WorkspaceRoot = root,
            Repos = new Dictionary<string, string> { ["SistemaMaestro"] = repo },
        });

        await using var workspace = await manager.PrepareAsync(
            new WorkspaceRequest(taskId, "SistemaMaestro", "main", ReadOnly: true),
            CancellationToken.None);

        var runner = new OpenCodeTaskRunner(new OpenCodeTaskRunnerOptions
        {
            Executable = executable,
            PinnedVersion = "2.3.2",
            ExecutionTimeout = TimeSpan.FromMinutes(2),
        });

        var result = await runner.RunAsync(
            new ExecutionRequest(snapshot, contract, workspace.Path, workspace.Branch),
            CancellationToken.None);

        Assert.Equal(Odw.Core.Contracts.ExecutorOutcomeKind.Succeeded, result.Kind);
        Assert.Contains("succeeded", result.ResultJson);
        Assert.Contains("OK", result.ResultJson, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateRepo()
    {
        var repo = Path.Combine(Path.GetTempPath(), "odw-runner-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        Run(repo, "init", "-q");
        Run(repo, "config", "user.email", "test@example.com");
        Run(repo, "config", "user.name", "Test");
        Run(repo, "checkout", "-q", "-b", "main");
        File.WriteAllText(Path.Combine(repo, "readme.txt"), "hola");
        Run(repo, "add", ".");
        Run(repo, "commit", "-q", "-m", "init");
        return repo;
    }

    private static void Run(string directory, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git falló: {error}");
        }
    }
}
