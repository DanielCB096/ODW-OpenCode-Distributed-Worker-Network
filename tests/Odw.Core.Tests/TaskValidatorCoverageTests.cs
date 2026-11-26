using Odw.Core.Model;
using Odw.Core.Validation;
using Xunit;

namespace Odw.Core.Tests;

public class TaskValidatorCoverageTests
{
    private static void AssertIssue(TaskRequest request, string code, string field)
    {
        var result = TaskValidator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == code && issue.Field == field);
    }

    [Fact]
    public void Unsupported_protocol_is_rejected()
        => AssertIssue(
            TestTasks.Valid() with { ProtocolVersion = 2 },
            ValidationCodes.ProtocolUnsupported,
            "protocol_version");

    [Fact]
    public void Invalid_identifiers_are_rejected()
    {
        var task = TestTasks.Valid();

        AssertIssue(task with { TaskId = "no" }, ValidationCodes.ContractInvalid, "task_id");
        AssertIssue(task with { OriginNode = "pc-a" }, ValidationCodes.ContractInvalid, "origin_node");
        AssertIssue(task with { TargetNode = "P" }, ValidationCodes.ContractInvalid, "target_node");
        AssertIssue(task with { ParentTaskId = "no" }, ValidationCodes.ContractInvalid, "parent_task_id");
    }

    [Fact]
    public void Lineage_limits_are_rejected()
    {
        var task = TestTasks.Valid();

        AssertIssue(
            task with { Lineage = new Lineage { Hops = ["PC-A", "PC-B", "PC-C", "PC-D", "PC-E"], MaxDepth = 5 } },
            ValidationCodes.ContractInvalid,
            "lineage.hops");

        AssertIssue(
            task with { Lineage = new Lineage { Hops = [], MaxDepth = 4 } },
            ValidationCodes.ContractInvalid,
            "lineage");

        AssertIssue(
            task with { Lineage = new Lineage { Hops = ["PC-A", "PC-B"], MaxDepth = 1 } },
            ValidationCodes.LineageLoop,
            "lineage.hops");

        AssertIssue(
            task with { Lineage = new Lineage { Hops = ["PC-B"], MaxDepth = 1 } },
            ValidationCodes.LineageLoop,
            "lineage.hops");
    }

    [Fact]
    public void Workspace_names_and_paths_are_validated()
    {
        var task = TestTasks.Valid();

        AssertIssue(
            task with { Workspace = task.Workspace with { Repo = "bad/repo" } },
            ValidationCodes.ContractInvalid,
            "workspace.repo");

        AssertIssue(
            task with { Workspace = task.Workspace with { BaseRef = "-bad" } },
            ValidationCodes.ContractInvalid,
            "workspace.base_ref");

        AssertIssue(
            task with { Workspace = task.Workspace with { AllowedPaths = [] } },
            ValidationCodes.PathInvalid,
            "workspace.allowed_paths");

        AssertIssue(
            task with
            {
                Workspace = task.Workspace with
                {
                    AllowedPaths = Enumerable.Range(0, 65).Select(index => $"src/{index}").ToArray(),
                },
            },
            ValidationCodes.PathInvalid,
            "workspace.allowed_paths");

        AssertIssue(
            task with { Workspace = task.Workspace with { AllowedPaths = ["../x"] } },
            ValidationCodes.PathInvalid,
            "workspace.allowed_paths[0]");

        AssertIssue(
            task with { Workspace = task.Workspace with { DeniedPaths = ["../x"] } },
            ValidationCodes.PathInvalid,
            "workspace.denied_paths[0]");
    }

    public static IEnumerable<object[]> InvalidLimits()
    {
        yield return [new TaskLimits { MaxMinutes = 0 }, "limits.max_minutes"];
        yield return [new TaskLimits { MaxMinutes = 241 }, "limits.max_minutes"];
        yield return [new TaskLimits { MaxMemoryGb = 0 }, "limits.max_memory_gb"];
        yield return [new TaskLimits { MaxMemoryGb = 1025 }, "limits.max_memory_gb"];
        yield return [new TaskLimits { MaxProcesses = 0 }, "limits.max_processes"];
        yield return [new TaskLimits { MaxProcesses = 513 }, "limits.max_processes"];
        yield return [new TaskLimits { MaxCostUsd = -1m }, "limits.max_cost_usd"];
        yield return [new TaskLimits { MaxCostUsd = 101m }, "limits.max_cost_usd"];
    }

    [Theory]
    [MemberData(nameof(InvalidLimits))]
    public void Limits_are_validated(TaskLimits limits, string field)
        => AssertIssue(TestTasks.Valid() with { Limits = limits }, ValidationCodes.LimitsInvalid, field);

    [Fact]
    public void Instructions_are_validated()
    {
        var task = TestTasks.Valid();

        AssertIssue(task with { Instructions = null! }, ValidationCodes.ContractInvalid, "instructions");
        AssertIssue(task with { Instructions = "a\0b" }, ValidationCodes.ContractInvalid, "instructions");
        AssertIssue(
            task with { Instructions = new string('x', 65_537) },
            ValidationCodes.ContractInvalid,
            "instructions");
        AssertIssue(task with { Instructions = "a\uD800" }, ValidationCodes.ContractInvalid, "instructions");
        AssertIssue(task with { Instructions = "   " }, ValidationCodes.ContractInvalid, "instructions");
    }

    [Fact]
    public void Time_window_is_validated()
    {
        var task = TestTasks.Valid();

        AssertIssue(task with { CreatedAt = default }, ValidationCodes.ContractInvalid, "expires_at");
        AssertIssue(task with { ExpiresAt = default }, ValidationCodes.ContractInvalid, "expires_at");
        AssertIssue(
            task with { ExpiresAt = task.CreatedAt },
            ValidationCodes.ContractInvalid,
            "expires_at");
        AssertIssue(
            task with { ExpiresAt = task.CreatedAt.AddHours(25) },
            ValidationCodes.ContractInvalid,
            "expires_at");
    }

    [Fact]
    public void Executor_type_and_hint_are_validated()
    {
        var task = TestTasks.Valid();

        AssertIssue(
            task with { Executor = new ExecutorSpec { Type = "otro" } },
            ValidationCodes.ContractInvalid,
            "executor.type");

        AssertIssue(
            task with { Executor = new ExecutorSpec { ModelHint = new string('x', 129) } },
            ValidationCodes.ContractInvalid,
            "executor.model_hint");
    }

    [Fact]
    public void Approval_evidence_is_validated()
    {
        var task = TestTasks.Valid();
        var approval = TestTasks.ApprovalFor(task);

        AssertIssue(
            task with { Approval = approval with { ApprovalId = "" } },
            ValidationCodes.ApprovalInvalid,
            "approval");
        AssertIssue(
            task with { Approval = approval with { Nonce = "" } },
            ValidationCodes.ApprovalInvalid,
            "approval");
        AssertIssue(
            task with { Approval = approval with { Signature = "" } },
            ValidationCodes.ApprovalInvalid,
            "approval");
        AssertIssue(
            task with { Approval = approval with { ApprovedAt = default } },
            ValidationCodes.ApprovalInvalid,
            "approval");

        AssertIssue(
            task with { Approval = approval with { ApprovedBy = "agent" } },
            ValidationCodes.ApprovalInvalid,
            "approval.approved_by");
        AssertIssue(
            task with { Approval = approval with { Nonce = "short" } },
            ValidationCodes.ApprovalInvalid,
            "approval.nonce");
        AssertIssue(
            task with { Approval = approval with { Signature = "bad" } },
            ValidationCodes.ApprovalInvalid,
            "approval.signature");
        AssertIssue(
            task with { Approval = approval with { TaskHash = "bad" } },
            ValidationCodes.ApprovalInvalid,
            "approval.task_hash");
        AssertIssue(
            task with { Approval = approval with { TaskHash = "sha256:" + new string('a', 64) } },
            ValidationCodes.ApprovalInvalid,
            "approval.task_hash");
    }

    [Fact]
    public void Valid_approval_passes()
    {
        var task = TestTasks.Valid();
        var withApproval = task with { Approval = TestTasks.ApprovalFor(task) };

        Assert.True(TaskValidator.Validate(withApproval).IsValid);
    }
}
