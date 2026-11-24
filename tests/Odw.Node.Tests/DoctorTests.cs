using System.Security.Cryptography;
using Odw.Node.Diagnostics;
using Xunit;

namespace Odw.Node.Tests;

public class DoctorTests
{
    private static DoctorOptions Options(TestDatabase fixture) => new()
    {
        DatabasePath = fixture.Path,
        PeerBind = "tailscale",
        PeerPort = 47421,
        McpBind = "127.0.0.1",
        McpPort = 47422,
        McpToken = Convert.ToBase64String(new byte[32]),
    };

    [Fact]
    public async Task Healthy_node_passes()
    {
        await using var fixture = TestDatabase.Create();

        var report = await NodeDoctor.RunAsync(Options(fixture));

        Assert.True(report.Ok);
        Assert.Equal(0, report.Failures);
        Assert.Contains(report.Checks, check =>
            check.Name == "database.schema" && check.Status == DoctorStatus.Pass);
        Assert.Contains(report.Checks, check =>
            check.Name == "database.audit" && check.Status == DoctorStatus.Pass);
        Assert.Contains(report.Checks, check =>
            check.Name == "bind.peer" && check.Status == DoctorStatus.Pass);
        Assert.Contains(report.Checks, check =>
            check.Name == "bind.mcp" && check.Status == DoctorStatus.Pass);
        Assert.Contains(report.Checks, check =>
            check.Name == "listener.peer" && check.Status == DoctorStatus.Warn);
    }

    [Fact]
    public async Task Missing_database_fails()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "odw-tests", Guid.NewGuid().ToString("N"), "missing.db");

        var report = await NodeDoctor.RunAsync(new DoctorOptions
        {
            DatabasePath = path,
            PeerPort = 47421,
            McpPort = 47422,
            McpToken = Convert.ToBase64String(new byte[32]),
        });

        Assert.False(report.Ok);
        Assert.Contains(report.Checks, check =>
            check.Name == "database.schema" && check.Status == DoctorStatus.Fail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-base64")]
    public async Task Invalid_token_fails(string token)
    {
        await using var fixture = TestDatabase.Create();
        var options = Options(fixture) with { McpToken = token };

        var report = await NodeDoctor.RunAsync(options);

        Assert.False(report.Ok);
        Assert.Contains(report.Checks, check =>
            check.Name == "token.mcp" && check.Status == DoctorStatus.Fail);
    }

    [Fact]
    public async Task Short_token_fails()
    {
        await using var fixture = TestDatabase.Create();
        var options = Options(fixture) with { McpToken = Convert.ToBase64String(new byte[8]) };

        var report = await NodeDoctor.RunAsync(options);

        Assert.Contains(report.Checks, check =>
            check.Name == "token.mcp" && check.Status == DoctorStatus.Fail);
    }

    [Fact]
    public async Task Wildcard_peer_bind_fails()
    {
        await using var fixture = TestDatabase.Create();
        var options = Options(fixture) with { PeerBind = "0.0.0.0" };

        var report = await NodeDoctor.RunAsync(options);

        Assert.False(report.Ok);
        Assert.Contains(report.Checks, check =>
            check.Name == "bind.peer" && check.Status == DoctorStatus.Fail);
    }

    [Fact]
    public async Task Non_loopback_mcp_bind_fails()
    {
        await using var fixture = TestDatabase.Create();
        var options = Options(fixture) with { McpBind = "100.64.0.10" };

        var report = await NodeDoctor.RunAsync(options);

        Assert.False(report.Ok);
        Assert.Contains(report.Checks, check =>
            check.Name == "bind.mcp" && check.Status == DoctorStatus.Fail);
    }

    [Fact]
    public async Task Spec_hash_is_verified()
    {
        await using var fixture = TestDatabase.Create();
        var specPath = System.IO.Path.Combine(fixture.Folder, "spec.json");
        await System.IO.File.WriteAllTextAsync(specPath, "{\"openapi\":\"3.1.0\"}");

        var hash = Convert.ToHexStringLower(SHA256.HashData(await System.IO.File.ReadAllBytesAsync(specPath)));

        var matching = await NodeDoctor.RunAsync(Options(fixture) with
        {
            SpecPath = specPath,
            ExpectedSpecSha256 = hash,
        });

        Assert.Contains(matching.Checks, check =>
            check.Name == "opencode.spec" && check.Status == DoctorStatus.Pass);

        var mismatching = await NodeDoctor.RunAsync(Options(fixture) with
        {
            SpecPath = specPath,
            ExpectedSpecSha256 = new string('0', 64),
        });

        Assert.Contains(mismatching.Checks, check =>
            check.Name == "opencode.spec" && check.Status == DoctorStatus.Fail);
    }

    [Theory]
    [InlineData("2.3.2", true, "2.3.2")]
    [InlineData("opencode v2.3.2\n", true, "2.3.2")]
    [InlineData("sin versión", false, "")]
    public void Version_parser_reads_semver(string output, bool expected, string version)
    {
        Assert.Equal(expected, OpenCodeVersion.TryParse(output, out var parsed));
        if (expected)
        {
            Assert.Equal(version, parsed);
        }
    }

    [Theory]
    [InlineData("tailscale", true)]
    [InlineData("100.100.1.2", true)]
    [InlineData("pc-laptop.tailnet.ts.net", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("0.0.0.0", false)]
    public void Tailnet_bind_policy(string bind, bool expected)
        => Assert.Equal(expected, BindPolicy.IsTailnetBind(bind));

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("localhost", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("100.64.0.10", false)]
    public void Loopback_bind_policy(string bind, bool expected)
        => Assert.Equal(expected, BindPolicy.IsLoopbackBind(bind));
}
