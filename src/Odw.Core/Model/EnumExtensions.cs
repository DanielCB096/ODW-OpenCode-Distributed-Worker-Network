using System.Text;

namespace Odw.Core.Model;

public static class TaskStateExtensions
{
    private static readonly HashSet<TaskState> OriginStates =
    [
        TaskState.Proposed, TaskState.WaitingApproval, TaskState.Approved, TaskState.Sent,
        TaskState.Accepted, TaskState.Running, TaskState.Unreachable, TaskState.Cancelling,
        TaskState.Done, TaskState.Failed, TaskState.Rejected, TaskState.Cancelled, TaskState.Expired,
        TaskState.Interrupted,
    ];

    private static readonly HashSet<TaskState> TargetStates =
    [
        TaskState.Received, TaskState.AwaitingLocalApproval, TaskState.Queued, TaskState.Running,
        TaskState.Cancelling, TaskState.Completed, TaskState.ReviewRequired, TaskState.Failed,
        TaskState.Rejected, TaskState.Cancelled, TaskState.Interrupted,
    ];

    private static readonly HashSet<TaskState> TerminalStates =
    [
        TaskState.Done, TaskState.Failed, TaskState.Rejected, TaskState.Cancelled, TaskState.Expired,
        TaskState.Completed, TaskState.ReviewRequired,
    ];

    public static bool IsOriginState(this TaskState state) => OriginStates.Contains(state);

    public static bool IsTargetState(this TaskState state) => TargetStates.Contains(state);

    public static bool IsTerminal(this TaskState state) => TerminalStates.Contains(state);

    public static bool IsTerminal(this TaskState state, TaskRole role)
        => state.IsTerminal() && (role == TaskRole.Origin ? state.IsOriginState() : state.IsTargetState());

    /// <summary>Nombre en el protocolo: PascalCase → UPPER_SNAKE_CASE.</summary>
    public static string ToContractString(this TaskState state) => ToUpperSnake(state.ToString());

    public static bool TryParseContractState(string? value, out TaskState state)
    {
        foreach (var candidate in Enum.GetValues<TaskState>())
        {
            if (string.Equals(candidate.ToContractString(), value, StringComparison.Ordinal))
            {
                state = candidate;
                return true;
            }
        }

        state = default;
        return false;
    }

    public static string ToWireValue(this TaskRole role)
        => role == TaskRole.Origin ? "origin" : "target";

    public static bool TryParseRole(string? value, out TaskRole role)
    {
        switch (value)
        {
            case "origin":
                role = TaskRole.Origin;
                return true;
            case "target":
                role = TaskRole.Target;
                return true;
            default:
                role = default;
                return false;
        }
    }

    public static string ToWireValue(this TaskKind kind) => kind switch
    {
        TaskKind.Analysis => "analysis",
        TaskKind.TestRun => "test_run",
        TaskKind.CodeChange => "code_change",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string ToWireValue(this WorkspaceMode mode) => mode switch
    {
        WorkspaceMode.ReadOnly => "read_only",
        WorkspaceMode.ReadWriteWorktree => "read_write_worktree",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    public static string ToWireValue(this WorkspaceStrategy strategy) => strategy switch
    {
        WorkspaceStrategy.Snapshot => "snapshot",
        WorkspaceStrategy.Clone => "clone",
        WorkspaceStrategy.Worktree => "worktree",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
    };

    public static string ToWireValue(this NetworkAccess network) => network switch
    {
        NetworkAccess.None => "none",
        NetworkAccess.Allow => "allow",
        _ => throw new ArgumentOutOfRangeException(nameof(network), network, null),
    };

    public static string ToWireValue(this TaskOutcome outcome) => outcome switch
    {
        TaskOutcome.Succeeded => "succeeded",
        TaskOutcome.Failed => "failed",
        TaskOutcome.ReviewRequired => "review_required",
        TaskOutcome.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    private static string ToUpperSnake(string value)
    {
        var builder = new StringBuilder(value.Length + 4);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]))
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString();
    }
}
