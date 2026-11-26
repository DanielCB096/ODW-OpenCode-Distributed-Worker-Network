using Odw.Core.Contracts;
using Odw.Core.Model;
using Xunit;

namespace Odw.Core.Tests;

public class ContractRecordTests
{
    [Fact]
    public void Executor_and_approval_records_construct()
    {
        var task = TestTasks.Valid();
        var hash = "sha256:" + new string('a', 64);
        var limits = new ExecutorLimits(45, 12, 64, false, 5m);
        var rules = new[] { new ToolRule("read", "*", ToolEffect.Allow) };
        var policy = new ToolPolicy(rules);

        var start = new ExecutorStartRequest(
            task.TaskId,
            "C:\\workspace",
            "haz algo",
            "opencode/free",
            limits,
            policy,
            new Dictionary<string, string> { ["A"] = "B" });

        var session = new ExecutorSession(
            "session-1", 42, "http://127.0.0.1:1234", TestTasks.CreatedAt);
        var eventItem = new ExecutorEvent(
            ExecutorEventKind.ToolCalled, "read", "detail", TestTasks.CreatedAt);
        var outcome = new ExecutorOutcome(
            ExecutorOutcomeKind.Succeeded, "OK", "hecho");
        var snapshot = new ExecutorSnapshot(
            "Running", 50.5, "tests", TestTasks.CreatedAt, false);

        var approvalRequest = new ApprovalRequest(
            "approval-1", ApprovalScope.Task, task.TaskId, hash, TaskRole.Origin,
            "resumen", TestTasks.CreatedAt, "nonce-1234567890123456");
        var approvalOutcome = new ApprovalOutcome(
            ApprovalDecisionKind.Approved, "human:cli:tester", TestTasks.CreatedAt, "ok");

        Assert.Equal("session-1", session.SessionId);
        Assert.Equal(42, session.ProcessId);
        Assert.Equal(ExecutorEventKind.ToolCalled, eventItem.Kind);
        Assert.Equal(ExecutorOutcomeKind.Succeeded, outcome.Kind);
        Assert.Equal(50.5, snapshot.ProgressPercent);
        Assert.Equal(TaskRole.Origin, approvalRequest.Role);
        Assert.Equal(ApprovalDecisionKind.Approved, approvalOutcome.Decision);
        Assert.Equal(limits, start.Limits);
        Assert.Equal(policy, start.Policy);
        Assert.Equal("B", start.Environment["A"]);
    }

    [Fact]
    public void Policy_records_expose_helpers()
    {
        var task = TestTasks.Valid();

        var request = new PolicyRequest(
            TaskRole.Origin,
            "PC-B",
            "trusted",
            task,
            ["git"],
            TestTasks.CreatedAt)
        {
            Actions = ["shell", "read"],
        };

        var auto = new PolicyDecision(PolicyEffect.Auto, "r1", "ok");
        var ask = new PolicyDecision(PolicyEffect.Ask, "r2", "humano");
        var deny = new PolicyDecision(PolicyEffect.Deny, "r3", "no");

        Assert.Equal("PC-B", request.PeerNodeId);
        Assert.Equal(2, request.Actions.Count);
        Assert.True(auto.IsAllowed);
        Assert.False(auto.RequiresHuman);
        Assert.True(ask.IsAllowed);
        Assert.True(ask.RequiresHuman);
        Assert.False(deny.IsAllowed);
        Assert.False(deny.RequiresHuman);
    }

    [Fact]
    public void Task_result_records_construct()
    {
        var hash = "sha256:" + new string('b', 64);

        var result = new TaskResult
        {
            TaskId = TestTasks.TaskId,
            State = TaskState.Done,
            Outcome = TaskOutcome.Succeeded,
            SummaryMarkdown = "todo bien",
            ChangedPaths = ["src/a.cs"],
            Branch = "odw/x",
            Commit = "abc123",
            DiffSha256 = hash,
            Tests = [new TestResult("dotnet test", TestStatus.Passed, 1200)],
            Artifacts = [new ArtifactRef("report.md", 42, hash, "text/markdown")],
            Usage = new TaskUsage(100, 50, 0.25m),
            ModelUsed = "opencode/free",
            Error = new TaskError("CODE", "mensaje", true),
            StartedAt = TestTasks.CreatedAt,
            FinishedAt = TestTasks.CreatedAt.AddMinutes(1),
        };

        Assert.Equal(1, result.ProtocolVersion);
        Assert.Equal("todo bien", result.SummaryMarkdown);
        Assert.Equal("abc123", result.Commit);
        Assert.Equal(hash, result.DiffSha256);
        Assert.Equal(TestStatus.Passed, result.Tests[0].Status);
        Assert.Equal(42, result.Artifacts[0].Bytes);
        Assert.Equal(0.25m, result.Usage!.CostUsd);
        Assert.True(result.Error!.Retryable);
    }

    [Fact]
    public void Tool_policy_presets_are_usable()
    {
        Assert.Contains(ToolPolicy.AllowAll.Rules, rule => rule.Effect == ToolEffect.Allow);
        Assert.Contains(ToolPolicy.AskAll.Rules, rule => rule.Effect == ToolEffect.Ask);
    }
}
