using System.Diagnostics;
using System.Security.Principal;
using Odw.Core.Contracts;
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

    [Fact]
    public async Task OpenCode_completes_a_prompt_as_the_agent()
    {
        if (!OperatingSystem.IsWindows()
            || Environment.GetEnvironmentVariable("ODW_AGENT_E2E") != "1")
        {
            return;
        }

        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var credentials = AgentCredentialsStore.Load(
            "odw-agent",
            Path.Combine(common, "ODW", "agent.secret"));
        var workspace = Path.Combine(common, "ODW", "workspaces", "agent-prompt-test");

        Directory.CreateDirectory(workspace);
        GrantAgent(workspace);

        var model = Environment.GetEnvironmentVariable("ODW_AGENT_MODEL")
                    ?? "opencode/deepseek-v4.1-flash-free";

        try
        {
            await using var server = await OpenCodeServer.StartAsync(new OpenCodeServerOptions
            {
                Executable = Path.Combine(common, "ODW", "opencode", "opencode.exe"),
                WorkingDirectory = workspace,
                PinnedVersion = "2.3.2",
                StartupTimeout = TimeSpan.FromSeconds(60),
                RunAs = new RunAsCredentials(credentials.Account, credentials.Password),
            });

            using var client = new OpenCodeClient(server.Connection);
            var executor = new OpenCodeExecutor(client);

            var session = await executor.StartAsync(new ExecutorStartRequest(
                "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
                workspace,
                "Responde exactamente: OK. No uses herramientas.",
                model,
                new ExecutorLimits(5, 12, 64, false, 2m),
                ToolPolicy.AskAll,
                new Dictionary<string, string>()), CancellationToken.None);

            var events = new List<ExecutorEvent>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            var pump = Task.Run(async () =>
            {
                await foreach (var item in executor.StreamAsync(session.SessionId, timeout.Token))
                {
                    events.Add(item);
                }
            }, timeout.Token);

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            await executor.SendAsync(
                session.SessionId,
                "Responde exactamente: OK. No uses herramientas.",
                Odw.Core.Model.MessageDelivery.Queue,
                timeout.Token);

            await pump;

            var terminal = events.LastOrDefault(item => item.Kind is
                ExecutorEventKind.Completed or ExecutorEventKind.Failed or ExecutorEventKind.Interrupted);

            Assert.True(
                terminal?.Kind == ExecutorEventKind.Completed,
                $"La ejecución no terminó bien: {terminal?.Kind} {terminal?.Detail}");

            var messages = await client.GetMessagesAsync(session.SessionId, CancellationToken.None);
            Assert.Contains(messages, message => message.Text.Contains("OK", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void Agent_cannot_read_node_data()
    {
        if (!OperatingSystem.IsWindows()
            || Environment.GetEnvironmentVariable("ODW_AGENT_E2E") != "1")
        {
            return;
        }

        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var credentials = AgentCredentialsStore.Load(
            "odw-agent",
            Path.Combine(common, "ODW", "agent.secret"));

        var probe = Path.Combine(common, "ODW", $"acl-probe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "odw-acl-probe");

        try
        {
            Assert.NotEqual(0, RunAsAgent(credentials, "/c", "type", probe));
            Assert.NotEqual(0, RunAsAgent(
                credentials,
                "/c",
                "type",
                Path.Combine(common, "ODW", "agent.secret")));

            var profile = Path.Combine(
                Directory.GetParent(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!.FullName,
                credentials.Account);
            var agentConfig = Path.Combine(profile, ".config", "opencode", "opencode.json");

            Assert.Equal(0, RunAsAgent(credentials, "/c", "type", agentConfig));
        }
        finally
        {
            File.Delete(probe);
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int RunAsAgent(AgentCredentials credentials, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UserName = credentials.Account,
            PasswordInClearText = credentials.Password,
            Domain = ".",
            LoadUserProfile = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode;
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
