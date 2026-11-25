using Odw.Sandbox.Windows;
using Xunit;

namespace Odw.Sandbox.Tests;

public class SandboxInspectorTests
{
    [Fact]
    public void Report_has_consistent_platform_values()
    {
        var report = SandboxInspector.Inspect("odw-agent");

        Assert.Equal(OperatingSystem.IsWindows(), report.IsWindows);

        if (OperatingSystem.IsWindows())
        {
            Assert.True(report.AclsSupported);
            Assert.False(string.IsNullOrWhiteSpace(report.Detail));
        }
    }

    [Fact]
    public void Well_known_account_is_detected()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(SandboxInspector.AccountExists("S-1-5-32-546"));
        Assert.False(SandboxInspector.AccountExists("odw-agent-inexistente-xyz"));
    }
}
