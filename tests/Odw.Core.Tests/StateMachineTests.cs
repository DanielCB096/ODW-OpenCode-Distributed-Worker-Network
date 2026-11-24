using Odw.Core.Model;
using Odw.Core.StateMachines;
using Xunit;

namespace Odw.Core.Tests;

public class StateMachineTests
{
    private static readonly TransitionContext ApprovalOk = TransitionContext.Satisfied;
    private static readonly TransitionContext NoApprovalMatch = new() { ApprovalMatches = false };
    private static readonly TransitionContext LeaseOk = new() { LeaseExpired = true };
    private static readonly TransitionContext LeaseNotExpired = new() { LeaseExpired = false };
    private static readonly TransitionContext NoResources = new() { ResourcesAvailable = false };
    private static readonly TransitionContext Violated = new() { ValidationViolated = true };
    private static readonly TransitionContext Clean = new() { ValidationViolated = false };
    private static readonly TransitionContext Expired = new() { ApprovalExpired = true };

    public static IEnumerable<object?[]> Cases()
    {
        // Origen
        yield return [TaskRole.Origin, TaskState.Proposed, TaskTrigger.PolicyAuto, TransitionContext.Default, TaskState.Approved];
        yield return [TaskRole.Origin, TaskState.Proposed, TaskTrigger.PolicyAsk, TransitionContext.Default, TaskState.WaitingApproval];
        yield return [TaskRole.Origin, TaskState.Proposed, TaskTrigger.PolicyDeny, TransitionContext.Default, TaskState.Rejected];
        yield return [TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.ApprovalGranted, ApprovalOk, TaskState.Approved];
        yield return [TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.ApprovalGranted, NoApprovalMatch, null];
        yield return [TaskRole.Origin, TaskState.WaitingApproval, TaskTrigger.ApprovalExpired, Expired, TaskState.Expired];
        yield return [TaskRole.Origin, TaskState.Approved, TaskTrigger.TransportSent, TransitionContext.Default, TaskState.Sent];
        yield return [TaskRole.Origin, TaskState.Approved, TaskTrigger.ApprovalExpired, Expired, TaskState.Expired];
        yield return [TaskRole.Origin, TaskState.Sent, TaskTrigger.TransportAccepted, TransitionContext.Default, TaskState.Accepted];
        yield return [TaskRole.Origin, TaskState.Sent, TaskTrigger.TransportFailed, TransitionContext.Default, TaskState.Unreachable];
        yield return [TaskRole.Origin, TaskState.Accepted, TaskTrigger.LeaseExpired, LeaseOk, TaskState.Unreachable];
        yield return [TaskRole.Origin, TaskState.Accepted, TaskTrigger.LeaseExpired, LeaseNotExpired, null];
        yield return [TaskRole.Origin, TaskState.Running, TaskTrigger.TaskCompleted, TransitionContext.Default, TaskState.Done];
        yield return [TaskRole.Origin, TaskState.Cancelling, TaskTrigger.CancelTimeout, TransitionContext.Default, TaskState.Cancelled];
        yield return [TaskRole.Origin, TaskState.Done, TaskTrigger.UserCancel, TransitionContext.Default, null];

        // Destino
        yield return [TaskRole.Target, TaskState.Received, TaskTrigger.PolicyAsk, TransitionContext.Default, TaskState.AwaitingLocalApproval];
        yield return [TaskRole.Target, TaskState.AwaitingLocalApproval, TaskTrigger.ApprovalGranted, ApprovalOk, TaskState.Queued];
        yield return [TaskRole.Target, TaskState.AwaitingLocalApproval, TaskTrigger.ApprovalExpired, Expired, TaskState.Rejected];
        yield return [TaskRole.Target, TaskState.Queued, TaskTrigger.SlotAvailable, TransitionContext.Satisfied, TaskState.Running];
        yield return [TaskRole.Target, TaskState.Queued, TaskTrigger.SlotAvailable, NoResources, null];
        yield return [TaskRole.Target, TaskState.Running, TaskTrigger.TaskCompleted, Clean, TaskState.Completed];
        yield return [TaskRole.Target, TaskState.Running, TaskTrigger.TaskCompleted, Violated, TaskState.ReviewRequired];
        yield return [TaskRole.Target, TaskState.Running, TaskTrigger.ServiceRestart, TransitionContext.Default, TaskState.Interrupted];
        yield return [TaskRole.Target, TaskState.Interrupted, TaskTrigger.OperatorInspect, TransitionContext.Default, TaskState.ReviewRequired];
        yield return [TaskRole.Target, TaskState.Completed, TaskTrigger.UserCancel, TransitionContext.Default, null];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Transition_matches_specification(
        TaskRole role,
        TaskState from,
        TaskTrigger trigger,
        TransitionContext context,
        TaskState? expected)
    {
        var allowed = TaskStateMachine.TryTransition(role, from, trigger, context, out var to, out _);

        Assert.Equal(expected.HasValue, allowed);
        if (expected.HasValue)
        {
            Assert.Equal(expected.Value, to);
        }
    }

    [Fact]
    public void Guard_failure_reports_reason()
    {
        Assert.False(TaskStateMachine.TryTransition(
            TaskRole.Origin,
            TaskState.WaitingApproval,
            TaskTrigger.ApprovalGranted,
            NoApprovalMatch,
            out _,
            out var failure));

        Assert.Equal("approval_mismatch", failure);
    }

    [Fact]
    public void Terminal_states_have_no_outgoing_transitions()
    {
        foreach (var role in Enum.GetValues<TaskRole>())
        {
            foreach (var state in TaskStateMachine.StatesFor(role).Where(s => s.IsTerminal()))
            {
                foreach (var trigger in Enum.GetValues<TaskTrigger>())
                {
                    Assert.False(
                        TaskStateMachine.TryTransition(role, state, trigger, TransitionContext.Satisfied, out _, out _),
                        $"{role}/{state} no debe aceptar {trigger}.");
                }
            }
        }
    }

    [Fact]
    public void Every_successful_transition_stays_in_role()
    {
        var contexts = new[]
        {
            TransitionContext.Default,
            TransitionContext.Satisfied,
            new TransitionContext { ApprovalMatches = false },
            new TransitionContext { ApprovalExpired = true },
            new TransitionContext { LeaseExpired = false },
            new TransitionContext { ResourcesAvailable = false },
            new TransitionContext { ValidationViolated = true },
        };

        foreach (var role in Enum.GetValues<TaskRole>())
        {
            var states = TaskStateMachine.StatesFor(role);
            foreach (var from in states)
            {
                foreach (var trigger in Enum.GetValues<TaskTrigger>())
                {
                    foreach (var context in contexts)
                    {
                        if (TaskStateMachine.TryTransition(role, from, trigger, context, out var to, out _))
                        {
                            Assert.Contains(to, states);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void Aggregate_advances_sequence_and_records_change()
    {
        var aggregate = TaskAggregate.CreateOrigin(TestTasks.Valid());
        Assert.Equal(TaskState.Proposed, aggregate.State);
        Assert.Equal(0, aggregate.StateSeq);

        var at = TestTasks.CreatedAt.AddMinutes(1);
        var first = aggregate.Apply(TaskTrigger.PolicyAsk, TransitionContext.Default, at);
        var second = aggregate.Apply(TaskTrigger.ApprovalGranted, ApprovalOk, at.AddMinutes(1));

        Assert.Equal(1, first.Seq);
        Assert.Equal(2, second.Seq);
        Assert.Equal(TaskState.Proposed, first.From);
        Assert.Equal(TaskState.WaitingApproval, first.To);
        Assert.Equal(TaskState.Approved, second.To);
        Assert.Equal(2, aggregate.Changes.Count);
    }

    [Fact]
    public void Aggregate_rejects_invalid_transition()
    {
        var aggregate = TaskAggregate.Restore(
            TestTasks.TaskId,
            TaskRole.Origin,
            TaskState.Done,
            4,
            "sha256:" + new string('a', 64),
            TestTasks.CreatedAt);

        var exception = Assert.Throws<InvalidStateTransitionException>(
            () => aggregate.Apply(TaskTrigger.UserCancel, TransitionContext.Default, TestTasks.CreatedAt));

        Assert.Equal(TaskState.Done, exception.From);
        Assert.Equal(TaskTrigger.UserCancel, exception.Trigger);
        Assert.Equal(4, aggregate.StateSeq);
    }

    [Fact]
    public void Restore_rejects_state_from_other_role()
    {
        Assert.Throws<ArgumentException>(() => TaskAggregate.Restore(
            TestTasks.TaskId,
            TaskRole.Origin,
            TaskState.Completed,
            1,
            "sha256:" + new string('a', 64),
            TestTasks.CreatedAt));
    }

    [Fact]
    public void Contract_strings_are_upper_snake()
    {
        Assert.Equal("WAITING_APPROVAL", TaskState.WaitingApproval.ToContractString());
        Assert.Equal("AWAITING_LOCAL_APPROVAL", TaskState.AwaitingLocalApproval.ToContractString());
        Assert.Equal("REVIEW_REQUIRED", TaskState.ReviewRequired.ToContractString());
    }
}
