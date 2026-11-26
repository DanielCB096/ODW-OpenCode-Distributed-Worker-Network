using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;
using Odw.Mcp;
using Odw.Mcp.Abstractions;
using Odw.Mcp.Tools;
using Odw.Approvals;
using Odw.Node.Approvals;
using Odw.Node.Artifacts;
using Odw.Node.Origins;
using Odw.Node.PeerApi;
using Odw.Node.Peers;
using Odw.Node.Persistence;
using Odw.Node.Persistence.Outbox;
using Odw.Node.Persistence.Peers;
using Odw.Node.Persistence.Queries;
using Odw.Node.Persistence.Tasks;
using Odw.Node.Scheduling;
using Odw.Node.Tasks;
using Odw.Policy;
using Odw.Sandbox.Network;

namespace Odw.Node.Hosting;

/// <summary>
/// Host del nodo: dos listeners con pipelines separados (pares y MCP), SQLite, recuperación
/// al arranque y coordinador de tareas. Valida los binds antes de abrir nada.
/// </summary>
public sealed class NodeHost : IAsyncDisposable
{
    private readonly SqliteDatabase _database;
    private readonly WebApplication _mcp;
    private readonly WebApplication _peer;
    private readonly ExecutionScheduler? _scheduler;
    private readonly OriginPoller? _poller;
    private readonly ApprovalPipeServer? _pipe;
    private readonly TelegramApprovalService? _telegram;
    private readonly ModelProxy? _modelProxy;
    private bool _disposed;

    private NodeHost(
        NodeHostOptions options,
        SqliteDatabase database,
        TaskStore tasks,
        TaskCoordinator coordinator,
        PeerRegistry peers,
        WebApplication mcp,
        WebApplication peer,
        Uri mcpAddress,
        Uri peerAddress,
        ExecutionScheduler? scheduler,
        OriginPoller? poller,
        ITaskProposalService proposalService,
        ApprovalService approvals,
        ApprovalCoordinator approvalCoordinator,
        ApprovalPipeServer? pipe,
        TelegramApprovalService? telegram)
    {
        Options = options;
        _database = database;
        Tasks = tasks;
        Coordinator = coordinator;
        Peers = peers;
        _mcp = mcp;
        _peer = peer;
        McpBaseAddress = mcpAddress;
        PeerBaseAddress = peerAddress;
        _scheduler = scheduler;
        _poller = poller;
        ProposalService = proposalService;
        Approvals = approvals;
        ApprovalCoordinator = approvalCoordinator;
        _pipe = pipe;
        _telegram = telegram;
        _modelProxy = options.ModelProxy;
    }

    public NodeHostOptions Options { get; }

    public TaskStore Tasks { get; }

    public TaskCoordinator Coordinator { get; }

    public PeerRegistry Peers { get; }

    public Uri McpBaseAddress { get; }

    public Uri PeerBaseAddress { get; }

    public ExecutionScheduler? Scheduler => _scheduler;

    public OriginPoller? Poller => _poller;

    public ITaskProposalService ProposalService { get; }

    public ApprovalService Approvals { get; }

    public ApprovalCoordinator ApprovalCoordinator { get; }

    public static async Task<NodeHost> StartAsync(
        NodeHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var database = SqliteDatabase.Open(options.DatabasePath, new SqliteDatabaseOptions
        {
            Pooling = options.Pooling,
            TimeProvider = options.TimeProvider,
        });

        WebApplication? mcp = null;
        WebApplication? peer = null;
        ApprovalPipeServer? pipe = null;
        TelegramApprovalService? telegram = null;

        try
        {
            await new StartupRecovery(database, options.TimeProvider).RunAsync(cancellationToken)
                .ConfigureAwait(false);

            var tasks = new TaskStore(database, options.TimeProvider);
            var coordinator = new TaskCoordinator(tasks, options.TimeProvider);
            var registry = new PeerRegistry(options.Peers);
            var policy = options.Policy
                         ?? new PolicyEngine(new Odw.Policy.PolicyDocument(1, Odw.Core.Contracts.PolicyEffect.Ask, []));
            var peerStore = new PeerStore(database, options.TimeProvider);
            await peerStore.SyncAsync(options.Peers, cancellationToken).ConfigureAwait(false);

            var clients = new PeerApiClientFactory(options.NodeId);
            var approvals = new ApprovalService(database, options.TimeProvider, options.ApprovalSigner);
            var proposals = new OriginProposalService(
                options, tasks, coordinator, registry, policy, clients, options.TimeProvider,
                approvals, options.ApprovalOptions);
            var approvalCoordinator = new ApprovalCoordinator(
                approvals, tasks, coordinator, proposals, options.TimeProvider);

            if (options.EnableApprovals)
            {
                pipe = new ApprovalPipeServer(
                    approvals,
                    approvalCoordinator,
                    new ApprovalPipeOptions
                    {
                        Name = options.ApprovalPipeName,
                        SecurityDescriptor = options.ApprovalPipeSecurityDescriptor,
                    });
            }

            TelegramApprovalService? telegramService = null;
            if (options.TelegramApprovals is { } telegramOptions)
            {
                telegramService = new TelegramApprovalService(
                    approvals,
                    approvalCoordinator,
                    new TelegramClient(telegramOptions.BotToken),
                    telegramOptions);
                telegram = telegramService;
            }

            var poller = new OriginPoller(
                tasks,
                coordinator,
                registry,
                clients,
                options.TimeProvider,
                peerStore,
                options.OriginPollerOptions);

            var runtime = new NodeRuntime
            {
                Options = options,
                Database = database,
                Tasks = tasks,
                Coordinator = coordinator,
                Outbox = new OutboxStore(database, options.TimeProvider),
                Peers = registry,
                Policy = policy,
                Queries = new SqliteTaskQuery(tasks),
                Directory = new SqlitePeerDirectory(database, options.TimeProvider),
                Proposals = proposals,
                Artifacts = new PeerArtifactFetcher(tasks, registry, clients),
            };

            mcp = BuildMcp(runtime);
            peer = BuildPeer(runtime, out var peerAddress);

            await mcp.StartAsync(cancellationToken).ConfigureAwait(false);
            await peer.StartAsync(cancellationToken).ConfigureAwait(false);

            if (options.EnableOriginPoller)
            {
                poller.Start();
            }

            if (options.EnableApprovals)
            {
                pipe!.Start();
            }

            if (options.TelegramApprovals is not null)
            {
                telegram?.Start();
            }

            ExecutionScheduler? scheduler = null;
            if (options.EnableScheduler)
            {
                scheduler = new ExecutionScheduler(
                    tasks,
                    coordinator,
                    options.WorkspacePreparer!,
                    options.ExecutionRunner!,
                    options.TimeProvider,
                    options.SchedulerOptions);
                scheduler.Start();
            }

            return new NodeHost(
                options,
                database,
                tasks,
                coordinator,
                registry,
                mcp,
                peer,
                AddressOf(mcp),
                peerAddress ?? AddressOf(peer),
                scheduler,
                poller,
                proposals,
                approvals,
                approvalCoordinator,
                options.EnableApprovals ? pipe : null,
                telegram);
        }
        catch
        {
            if (pipe is not null)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
            }

            if (telegram is not null)
            {
                await telegram.DisposeAsync().ConfigureAwait(false);
            }

            if (peer is not null)
            {
                await SafeStopAsync(peer).ConfigureAwait(false);
            }

            if (mcp is not null)
            {
                await SafeStopAsync(mcp).ConfigureAwait(false);
            }

            if (options.ModelProxy is not null)
            {
                await options.ModelProxy.DisposeAsync().ConfigureAwait(false);
            }

            await database.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_pipe is not null)
        {
            await _pipe.DisposeAsync().ConfigureAwait(false);
        }

        if (_telegram is not null)
        {
            await _telegram.DisposeAsync().ConfigureAwait(false);
        }

        if (_poller is not null)
        {
            await _poller.DisposeAsync().ConfigureAwait(false);
        }

        if (_scheduler is not null)
        {
            await _scheduler.DisposeAsync().ConfigureAwait(false);
        }

        if (_modelProxy is not null)
        {
            await _modelProxy.DisposeAsync().ConfigureAwait(false);
        }

        await SafeStopAsync(_peer).ConfigureAwait(false);
        await SafeStopAsync(_mcp).ConfigureAwait(false);
        await _database.DisposeAsync().ConfigureAwait(false);
    }

    private static WebApplication BuildMcp(NodeRuntime runtime)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls(Url(runtime.Options.McpBind, runtime.Options.McpPort, "http"));

        var app = builder.Build();
        var server = new McpServer(
            PeerTools.Create(runtime.Directory)
                .Concat(TaskTools.Create(runtime.Queries, runtime.Artifacts))
                .Concat(OriginTools.Create(runtime.Proposals)));

        app.MapOdwMcp(server, new McpOptions { Token = runtime.Options.McpToken });
        return app;
    }

    private static WebApplication BuildPeer(NodeRuntime runtime, out Uri? knownAddress)
    {
        var builder = WebApplication.CreateSlimBuilder();
        var certificate = runtime.Options.PeerCertificate;
        knownAddress = null;

        if (certificate is null)
        {
            builder.WebHost.UseUrls(Url(runtime.Options.PeerBind, runtime.Options.PeerPort, "http"));
        }
        else
        {
            var address = ResolveBindAddress(runtime.Options.PeerBind);
            var port = runtime.Options.PeerPort == 0 ? FindFreePort(address) : runtime.Options.PeerPort;

            builder.WebHost.ConfigureKestrel(options =>
                options.Listen(address, port, listen => listen.UseHttps(certificate)));

            var host = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"[{address}]"
                : address.ToString();
            knownAddress = new Uri($"https://{host}:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;

            if (path.Equals("/peer/v1/health", StringComparison.OrdinalIgnoreCase))
            {
                var remote = context.Connection.RemoteIpAddress;
                if (remote is null || !System.Net.IPAddress.IsLoopback(remote))
                {
                    context.Response.StatusCode = 404;
                    return;
                }

                await next().ConfigureAwait(false);
                return;
            }

            var identity = runtime.Peers.Authenticate(
                context.Request.Headers.Authorization.ToString(),
                context.Request.Headers["X-Odw-Node"].ToString());

            if (identity is null)
            {
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return;
            }

            context.Items[PeerEndpoints.PeerItem] = identity;
            await next().ConfigureAwait(false);
        });

        PeerEndpoints.Map(app, runtime);
        return app;
    }

    private static Uri AddressOf(WebApplication app)
    {
        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        return new Uri(address);
    }

    private static string Url(string bind, int port, string scheme)
    {
        var host = bind.Contains(':', StringComparison.Ordinal) && !bind.StartsWith('[')
            ? $"[{bind}]"
            : bind;

        return $"{scheme}://{host}:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    private static IPAddress ResolveBindAddress(string bind)
    {
        if (IPAddress.TryParse(bind, out var address))
        {
            return address;
        }

        var addresses = Dns.GetHostAddresses(bind);
        return addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
               ?? addresses.First();
    }

    private static int FindFreePort(IPAddress address)
    {
        var listener = new TcpListener(address, 0);
        listener.Start();

        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task SafeStopAsync(WebApplication app)
    {
        try
        {
            await app.StopAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        await app.DisposeAsync().ConfigureAwait(false);
    }
}

internal sealed class NodeRuntime
{
    public required NodeHostOptions Options { get; init; }

    public required SqliteDatabase Database { get; init; }

    public required TaskStore Tasks { get; init; }

    public required TaskCoordinator Coordinator { get; init; }

    public required OutboxStore Outbox { get; init; }

    public required PeerRegistry Peers { get; init; }

    public required PolicyEngine Policy { get; init; }

    public required SqliteTaskQuery Queries { get; init; }

    public required SqlitePeerDirectory Directory { get; init; }

    public required ITaskProposalService Proposals { get; init; }

    public required IArtifactFetcher Artifacts { get; init; }
}
