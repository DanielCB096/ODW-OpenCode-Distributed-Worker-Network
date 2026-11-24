namespace Odw.Core.Model;

public enum TaskRole
{
    Origin,
    Target,
}

public enum TaskState
{
    // Vista del origen
    Proposed,
    WaitingApproval,
    Approved,
    Sent,
    Accepted,
    Running,
    Unreachable,
    Cancelling,
    Done,
    Failed,
    Rejected,
    Cancelled,
    Expired,

    // Vista del destino
    Received,
    AwaitingLocalApproval,
    Queued,
    Completed,
    ReviewRequired,
    Interrupted,
}

public enum TaskKind
{
    Analysis,
    TestRun,
    CodeChange,
}

public enum WorkspaceMode
{
    ReadOnly,
    ReadWriteWorktree,
}

public enum WorkspaceStrategy
{
    Snapshot,
    Clone,
    Worktree,
}

public enum NetworkAccess
{
    None,
    Allow,
}

public enum TaskOutcome
{
    Succeeded,
    Failed,
    ReviewRequired,
    Cancelled,
}

public enum TestStatus
{
    Passed,
    Failed,
    Skipped,
}

public enum MessageDelivery
{
    Queue,
    Steer,
}
