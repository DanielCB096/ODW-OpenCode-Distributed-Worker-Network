using Odw.Core.Contracts;
using Odw.Core.Model;
using Xunit;

namespace Odw.Executors.OpenCode.Tests;

/// <summary>
/// Prueba real contra un OpenCode instalado. Se activa con ODW_OPENCODE_EXE apuntando al
/// ejecutable (o shim .cmd). Sin la variable, no hace nada: el CI no depende de OpenCode.
/// </summary>
public class OpenCodeIntegrationTests
{
    [Fact]
    public async Task Real_server_completes_a_read_only_prompt()
    {
        var executable = Environment.GetEnvironmentVariable("ODW_OPENCODE_EXE");
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        var folder = Path.Combine(Path.GetTempPath(), "odw-opencode-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            await using var server = await OpenCodeServer.StartAsync(new OpenCodeServerOptions
            {
                Executable = executable,
                WorkingDirectory = folder,
                PinnedVersion = "2.3.2",
                StartupTimeout = TimeSpan.FromSeconds(40),
            });

            using var client = new OpenCodeClient(server.Connection);
            var executor = new OpenCodeExecutor(client);

            var session = await executor.StartAsync(new ExecutorStartRequest(
                "01JAZ5Z8Q4XK3V9C2M1N7P6R8T",
                folder,
                "Responde exactamente: OK. No uses herramientas.",
                TestModels.FreeOrFallback,
                new ExecutorLimits(5, 4, 16, false, 1m),
                ToolPolicy.AskAll,
                new Dictionary<string, string>()), CancellationToken.None);

            var events = new List<ExecutorEvent>();
            using var pumpCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(120));

            var pump = Task.Run(async () =>
            {
                await foreach (var item in executor.StreamAsync(session.SessionId, pumpCancellation.Token))
                {
                    events.Add(item);
                }
            });

            await Task.Delay(TimeSpan.FromMilliseconds(500));

            await executor.SendAsync(
                session.SessionId,
                "Responde exactamente: OK. No uses herramientas.",
                MessageDelivery.Queue,
                pumpCancellation.Token);

            await pump;

            Assert.Contains(events, item => item.Kind == ExecutorEventKind.Completed);

            var messages = await client.GetMessagesAsync(session.SessionId, CancellationToken.None);
            Assert.Contains(messages, message => message.Text.Contains("OK", StringComparison.OrdinalIgnoreCase));

            var info = await client.GetInfoAsync(CancellationToken.None);
            Assert.Equal("2.3.2", info.Version);
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
