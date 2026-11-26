using Odw.Node.Configuration;
using Odw.Node.Hosting;
using Xunit;

namespace Odw.Node.Tests;

public class NodeConfigTests
{
    private static string Folder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "odw-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Path.Combine(folder, "repo"));
        return folder;
    }

    private static string Json(
        string mcpEnv,
        string peerEnv,
        string? peersFile = null,
        string policy = "null",
        string? models = null)
        => $$"""
            {
              "node_id": "PC-A",
              "data_dir": "data",
              "peer_api": { "bind": "127.0.0.1", "port": 0, "allow_loopback": true },
              "mcp": { "bind": "127.0.0.1", "port": 0, "token_ref": "env:{{mcpEnv}}" },
              "opencode": { "executable": "opencode", "pinned_version": "2.3.2" },
              {{(models is null ? string.Empty : models + ",")}}
              "workspaces": { "root": "workspaces" },
              "repos": [ { "name": "SistemaMaestro", "path": "repo", "base_ref": "main" } ],
              "peers": [
                { "node_id": "PC-B", "address": "127.0.0.1:7421",
                  "token_ref": "env:{{peerEnv}}", "trust": "trusted",
                  "capabilities": ["git"] }
              ],
              {{(peersFile is null ? string.Empty : $"\"peers_file\": \"{peersFile}\",")}}
              "policy": {{policy}},
              "scheduler": { "enabled": false },
              "poller": { "enabled": false },
              "approvals": { "enabled": false }
            }
            """;

    private static string Write(string folder, string content)
    {
        var path = Path.Combine(folder, "node.json");
        File.WriteAllText(path, content);
        return path;
    }

    private static string Models(string defaultProfile = "free", string allowed = "\"free\"")
        => $$"""
            "models": {
              "default": "{{defaultProfile}}",
              "allowed": [{{allowed}}],
              "profiles": {
                "free": {
                  "provider": "opencode",
                  "model": "deepseek-v4.1-flash-free",
                  "max_cost_usd": 0
                },
                "paid-deepseek": {
                  "provider": "opencode-go",
                  "model": "deepseek-v4.1-flash",
                  "max_cost_usd": 2
                }
              }
            }
            """;

    [Fact]
    public void Loads_and_resolves_references()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            var settings = NodeConfigLoader.Load(Write(folder, Json(mcpEnv, peerEnv)));

            Assert.Equal("PC-A", settings.Config.NodeId);
            Assert.Equal("127.0.0.1", settings.PeerBindAddress);
            Assert.True(settings.Config.PeerApi.AllowLoopback);
            Assert.EndsWith("odw.db", settings.DatabasePath);
            Assert.Contains("SistemaMaestro", settings.Repos.Keys);
            var peer = Assert.Single(settings.Peers);
            Assert.Equal("PC-B", peer.NodeId);
            Assert.Equal("peer-token", peer.Token);
            Assert.Equal("git", Assert.Single(peer.Capabilities!));
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }

    [Fact]
    public void Missing_secret_fails()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, null);

        try
        {
            var exception = Assert.Throws<NodeConfigurationException>(
                () => NodeConfigLoader.Load(Write(folder, Json(mcpEnv, peerEnv))));

            Assert.Contains(peerEnv, exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
        }
    }

    [Fact]
    public void Literal_secret_is_rejected()
    {
        var folder = Folder();
        var content = Json("ODW_TEST_MCP", "ODW_TEST_PEER")
            .Replace("\"env:ODW_TEST_MCP\"", "\"texto-en-claro\"");

        var exception = Assert.Throws<NodeConfigurationException>(
            () => NodeConfigLoader.Load(Write(folder, content)));

        Assert.Contains("env:NAME", exception.Message);
    }

    [Fact]
    public void Peers_file_is_loaded()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            File.WriteAllText(
                Path.Combine(folder, "peers.json"),
                $$"""
                {
                  "peers": [
                    { "node_id": "PC-C", "address": "127.0.0.1:7422",
                      "token_ref": "env:{{peerEnv}}", "trust": "limited" }
                  ]
                }
                """);

            var content = Json(mcpEnv, peerEnv, peersFile: "peers.json");
            var settings = NodeConfigLoader.Load(Write(folder, content));

            Assert.Equal(2, settings.Peers.Count);
            Assert.Contains(settings.Peers, peer => peer.NodeId == "PC-C" && peer.Trust == "limited");
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }

    [Fact]
    public void Missing_repo_path_fails()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            var content = Json(mcpEnv, peerEnv).Replace("\"path\": \"repo\"", "\"path\": \"no-existe\"");
            var exception = Assert.Throws<NodeConfigurationException>(
                () => NodeConfigLoader.Load(Write(folder, content)));

            Assert.Contains("no-existe", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }

    [Fact]
    public void Models_section_exposes_only_allowed_profiles()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            var settings = NodeConfigLoader.Load(Write(folder, Json(mcpEnv, peerEnv, models: Models())));

            Assert.Equal("free", settings.DefaultModelProfile);
            Assert.True(settings.ModelProfiles.ContainsKey("free"));
            Assert.False(settings.ModelProfiles.ContainsKey("paid-deepseek"));
            Assert.Equal(
                "opencode/deepseek-v4.1-flash-free",
                settings.ModelProfiles["free"].QualifiedName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }

    [Fact]
    public void Models_default_must_be_allowed()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            var content = Json(mcpEnv, peerEnv, models: Models(defaultProfile: "paid-deepseek"));
            var exception = Assert.Throws<NodeConfigurationException>(
                () => NodeConfigLoader.Load(Write(folder, content)));

            Assert.Contains("models.allowed debe incluir models.default", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }

    [Fact]
    public void Models_allowed_must_reference_a_known_profile()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            var content = Json(mcpEnv, peerEnv, models: Models(allowed: "\"free\", \"nope\""));
            var exception = Assert.Throws<NodeConfigurationException>(
                () => NodeConfigLoader.Load(Write(folder, content)));

            Assert.Contains("perfil desconocido", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }

    [Fact]
    public async Task Bootstrap_starts_a_host_from_config()
    {
        var folder = Folder();
        var mcpEnv = "ODW_TEST_MCP_" + Guid.NewGuid().ToString("N");
        var peerEnv = "ODW_TEST_PEER_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(mcpEnv, Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable(peerEnv, "peer-token");

        try
        {
            var settings = NodeConfigLoader.Load(Write(folder, Json(mcpEnv, peerEnv)));
            await using var host = await NodeBootstrap.StartAsync(settings);

            Assert.NotEqual(0, host.PeerBaseAddress.Port);
            Assert.NotEqual(0, host.McpBaseAddress.Port);
            Assert.True(host.PeerBaseAddress.IsLoopback);
        }
        finally
        {
            Environment.SetEnvironmentVariable(mcpEnv, null);
            Environment.SetEnvironmentVariable(peerEnv, null);
        }
    }
}
