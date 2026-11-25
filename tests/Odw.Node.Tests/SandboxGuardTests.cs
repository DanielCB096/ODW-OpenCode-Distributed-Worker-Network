using Odw.Node.Configuration;
using Odw.Node.Hosting;
using Xunit;

namespace Odw.Node.Tests;

public class SandboxGuardTests
{
    [Fact]
    public void Rejects_concurrent_tasks_when_the_agent_is_shared()
    {
        var error = Assert.Throws<NodeConfigurationException>(() => SandboxGuard.Validate(
            new SandboxConfig { RunAsAgent = true },
            new SchedulerConfig { Enabled = true, MaxConcurrent = 2 },
            new WorkspacesConfig()));

        Assert.Contains("max_concurrent", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_kept_workspaces_when_the_agent_is_shared()
    {
        var error = Assert.Throws<NodeConfigurationException>(() => SandboxGuard.Validate(
            new SandboxConfig { RunAsAgent = true },
            new SchedulerConfig { Enabled = true, MaxConcurrent = 1 },
            new WorkspacesConfig { Keep = true }));

        Assert.Contains("keep", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Accepts_the_safe_single_task_configuration()
        => SandboxGuard.Validate(
            new SandboxConfig { RunAsAgent = true },
            new SchedulerConfig { Enabled = true, MaxConcurrent = 1 },
            new WorkspacesConfig());

    [Fact]
    public void Does_not_restrict_configurations_without_the_restricted_agent()
        => SandboxGuard.Validate(
            new SandboxConfig { RunAsAgent = false },
            new SchedulerConfig { Enabled = true, MaxConcurrent = 4 },
            new WorkspacesConfig { Keep = true });
}
