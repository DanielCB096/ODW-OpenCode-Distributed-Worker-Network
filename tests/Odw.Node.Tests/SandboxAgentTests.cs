using System.Diagnostics;
using System.Security.Principal;
using Odw.Executors.OpenCode;
using Odw.Sandbox.Windows;
using Xunit;

namespace Odw.Node.Tests;

/// <summary>
/// Integración opcional: arranca `opencode serve` con el token del agente restringido.
/// Requiere ODW_AGENT_E2E=1, el usuario provisionado y el binario compartido.
/// </summary>
public class SandboxAgentTests
{
    [Fact]
    public async Task OpenCode_starts_as_the_agent()
    {
        if (!OperatingSystem.IsWindows()
            || Environment.GetEnvironmentVariable("ODW_AGENT_E2E") != "1")
        {
            return;
        }

        var secret = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ODW",
            "agent.secret");
        var executable = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ODW",
            "opencode",
            "opencode.exe");

        var credentials = AgentCredentialsStore.Load("odw-agent", secret);
        var workspace = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ODW",
            "workspaces",
            "agent-cs-test");

        Directory.CreateDirectory(workspace);
        GrantAgent(workspace);

        try
        {
            await using var server = await OpenCodeServer.StartAsync(new OpenCodeServerOptions
            {
                Executable = executable,
                WorkingDirectory = workspace,
                PinnedVersion = "2.3.2",
                StartupTimeout = TimeSpan.FromSeconds(60),
                RunAs = new RunAsCredentials(credentials.Account, credentials.Password),
            });

            Assert.Equal("2.3.2", server.Info.Version);
            Assert.True(server.Info.Pid > 0);
        }
        finally
        {
            try
            {
                Directory.Delete(workspace, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void GrantAgent(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var service = WindowsIdentity.GetCurrent().Name;

        Run("icacls", path, "/inheritance:r");
        Run("icacls", path, "/grant:r", $"{service}:(OI)(CI)F",
            "/grant:r", "odw-agent:(OI)(CI)M", "/T");
    }

    private static void Run(string executable, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{(arguments.Length > 0 ? arguments[0] : executable)} falló: {error}");
        }
    }
}
