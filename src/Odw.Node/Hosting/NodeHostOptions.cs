using Odw.Core.Primitives;
using Odw.Node.Approvals;
using Odw.Node.Diagnostics;
using Odw.Node.Origins;
using Odw.Node.Peers;
using Odw.Node.Scheduling;
using Odw.Policy;
using Odw.Sandbox.Workspaces;

namespace Odw.Node.Hosting;

public sealed class NodeConfigurationException(string message) : Exception(message);

public sealed record NodeHostOptions
{
    public required string NodeId { get; init; }

    public required string DatabasePath { get; init; }

    public required string McpToken { get; init; }

    /// <summary>IP tailnet (o hostname) a la que se enlaza la API de pares. No acepta "tailscale".</summary>
    public string PeerBind { get; init; } = "127.0.0.1";

    public int PeerPort { get; init; } = 7421;

    public string McpBind { get; init; } = "127.0.0.1";

    public int McpPort { get; init; } = 7422;

    /// <summary>Solo para desarrollo/pruebas: permite enlazar pares en loopback.</summary>
    public bool AllowLoopbackPeerBind { get; init; }

    public IReadOnlyList<PeerRegistration> Peers { get; init; } = [];

    public IReadOnlyList<string> Capabilities { get; init; } = ["opencode"];

    public IReadOnlyList<string> Accepts { get; init; } = ["analysis", "test_run"];

    public PolicyEngine? Policy { get; init; }

    public bool EnableScheduler { get; init; }

    public ITaskExecutionRunner? ExecutionRunner { get; init; }

    public IWorkspacePreparer? WorkspacePreparer { get; init; }

    /// <summary>Proxy de modelo para el agente; el host lo libera al parar.</summary>
    public Odw.Sandbox.Network.ModelProxy? ModelProxy { get; init; }

    public ExecutionSchedulerOptions? SchedulerOptions { get; init; }

    /// <summary>Certificado para la API de pares; si está presente, el listener usa HTTPS.</summary>
    public System.Security.Cryptography.X509Certificates.X509Certificate2? PeerCertificate { get; init; }

    public bool EnableOriginPoller { get; init; } = true;

    public OriginPollerOptions? OriginPollerOptions { get; init; }

    public bool EnableApprovals { get; init; } = true;

    public string ApprovalPipeName { get; init; } = "odw-approvals";

    public string? ApprovalPipeSecurityDescriptor { get; init; } = "D:(A;;GA;;;BA)(A;;GA;;;SY)";

    public ApprovalOptions? ApprovalOptions { get; init; }

    public ApprovalSigner? ApprovalSigner { get; init; }

    public TelegramApprovalOptions? TelegramApprovals { get; init; }

    public string? OpenCodeVersion { get; init; } = "2.3.2";

    public bool Pooling { get; init; } = true;

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    public void Validate()
    {
        if (!ContractFormat.IsValidNodeId(NodeId))
        {
            throw new NodeConfigurationException($"node_id inválido: {NodeId}.");
        }

        if (string.IsNullOrWhiteSpace(McpToken))
        {
            throw new NodeConfigurationException("Falta el token de la fachada MCP.");
        }

        try
        {
            if (Convert.FromBase64String(McpToken).Length < 32)
            {
                throw new NodeConfigurationException("El token de la fachada MCP debe tener al menos 32 bytes.");
            }
        }
        catch (FormatException)
        {
            throw new NodeConfigurationException("El token de la fachada MCP no es base64 válido.");
        }

        if (!BindPolicy.IsLoopbackBind(McpBind))
        {
            throw new NodeConfigurationException($"La fachada MCP debe enlazar a loopback, no a {McpBind}.");
        }

        if (BindPolicy.IsAnyAddress(PeerBind))
        {
            throw new NodeConfigurationException($"La API de pares no puede enlazar a comodín: {PeerBind}.");
        }

        if (!BindPolicy.IsTailnetBind(PeerBind)
            && !(AllowLoopbackPeerBind && BindPolicy.IsLoopbackBind(PeerBind)))
        {
            throw new NodeConfigurationException(
                $"La API de pares debe enlazar a una IP tailnet, no a {PeerBind}. "
                + "Para pruebas, usa AllowLoopbackPeerBind.");
        }

        if (PeerPort is < 0 or > 65535 || McpPort is < 0 or > 65535)
        {
            throw new NodeConfigurationException("Puerto fuera de rango.");
        }

        if (EnableScheduler && (ExecutionRunner is null || WorkspacePreparer is null))
        {
            throw new NodeConfigurationException(
                "EnableScheduler requiere ExecutionRunner y WorkspacePreparer.");
        }
    }
}
