using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Odw.Node.Configuration;
using Odw.Node.Hosting;

namespace Odw.Cli;

public sealed record ServeOptions(string ConfigPath);

/// <summary>Servicio hospedado: arranca el nodo al iniciar y lo detiene al parar.</summary>
public sealed class NodeHostedService(ServeOptions options) : IHostedService
{
    private NodeHost? _host;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var settings = NodeConfigLoader.Load(options.ConfigPath);
        _host = await NodeBootstrap.StartAsync(settings, cancellationToken);

        Console.WriteLine($"odw {settings.Config.NodeId} iniciado");
        Console.WriteLine($"  pares:    {_host.PeerBaseAddress}");
        Console.WriteLine($"  mcp:      {_host.McpBaseAddress}");
        Console.WriteLine($"  scheduler:{(settings.Config.Scheduler.Enabled ? " on" : " off")}");
        Console.WriteLine($"  approvals:{(settings.Config.Approvals.Enabled ? $" pipe {settings.Config.Approvals.PipeName}" : " off")}");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
            _host = null;
        }
    }
}

public static class ServeHost
{
    public static async Task RunAsync(string configPath, string serviceName = "OdwNode")
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Services.AddWindowsService(options => options.ServiceName = serviceName);
        builder.Services.AddSingleton(new ServeOptions(Path.GetFullPath(configPath)));
        builder.Services.AddHostedService<NodeHostedService>();

        await builder.Build().RunAsync();
    }
}
