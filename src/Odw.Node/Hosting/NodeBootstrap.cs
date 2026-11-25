using Odw.Core.Contracts;
using Odw.Core.Model;
using Odw.Executors.OpenCode;
using Odw.Node.Approvals;
using Odw.Node.Configuration;
using Odw.Node.Hosting;
using Odw.Node.Origins;
using Odw.Node.Scheduling;
using Odw.Policy;
using Odw.Sandbox.Network;
using Odw.Sandbox.Windows;
using Odw.Sandbox.Workspaces;
using System.Globalization;
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

        SandboxGuard.Validate(config.Sandbox, config.Scheduler, config.Workspaces);

        var policy = BuildPolicy(settings);
        var capabilities = config.Capabilities.Count > 0 ? config.Capabilities : ["opencode"];
        var accepts = config.Accepts.Count > 0 ? config.Accepts : ["analysis", "test_run"];
        var certificate = LoadCertificate(settings);

        WorkspaceManager? workspaces = null;
        OpenCodeTaskRunner? runner = null;

        RunAsCredentials? runAs = null;
        var runtimeRoot = Environment.ExpandEnvironmentVariables(config.Sandbox.RuntimeRoot);
        var agentConfigTemplate = Environment.ExpandEnvironmentVariables(config.Sandbox.AgentConfigPath);
        var networkStatePath = Environment.ExpandEnvironmentVariables(config.Sandbox.NetworkStatePath);

        if (config.Scheduler.Enabled && config.Sandbox.RunAsAgent)
        {
            var secretPath = Environment.ExpandEnvironmentVariables(config.Sandbox.AgentSecretPath);
            var credentials = AgentCredentialsStore.Load(config.Sandbox.AgentAccount, secretPath);
            runAs = new RunAsCredentials(credentials.Account, credentials.Password);

            if (!File.Exists(agentConfigTemplate))
            {
                throw new NodeConfigurationException(
                    $"Falta la plantilla del agente en {agentConfigTemplate}. "
                    + "Ejecuta tools/provision-sandbox.ps1.");
            }

            AgentRuntime.Cleanup(runtimeRoot);
        }

        ModelProxy? modelProxy = null;
        if (config.Scheduler.Enabled && config.Sandbox.NetworkIsolation)
        {
            if (!config.Sandbox.RunAsAgent)
            {
                throw new NodeConfigurationException(
                    "sandbox.network_isolation requiere sandbox.run_as_agent.");
            }

            if (!OperatingSystem.IsWindows())
            {
                throw new NodeConfigurationException(
                    "sandbox.network_isolation solo está disponible en Windows (WFP).");
            }

            modelProxy = ModelProxy.Start(new ModelProxyOptions
            {
                Port = config.Sandbox.NetworkProxyPort,
                AllowedHosts = config.Sandbox.NetworkAllowedHosts,
            });

            try
            {
                if (!WfpNetworkPolicy.IsInstalled(networkStatePath))
                {
                    throw new NodeConfigurationException(
                        "sandbox.network_isolation requiere los filtros WFP. Ejecuta "
                        + "'odw sandbox net install --agent " + config.Sandbox.AgentAccount
                        + " --port " + modelProxy.Port.ToString(CultureInfo.InvariantCulture)
                        + " --state " + networkStatePath
                        + "' con una consola elevada.");
                }
            }
            catch
            {
                await modelProxy.DisposeAsync().ConfigureAwait(false);
                modelProxy = null;
                throw;
            }
        }

        if (config.Scheduler.Enabled)
        {
            workspaces = new WorkspaceManager(new WorkspaceOptions
            {
                WorkspaceRoot = settings.WorkspaceRoot,
                Repos = settings.Repos,
                GitExecutable = config.Workspaces.Git,
                KeepWorkspaces = config.Workspaces.Keep,
                ApplyAcls = config.Sandbox.ApplyAcls,
                AgentAccount = config.Sandbox.AgentAccount,
                ServiceAccount = OperatingSystem.IsWindows()
                    ? System.Security.Principal.WindowsIdentity.GetCurrent().Name
                    : null,
            });

            runner = new OpenCodeTaskRunner(new OpenCodeTaskRunnerOptions
            {
                Executable = config.OpenCode.Executable!,
                PinnedVersion = config.OpenCode.PinnedVersion,
                UseJobObject = config.OpenCode.UseJobObject,
                RunAs = runAs,
                ModelProxyUrl = modelProxy?.Url,
                RequireNetworkIsolation = config.Sandbox.NetworkIsolation,
                NetworkIsolationProbe = config.Sandbox.NetworkIsolation
                    ? () => WfpNetworkPolicy.IsInstalled(networkStatePath)
                    : null,
                AgentRuntimeRoot = config.Sandbox.RunAsAgent ? runtimeRoot : null,
                AgentConfigTemplate = agentConfigTemplate,
                AgentAccount = config.Sandbox.AgentAccount,
                ServiceAccount = OperatingSystem.IsWindows()
                    ? System.Security.Principal.WindowsIdentity.GetCurrent().Name
                    : null,
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
            ModelProxy = modelProxy,
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
            ApprovalSigner = settings.ApprovalHmacKey is null
                ? null
                : new ApprovalSigner(settings.ApprovalHmacKey),
            TelegramApprovals = settings.Telegram is null
                ? null
                : new TelegramApprovalOptions
                {
                    BotToken = settings.Telegram.Token,
                    AllowedUserIds = settings.Telegram.AllowedUserIds,
                    AllowedChatIds = settings.Telegram.AllowedChatIds,
                    PollTimeoutSeconds = settings.Telegram.PollSeconds,
                },
        };

        try
        {
            return await NodeHost.StartAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (modelProxy is not null)
            {
                await modelProxy.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
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
