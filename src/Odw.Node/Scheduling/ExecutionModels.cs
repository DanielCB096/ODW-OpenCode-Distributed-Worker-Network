using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Node.Persistence.Tasks;

namespace Odw.Node.Scheduling;

public sealed record ExecutionRequest(
    TaskSnapshot Snapshot,
    TaskRequest Contract,
    string WorkspacePath,
    string WorkspaceBranch = "");

public sealed record ExecutionResult(
    ExecutorOutcomeKind Kind,
    string ResultJson,
    bool RequiresReview = false);

public interface ITaskExecutionRunner
{
    Task<ExecutionResult> RunAsync(ExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record ExecutionSchedulerOptions
{
    public int MaxConcurrentTasks { get; init; } = 1;

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
}
