using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Scheduling;
using Odw.Node.Tasks;
using Xunit;

namespace Odw.Node.Tests;

/// <summary>Guardas de red: con aislamiento activo, una tarea incompatible no arranca nada.</summary>
public class OpenCodeTaskRunnerGuardTests
{
    [Fact]
    public async Task Rejects_allow_when_network_isolation_is_required()
    {
        var (snapshot, contract) = await CreateAsync(new TaskLimits
        {
            Network = NetworkAccess.Allow,
        });

        var runner = CreateRunner(proxyUrl: "http://127.0.0.1:1", enforced: true);
        var result = await runner.RunAsync(
            new ExecutionRequest(snapshot, contract, Path.GetTempPath(), "main"),
            CancellationToken.None);

        Assert.Equal(ExecutorOutcomeKind.Failed, result.Kind);
        Assert.Contains("NETWORK_ALLOW_UNSUPPORTED", result.ResultJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_none_without_the_model_proxy()
    {
        var (snapshot, contract) = await CreateAsync(new TaskLimits());

        var runner = CreateRunner(proxyUrl: null, enforced: true);
        var result = await runner.RunAsync(
            new ExecutionRequest(snapshot, contract, Path.GetTempPath(), "main"),
            CancellationToken.None);

        Assert.Equal(ExecutorOutcomeKind.Failed, result.Kind);
        Assert.Contains("NETWORK_UNENFORCED", result.ResultJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_none_when_wfp_is_not_installed()
    {
        var (snapshot, contract) = await CreateAsync(new TaskLimits());

        var runner = CreateRunner(proxyUrl: "http://127.0.0.1:1", enforced: false);
        var result = await runner.RunAsync(
            new ExecutionRequest(snapshot, contract, Path.GetTempPath(), "main"),
            CancellationToken.None);

        Assert.Equal(ExecutorOutcomeKind.Failed, result.Kind);
        Assert.Contains("NETWORK_UNENFORCED", result.ResultJson, StringComparison.Ordinal);
    }

    private static OpenCodeTaskRunner CreateRunner(string? proxyUrl, bool enforced)
        => new(new OpenCodeTaskRunnerOptions
        {
            Executable = "opencode-inexistente-odw.exe",
            RequireNetworkIsolation = true,
            ModelProxyUrl = proxyUrl,
            NetworkIsolationProbe = () => enforced,
        });

    private static async Task<(TaskSnapshot Snapshot, TaskRequest Contract)> CreateAsync(TaskLimits limits)
    {
        await using var fixture = TestDatabase.Create();
        var store = new TaskStore(fixture.Database, fixture.Time);
        var coordinator = new TaskCoordinator(store, fixture.Time);
        var taskId = TestRows.NewId();
        var contract = ContractModels.Analysis(taskId) with { Limits = limits };
        var snapshot = await coordinator.AcceptAsync(
            contract,
            TaskRole.Target,
            ContractJson.Analysis(taskId),
            TaskState.Queued,
            "task.received",
            "{}");

        return (snapshot, contract);
    }
}
