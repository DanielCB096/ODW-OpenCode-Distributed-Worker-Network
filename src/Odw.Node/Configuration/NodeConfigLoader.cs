using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Odw.Core.Primitives;
using Odw.Node.Hosting;
using Odw.Node.Peers;

namespace Odw.Node.Configuration;

public sealed record NodeSettings(
    NodeConfig Config,
    string ConfigPath,
    string DatabasePath,
    string PeerBindAddress,
    string McpToken,
    IReadOnlyList<PeerRegistration> Peers,
    IReadOnlyDictionary<string, string> Repos,
    string? PolicyPath,
    string WorkspaceRoot,
    string? TlsCertificatePath,
    string? TlsCertificatePassword);

/// <summary>
/// Carga `node.json`, resuelve rutas y secretos por referencia (`env:`/`file:`) y valida lo
/// que necesita el host. Un secreto literal o DPAPI (aún no implementado) falla.
/// </summary>
public static class NodeConfigLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static NodeSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new NodeConfigurationException($"No existe la configuración: {fullPath}.");
        }

        var directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        NodeConfig? config;

        try
        {
            config = JsonSerializer.Deserialize<NodeConfig>(File.ReadAllText(fullPath), Options);
        }
        catch (JsonException exception)
        {
            throw new NodeConfigurationException($"node.json inválido: {exception.Message}");
        }

        if (config is null)
        {
            throw new NodeConfigurationException("node.json vacío.");
        }

        if (!ContractFormat.IsValidNodeId(config.NodeId))
        {
            throw new NodeConfigurationException($"node_id inválido: {config.NodeId}.");
        }

        ValidatePort(config.PeerApi.Port, "peer_api.port");
        ValidatePort(config.Mcp.Port, "mcp.port");

        var dataDirectory = Expand(config.DataDirectory, directory);
        var databasePath = config.DatabasePath is null
            ? Path.Combine(dataDirectory, "odw.db")
            : Expand(config.DatabasePath, directory);

        if (config.Mcp.TokenRef is null)
        {
            throw new NodeConfigurationException("Falta mcp.token_ref.");
        }

        var mcpToken = ResolveSecret(config.Mcp.TokenRef, directory, "mcp.token_ref");

        var peers = LoadPeers(config, directory);
        var repos = LoadRepos(config, directory);
        var workspaceRoot = Expand(config.Workspaces.Root, directory);
        var policyPath = config.PolicyPath is null ? null : Expand(config.PolicyPath, directory);

        if (policyPath is not null && !File.Exists(policyPath))
        {
            throw new NodeConfigurationException($"No existe la política: {policyPath}.");
        }

        if (config.Scheduler.Enabled)
        {
            if (string.IsNullOrWhiteSpace(config.OpenCode.Executable))
            {
                throw new NodeConfigurationException("El scheduler requiere opencode.executable.");
            }

            if (config.Scheduler.MaxConcurrent < 1)
            {
                throw new NodeConfigurationException("scheduler.max_concurrent debe ser >= 1.");
            }
        }

        foreach (var repo in config.Repos)
        {
            if (!Directory.Exists(Expand(repo.Path, directory)))
            {
                throw new NodeConfigurationException($"No existe el repo '{repo.Name}': {repo.Path}.");
            }
        }

        var peerBind = ResolvePeerBind(config.PeerApi, directory);

        string? tlsCertificate = null;
        string? tlsPassword = null;

        if (config.PeerApi.Tls.Enabled)
        {
            if (string.IsNullOrWhiteSpace(config.PeerApi.Tls.CertificatePath))
            {
                throw new NodeConfigurationException("peer_api.tls requiere certificate_path.");
            }

            tlsCertificate = Expand(config.PeerApi.Tls.CertificatePath, directory);
            if (!File.Exists(tlsCertificate))
            {
                throw new NodeConfigurationException($"No existe el certificado: {tlsCertificate}.");
            }

            if (config.PeerApi.Tls.PasswordRef is not null)
            {
                tlsPassword = ResolveSecret(config.PeerApi.Tls.PasswordRef, directory, "peer_api.tls.password_ref");
            }
        }

        return new NodeSettings(
            config,
            fullPath,
            databasePath,
            peerBind,
            mcpToken,
            peers,
            repos,
            policyPath,
            workspaceRoot,
            tlsCertificate,
            tlsPassword);
    }

    private static IReadOnlyList<PeerRegistration> LoadPeers(NodeConfig config, string directory)
    {
        var configured = new List<ConfiguredPeer>(config.Peers);

        if (config.PeersFile is not null)
        {
            var path = Expand(config.PeersFile, directory);
            if (!File.Exists(path))
            {
                throw new NodeConfigurationException($"No existe peers_file: {path}.");
            }

            PeerFile? file;
            try
            {
                file = JsonSerializer.Deserialize<PeerFile>(File.ReadAllText(path), Options);
            }
            catch (JsonException exception)
            {
                throw new NodeConfigurationException($"peers_file inválido: {exception.Message}");
            }

            if (file?.Peers is not null)
            {
                configured.AddRange(file.Peers);
            }
        }

        var peers = new List<PeerRegistration>(configured.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var peer in configured)
        {
            if (!ContractFormat.IsValidNodeId(peer.NodeId))
            {
                throw new NodeConfigurationException($"peer node_id inválido: {peer.NodeId}.");
            }

            if (!seen.Add(peer.NodeId))
            {
                throw new NodeConfigurationException($"peer duplicado: {peer.NodeId}.");
            }

            if (peer.TokenRef is null)
            {
                throw new NodeConfigurationException($"Falta token_ref para el peer {peer.NodeId}.");
            }

            var trust = peer.Trust.ToLowerInvariant();
            if (trust is not ("trusted" or "limited" or "blocked" or "unknown"))
            {
                throw new NodeConfigurationException($"trust inválido para {peer.NodeId}: {peer.Trust}.");
            }

            peers.Add(new PeerRegistration(
                peer.NodeId,
                ResolveSecret(peer.TokenRef, directory, $"peer {peer.NodeId}"),
                trust,
                peer.Capabilities,
                peer.Address));
        }

        return peers;
    }

    private static IReadOnlyDictionary<string, string> LoadRepos(NodeConfig config, string directory)
    {
        var repos = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var repo in config.Repos)
        {
            if (!ContractFormat.IsValidRepoName(repo.Name))
            {
                throw new NodeConfigurationException($"Nombre de repo inválido: {repo.Name}.");
            }

            if (!repos.TryAdd(repo.Name, Expand(repo.Path, directory)))
            {
                throw new NodeConfigurationException($"Repo duplicado: {repo.Name}.");
            }
        }

        return repos;
    }

    private static string ResolvePeerBind(PeerApiConfig config, string directory)
    {
        if (!string.IsNullOrWhiteSpace(config.Address))
        {
            return config.Address.Trim();
        }

        if (!string.Equals(config.Bind, "tailscale", StringComparison.OrdinalIgnoreCase))
        {
            return config.Bind.Trim();
        }

        return FindTailscaleAddress()
               ?? throw new NodeConfigurationException(
                   "No se encontró una dirección Tailscale. Indica peer_api.address "
                   + "o usa bind con una IP concreta.");
    }

    private static string? FindTailscaleAddress()
    {
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (!network.Name.Contains("tailscale", StringComparison.OrdinalIgnoreCase)
                && !network.Description.Contains("tailscale", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var address in network.GetIPProperties().UnicastAddresses)
            {
                var ip = address.Address;
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var bytes = ip.GetAddressBytes();
                    if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
                    {
                        return ip.ToString();
                    }
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    var bytes = ip.GetAddressBytes();
                    if (bytes.Length >= 6
                        && bytes[0] == 0xfd && bytes[1] == 0x7a && bytes[2] == 0x11
                        && bytes[3] == 0x5c && bytes[4] == 0xa1 && bytes[5] == 0xe0)
                    {
                        return ip.ToString();
                    }
                }
            }
        }

        return null;
    }

    private static string ResolveSecret(string reference, string directory, string what)
    {
        if (reference.StartsWith("env:", StringComparison.OrdinalIgnoreCase))
        {
            var name = reference[4..].Trim();
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(value))
            {
                throw new NodeConfigurationException($"{what}: la variable {name} está vacía o no existe.");
            }

            return value;
        }

        if (reference.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var path = Expand(reference[5..].Trim(), directory);
            if (!File.Exists(path))
            {
                throw new NodeConfigurationException($"{what}: no existe el archivo {path}.");
            }

            var value = File.ReadAllText(path).Trim();
            if (value.Length == 0)
            {
                throw new NodeConfigurationException($"{what}: el archivo {path} está vacío.");
            }

            return value;
        }

        throw new NodeConfigurationException(
            $"{what}: usa env:NAME o file:ruta. dpapi: todavía no está implementado.");
    }

    private static string Expand(string value, string directory)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NodeConfigurationException("Ruta vacía en node.json.");
        }

        var expanded = Environment.ExpandEnvironmentVariables(value.Trim());

        if (expanded.StartsWith("~", StringComparison.Ordinal))
        {
            expanded = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                expanded[1..].TrimStart('\\', '/'));
        }

        return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(directory, expanded));
    }

    private static void ValidatePort(int port, string name)
    {
        if (port is < 0 or > 65535)
        {
            throw new NodeConfigurationException($"{name} fuera de rango.");
        }
    }

    private sealed record PeerFile
    {
        [System.Text.Json.Serialization.JsonPropertyName("peers")]
        public List<ConfiguredPeer>? Peers { get; init; }
    }
}
