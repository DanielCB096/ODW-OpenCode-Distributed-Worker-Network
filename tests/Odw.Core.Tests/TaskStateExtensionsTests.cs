using Odw.Core.Model;
using Xunit;

namespace Odw.Core.Tests;

public class TaskStateExtensionsTests
{
    [Fact]
    public void Every_state_round_trips_to_contract_string()
    {
        foreach (var state in Enum.GetValues<TaskState>())
        {
            var text = state.ToContractString();

            Assert.Equal(text.ToUpperInvariant(), text);
            Assert.True(TaskStateExtensions.TryParseContractState(text, out var parsed));
            Assert.Equal(state, parsed);
        }

        Assert.False(TaskStateExtensions.TryParseContractState("NO_EXISTE", out _));
        Assert.False(TaskStateExtensions.TryParseContractState(null, out _));
    }

    [Fact]
    public void Origin_and_target_membership_are_disjoint()
    {
        var origin = new[]
        {
            TaskState.Proposed, TaskState.WaitingApproval, TaskState.Approved, TaskState.Sent,
            TaskState.Accepted, TaskState.Running, TaskState.Unreachable, TaskState.Cancelling,
            TaskState.Done, TaskState.Failed, TaskState.Rejected, TaskState.Cancelled, TaskState.Expired,
            TaskState.Interrupted,
        };
        var target = new[]
        {
            TaskState.Received, TaskState.AwaitingLocalApproval, TaskState.Queued, TaskState.Running,
            TaskState.Cancelling, TaskState.Completed, TaskState.ReviewRequired, TaskState.Failed,
            TaskState.Rejected, TaskState.Cancelled, TaskState.Interrupted,
        };

        foreach (var state in Enum.GetValues<TaskState>())
        {
            Assert.Equal(origin.Contains(state), state.IsOriginState());
            Assert.Equal(target.Contains(state), state.IsTargetState());
        }
    }

    [Fact]
    public void Terminal_states_are_role_aware()
    {
        Assert.True(TaskState.Done.IsTerminal());
        Assert.True(TaskState.Done.IsTerminal(TaskRole.Origin));
        Assert.False(TaskState.Done.IsTerminal(TaskRole.Target));

        Assert.True(TaskState.Completed.IsTerminal());
        Assert.True(TaskState.Completed.IsTerminal(TaskRole.Target));
        Assert.False(TaskState.Completed.IsTerminal(TaskRole.Origin));

        Assert.False(TaskState.Running.IsTerminal());
        Assert.False(TaskState.Running.IsTerminal(TaskRole.Origin));
        Assert.False(TaskState.Running.IsTerminal(TaskRole.Target));
    }

    [Fact]
    public void Wire_values_cover_every_enum()
    {
        Assert.Equal("origin", TaskRole.Origin.ToWireValue());
        Assert.Equal("target", TaskRole.Target.ToWireValue());

        Assert.Equal("analysis", TaskKind.Analysis.ToWireValue());
        Assert.Equal("test_run", TaskKind.TestRun.ToWireValue());
        Assert.Equal("code_change", TaskKind.CodeChange.ToWireValue());

        Assert.Equal("read_only", WorkspaceMode.ReadOnly.ToWireValue());
        Assert.Equal("read_write_worktree", WorkspaceMode.ReadWriteWorktree.ToWireValue());

        Assert.Equal("snapshot", WorkspaceStrategy.Snapshot.ToWireValue());
        Assert.Equal("clone", WorkspaceStrategy.Clone.ToWireValue());
        Assert.Equal("worktree", WorkspaceStrategy.Worktree.ToWireValue());

        Assert.Equal("none", NetworkAccess.None.ToWireValue());
        Assert.Equal("allow", NetworkAccess.Allow.ToWireValue());

        Assert.Equal("succeeded", TaskOutcome.Succeeded.ToWireValue());
        Assert.Equal("failed", TaskOutcome.Failed.ToWireValue());
        Assert.Equal("review_required", TaskOutcome.ReviewRequired.ToWireValue());
        Assert.Equal("cancelled", TaskOutcome.Cancelled.ToWireValue());
    }

    [Fact]
    public void Unknown_enum_values_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((TaskKind)99).ToWireValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((WorkspaceMode)99).ToWireValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((WorkspaceStrategy)99).ToWireValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((NetworkAccess)99).ToWireValue());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((TaskOutcome)99).ToWireValue());
    }

    [Fact]
    public void Roles_parse_from_wire_values()
    {
        Assert.True(TaskStateExtensions.TryParseRole("origin", out var origin));
        Assert.Equal(TaskRole.Origin, origin);

        Assert.True(TaskStateExtensions.TryParseRole("target", out var target));
        Assert.Equal(TaskRole.Target, target);

        Assert.False(TaskStateExtensions.TryParseRole("otro", out _));
        Assert.False(TaskStateExtensions.TryParseRole(null, out _));
    }
}
