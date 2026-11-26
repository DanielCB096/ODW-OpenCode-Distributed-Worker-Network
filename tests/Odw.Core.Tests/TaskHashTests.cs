using Odw.Core.Hashing;
using Odw.Core.Model;
using Xunit;

namespace Odw.Core.Tests;

public class TaskHashTests
{
    [Fact]
    public void Vector_V1_is_stable()
    {
        Assert.Equal(
            "sha256:1811ced43c7c77ec46a771902cdd039b5c8985bfdd70997f92696f3f6d4721d0",
            TaskHash.Compute(TestTasks.Valid()));
    }

    [Fact]
    public void Vector_V2_changed_instruction_changes_hash()
    {
        var request = TestTasks.Valid() with
        {
            Instructions = "Analizar la arquitectura y ejecutar las pruebas.\nNo modificar archivos ni ejecutar scripts.",
        };

        Assert.Equal(
            "sha256:f6424f1c4e6e5d0767d6da32d0750f9a4ecd2a0d3cc550ae79657c74a2823b7c",
            TaskHash.Compute(request));
    }

    [Fact]
    public void Vector_V4_changed_target_changes_hash()
    {
        var request = TestTasks.Valid() with { TargetNode = "PC-C" };

        Assert.Equal(
            "sha256:663a3edee18349352d72d32e95a811d8feba941f1168f3bcb3cd24771d5e404b",
            TaskHash.Compute(request));
    }

    [Fact]
    public void Paths_are_sorted_and_deduplicated()
    {
        var request = TestTasks.Valid() with
        {
            Workspace = TestTasks.Valid().Workspace with
            {
                AllowedPaths = ["tests/", "src/", "src", "tests"],
            },
        };

        Assert.Equal(TaskHash.Compute(TestTasks.Valid()), TaskHash.Compute(request));
    }

    [Fact]
    public void Instructions_are_normalized()
    {
        var request = TestTasks.Valid() with
        {
            Instructions = "  Analizar la arquitectura y ejecutar las pruebas.   \r\nNo modificar archivos.\t\r\n",
        };

        Assert.Equal(TaskHash.Compute(TestTasks.Valid()), TaskHash.Compute(request));
    }

    [Fact]
    public void Model_profile_is_security_relevant()
    {
        var request = TestTasks.Valid() with
        {
            Executor = new ExecutorSpec { Type = "opencode", ModelProfile = "paid-deepseek" },
        };

        Assert.NotEqual(TaskHash.Compute(TestTasks.Valid()), TaskHash.Compute(request));
    }

    [Fact]
    public void Approval_metadata_is_excluded()
    {
        var request = TestTasks.Valid();
        var withApproval = request with { Approval = TestTasks.ApprovalFor(request) };

        Assert.Equal(TaskHash.Compute(request), TaskHash.Compute(withApproval));
    }

    [Fact]
    public void Volatile_and_descriptive_fields_are_excluded()
    {
        var request = TestTasks.Valid();
        var changed = request with
        {
            ProtocolVersion = 7,
            CreatedAt = request.CreatedAt.AddMinutes(5),
            ExpiresAt = request.ExpiresAt.AddMinutes(5),
            Lineage = new Lineage { Hops = ["PC-X"], MaxDepth = request.Lineage.MaxDepth },
            Executor = new ExecutorSpec { Type = "opencode", ModelHint = "otro/modelo" },
            Workspace = request.Workspace with { Strategy = WorkspaceStrategy.Worktree },
        };

        Assert.Equal(TaskHash.Compute(request), TaskHash.Compute(changed));
    }

    [Fact]
    public void Security_relevant_fields_are_included()
    {
        var request = TestTasks.Valid();

        var different = new[]
        {
            request with { TargetNode = "PC-C" },
            request with { ParentTaskId = "01JAZ6AAAAAAAAAAAAAAAAAAAAA" },
            request with { Kind = TaskKind.TestRun },
            request with { Limits = request.Limits with { Network = NetworkAccess.Allow } },
            request with { Workspace = request.Workspace with { AllowedPaths = ["src/"] } },
            request with { Workspace = request.Workspace with { Repo = "Otro" } },
            request with { Lineage = request.Lineage with { MaxDepth = 0 } },
        };

        var baseline = TaskHash.Compute(request);
        foreach (var variant in different)
        {
            Assert.NotEqual(baseline, TaskHash.Compute(variant));
        }
    }

    [Fact]
    public void Canonical_json_has_sorted_keys_and_no_whitespace()
    {
        var canonical = TaskHash.BuildCanonicalJson(TestTasks.Valid());

        Assert.StartsWith("{\"executor\":{\"type\":\"opencode\"},\"instructions\":", canonical);
        Assert.Contains("\"allowed_paths\":[\"src\",\"tests\"]", canonical);
        Assert.DoesNotContain(": ", canonical);
        Assert.DoesNotContain(", ", canonical);
        Assert.DoesNotContain("\n", canonical);
    }

    [Fact]
    public void Task_hash_format_is_lowercase_sha256()
    {
        var hash = TaskHash.Compute(TestTasks.Valid());

        Assert.Matches("^sha256:[0-9a-f]{64}$", hash);
    }
}
