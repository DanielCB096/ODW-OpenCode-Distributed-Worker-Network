using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Executors.OpenCode;
using Odw.Node.Approvals;
using Odw.Node.Configuration;
using Odw.Node.Hosting;
using Odw.Node.Origins;
using Odw.Node.Scheduling;
using Odw.Policy;
using Odw.Sandbox.Workspaces;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Odw.Node.Hosting;

/// <summary>Compone un nodo desde <c>node.json</c> y lo arranca.</summary>
public static class NodeBootstrap
{
    public static async Task<NodeHost> StartAsync(
        NodeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var config = settings.Config;

        var policy = BuildPolicy(settings);
        var capabilities = config.Capabilities.Count > 0 ? config.Capabilities : ["opencode"];
        var accepts = config.Accepts.Count > 0 ? config.Accepts : ["analysis", "test_run"];
        var certificate = LoadCertificate(settings);

        WorkspaceManager? workspaces = null;
        OpenCodeTaskRunner? runner = null;

        if (config.Scheduler.Enabled)
        {
            workspaces = new WorkspaceManager(new WorkspaceOptions
            {
                WorkspaceRoot = settings.WorkspaceRoot,
                Repos = settings.Repos,
                GitExecutable = config.Workspaces.Git,
                KeepWorkspaces = config.Workspaces.Keep,
            });

            runner = new OpenCodeTaskRunner(new OpenCodeTaskRunnerOptions
            {
                Executable = config.OpenCode.Executable!,
                PinnedVersion = config.OpenCode.PinnedVersion,
                UseJobObject = config.OpenCode.UseJobObject,
                PermissionHandlerFactory = request => BuildToolHandler(
                    policy, settings, request, capabilities),
            });
        }

        var options = new NodeHostOptions
        {
            NodeId = config.NodeId,
            DatabasePath = settings.DatabasePath,
            McpToken = settings.McpToken,
            PeerBind = settings.PeerBindAddress,
            PeerPort = config.PeerApi.Port,
            McpBind = config.Mcp.Bind,
            McpPort = config.Mcp.Port,
            AllowLoopbackPeerBind = config.PeerApi.AllowLoopback,
            Peers = settings.Peers,
            Capabilities = capabilities,
            Accepts = accepts,
            Policy = policy,
            EnableScheduler = config.Scheduler.Enabled,
            WorkspacePreparer = workspaces,
            ExecutionRunner = runner,
            SchedulerOptions = new ExecutionSchedulerOptions
            {
                MaxConcurrentTasks = config.Scheduler.MaxConcurrent,
                PollInterval = TimeSpan.FromSeconds(Math.Clamp(config.Scheduler.PollSeconds, 1, 3600)),
            },
            PeerCertificate = certificate,
            EnableOriginPoller = config.Poller.Enabled,
            OriginPollerOptions = new OriginPollerOptions
            {
                PollInterval = TimeSpan.FromSeconds(Math.Clamp(config.Poller.IntervalSeconds, 1, 3600)),
            },
            EnableApprovals = config.Approvals.Enabled,
            ApprovalPipeName = config.Approvals.PipeName,
            ApprovalPipeSecurityDescriptor = config.Approvals.SecurityDescriptor,
            ApprovalOptions = new ApprovalOptions
            {
                Ttl = TimeSpan.FromMinutes(Math.Clamp(config.Approvals.TtlMinutes, 1, 1440)),
            },
        };

        return await NodeHost.StartAsync(options, cancellationToken).ConfigureAwait(false);
    }

    private static X509Certificate2? LoadCertificate(NodeSettings settings)
    {
        if (settings.TlsCertificatePath is null)
        {
            return null;
        }

        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                settings.TlsCertificatePath,
                settings.TlsCertificatePassword,
                X509KeyStorageFlags.EphemeralKeySet);

            if (!certificate.HasPrivateKey)
            {
                throw new NodeConfigurationException("El certificado TLS no tiene clave privada.");
            }

            return certificate;
        }
        catch (CryptographicException exception)
        {
            throw new NodeConfigurationException($"Certificado TLS inválido: {exception.Message}");
        }
    }

    private static PolicyEngine BuildPolicy(NodeSettings settings)
    {
        if (settings.PolicyPath is null)
        {
            return new PolicyEngine(new PolicyDocument(1, PolicyEffect.Ask, []));
        }

        try
        {
            return PolicyEngine.FromFile(settings.PolicyPath);
        }
        catch (PolicyValidationException exception)
        {
            throw new NodeConfigurationException(
                $"policy.json inválido: {string.Join("; ", exception.Issues.Select(issue => $"{issue.Path}: {issue.Message}"))}");
        }
    }

    private static ToolPermissionHandler BuildToolHandler(
        PolicyEngine policy,
        NodeSettings settings,
        ExecutionRequest request,
        IReadOnlyList<string> capabilities)
    {
        var trust = settings.Peers
            .FirstOrDefault(peer => string.Equals(peer.NodeId, request.Contract.OriginNode, StringComparison.Ordinal))
            ?.Trust ?? "unknown";

        return (permission, cancellationToken) =>
        {
            var decision = policy.Evaluate(new PolicyRequest(
                TaskRole.Target,
                request.Contract.OriginNode,
                trust,
                request.Contract,
                capabilities,
                DateTimeOffset.UtcNow)
            {
                Actions = [permission.Action],
            });

            return Task.FromResult(
                decision.Effect == PolicyEffect.Auto
                    ? ToolPermissionDecision.AllowOnce
                    : ToolPermissionDecision.Reject);
        };
    }
}
