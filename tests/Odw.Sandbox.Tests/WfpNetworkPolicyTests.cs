using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Odw.Sandbox.Windows;
using Xunit;

namespace Odw.Sandbox.Tests;

public class WfpNetworkPolicyTests
{
    [Fact]
    public async Task Agent_is_blocked_from_internet_but_can_reach_loopback()
    {
        if (!OperatingSystem.IsWindows()
            || Environment.GetEnvironmentVariable("ODW_WFP_E2E") != "1")
        {
            return;
        }

        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var credentials = AgentCredentialsStore.Load(
            "odw-agent",
            Path.Combine(common, "ODW", "agent.secret"));

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var other = new TcpListener(IPAddress.Loopback, 0);
        other.Start();
        var otherPort = ((IPEndPoint)other.LocalEndpoint).Port;

        var server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var request = new byte[1024];
            var read = await stream.ReadAsync(request);
            if (read == 0)
            {
                return;
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes(
                "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK"));
        });

        try
        {
            WfpNetworkPolicy.Install("odw-agent", port);
            Assert.True(WfpNetworkPolicy.IsInstalled());

            Assert.Equal(0, RunAsAgent(credentials, "/c", "curl.exe", "-sS", "-m", "5",
                $"http://127.0.0.1:{port}/"));

            Assert.NotEqual(0, RunAsAgent(credentials, "/c", "curl.exe", "-sS", "-m", "5",
                $"http://127.0.0.1:{otherPort}/"));

            Assert.NotEqual(0, RunAsAgent(credentials, "/c", "curl.exe", "-sS", "-m", "8",
                "https://example.com/"));

            Assert.NotEqual(0, RunAsAgent(credentials, "/c", "curl.exe", "-sS", "-m", "3",
                "http://169.254.169.254/latest/meta-data/"));

            Assert.NotEqual(0, RunAsAgent(credentials, "/c", "curl.exe", "-sS", "-m", "3",
                "http://192.168.0.1/"));

            await server.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            WfpNetworkPolicy.Uninstall();
            listener.Stop();
            other.Stop();
        }

        Assert.False(WfpNetworkPolicy.IsInstalled());
    }

    private static int RunAsAgent(AgentCredentials credentials, params string[] arguments)
    {
        if (!OperatingSystem.IsWindows())
        {
            return -1;
        }

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
}
