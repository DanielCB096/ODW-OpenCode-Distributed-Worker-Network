using Odw.Core.Model;
using Odw.Core.StateMachines;
using Xunit;

namespace Odw.Core.Tests;

public class TaskAggregateTests
{
    [Fact]
    public void Create_and_apply_transition()
    {
        var task = TestTasks.Valid();
        var aggregate = TaskAggregate.CreateOrigin(task, TestTasks.CreatedAt);

        Assert.Equal(TaskState.Proposed, aggregate.State);
        Assert.Equal(0, aggregate.StateSeq);
        Assert.Empty(aggregate.Changes);
        Assert.True(aggregate.CanApply(TaskTrigger.PolicyAuto, TransitionContext.Default));
        Assert.False(aggregate.CanApply(TaskTrigger.TaskCompleted, TransitionContext.Default));

        var change = aggregate.Apply(
            TaskTrigger.PolicyAuto, TransitionContext.Default, TestTasks.CreatedAt.AddSeconds(1));

        Assert.Equal(TaskState.Approved, aggregate.State);
        Assert.Equal(1, aggregate.StateSeq);
        Assert.Single(aggregate.Changes);
        Assert.Equal(task.TaskId, change.TaskId);
        Assert.Equal(TaskRole.Origin, change.Role);
        Assert.Equal(1, change.Seq);
        Assert.Equal(TaskState.Proposed, change.From);
        Assert.Equal(TaskState.Approved, change.To);
        Assert.Equal(TaskTrigger.PolicyAuto, change.Trigger);

        var target = TaskAggregate.CreateTarget(task, TestTasks.CreatedAt);
        Assert.Equal(TaskState.Received, target.State);
        Assert.Equal(TaskRole.Target, target.Role);
    }

    [Fact]
    public void Invalid_transition_throws()
    {
        var aggregate = TaskAggregate.CreateOrigin(TestTasks.Valid(), TestTasks.CreatedAt);

        var exception = Assert.Throws<InvalidStateTransitionException>(
            () => aggregate.Apply(
                TaskTrigger.TaskCompleted, TransitionContext.Default, TestTasks.CreatedAt));

        Assert.Equal(TaskState.Proposed, aggregate.State);
        Assert.Equal(0, aggregate.StateSeq);
        Assert.Equal(TaskRole.Origin, exception.Role);
        Assert.Equal(TaskTrigger.TaskCompleted, exception.Trigger);
    }

    [Fact]
    public void Restore_validates_role_and_sequence()
    {
        Assert.Throws<ArgumentException>(() => TaskAggregate.Restore(
            TestTasks.TaskId,
            TaskRole.Origin,
            TaskState.Received,
            0,
            "sha256:" + new string('a', 64),
            TestTasks.CreatedAt));

        Assert.Throws<ArgumentOutOfRangeException>(() => TaskAggregate.Restore(
            TestTasks.TaskId,
            TaskRole.Origin,
            TaskState.Proposed,
            -1,
            "sha256:" + new string('a', 64),
            TestTasks.CreatedAt));

        var restored = TaskAggregate.Restore(
            TestTasks.TaskId,
            TaskRole.Target,
            TaskState.Queued,
            3,
            "sha256:" + new string('a', 64),
            TestTasks.CreatedAt);

        Assert.Equal(TaskRole.Target, restored.Role);
        Assert.Equal(TaskState.Queued, restored.State);
        Assert.Equal(3, restored.StateSeq);
    }

    [Fact]
    public void Create_requires_a_request()
        => Assert.Throws<ArgumentNullException>(() => TaskAggregate.CreateOrigin(null!));
}
