using System.Text.Json.Serialization;

namespace Odw.Node.Configuration;

public sealed record NodeConfig
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = string.Empty;

    [JsonPropertyName("data_dir")]
    public string DataDirectory { get; init; } = @"%PROGRAMDATA%\ODW";

    [JsonPropertyName("database_path")]
    public string? DatabasePath { get; init; }

    [JsonPropertyName("peer_api")]
    public PeerApiConfig PeerApi { get; init; } = new();

    [JsonPropertyName("mcp")]
    public McpConfig Mcp { get; init; } = new();

    [JsonPropertyName("opencode")]
    public OpenCodeConfig OpenCode { get; init; } = new();

    [JsonPropertyName("workspaces")]
    public WorkspacesConfig Workspaces { get; init; } = new();

    [JsonPropertyName("repos")]
    public List<RepoConfig> Repos { get; init; } = [];

    [JsonPropertyName("peers")]
    public List<ConfiguredPeer> Peers { get; init; } = [];

    [JsonPropertyName("peers_file")]
    public string? PeersFile { get; init; }

    [JsonPropertyName("policy")]
    public string? PolicyPath { get; init; }

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; init; } = [];

    [JsonPropertyName("accepts")]
    public List<string> Accepts { get; init; } = [];

    [JsonPropertyName("scheduler")]
    public SchedulerConfig Scheduler { get; init; } = new();

    [JsonPropertyName("poller")]
    public PollerConfig Poller { get; init; } = new();

    [JsonPropertyName("approvals")]
    public ApprovalsConfig Approvals { get; init; } = new();
}

public sealed record PeerApiConfig
{
    [JsonPropertyName("bind")]
    public string Bind { get; init; } = "tailscale";

    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; } = 7421;

    [JsonPropertyName("allow_loopback")]
    public bool AllowLoopback { get; init; }

    [JsonPropertyName("tls")]
    public PeerTlsConfig Tls { get; init; } = new();
}

public sealed record PeerTlsConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("certificate_path")]
    public string? CertificatePath { get; init; }

    [JsonPropertyName("password_ref")]
    public string? PasswordRef { get; init; }
}

public sealed record McpConfig
{
    [JsonPropertyName("bind")]
    public string Bind { get; init; } = "127.0.0.1";

    [JsonPropertyName("port")]
    public int Port { get; init; } = 7422;

    [JsonPropertyName("token_ref")]
    public string? TokenRef { get; init; }
}

public sealed record OpenCodeConfig
{
    [JsonPropertyName("executable")]
    public string? Executable { get; init; }

    [JsonPropertyName("pinned_version")]
    public string? PinnedVersion { get; init; } = "2.3.2";

    [JsonPropertyName("use_job_object")]
    public bool UseJobObject { get; init; } = true;
}

public sealed record WorkspacesConfig
{
    [JsonPropertyName("root")]
    public string Root { get; init; } = @"%PROGRAMDATA%\ODW\workspaces";

    [JsonPropertyName("keep")]
    public bool Keep { get; init; }

    [JsonPropertyName("git")]
    public string Git { get; init; } = "git";
}

public sealed record RepoConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("base_ref")]
    public string BaseRef { get; init; } = "main";
}

public sealed record ConfiguredPeer
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; init; } = string.Empty;

    [JsonPropertyName("address")]
    public string? Address { get; init; }

    [JsonPropertyName("token_ref")]
    public string? TokenRef { get; init; }

    [JsonPropertyName("trust")]
    public string Trust { get; init; } = "trusted";

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; init; } = [];
}

public sealed record SchedulerConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("max_concurrent")]
    public int MaxConcurrent { get; init; } = 1;

    [JsonPropertyName("poll_seconds")]
    public int PollSeconds { get; init; } = 2;
}

public sealed record PollerConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("interval_seconds")]
    public int IntervalSeconds { get; init; } = 5;
}

public sealed record ApprovalsConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("pipe_name")]
    public string PipeName { get; init; } = "odw-approvals";

    [JsonPropertyName("ttl_minutes")]
    public int TtlMinutes { get; init; } = 15;

    [JsonPropertyName("hmac_key_ref")]
    public string? HmacKeyRef { get; init; }

    [JsonPropertyName("security_descriptor")]
    public string? SecurityDescriptor { get; init; } = "D:(A;;GA;;;BA)(A;;GA;;;SY)";

    [JsonPropertyName("telegram")]
    public TelegramConfig Telegram { get; init; } = new();
}

public sealed record TelegramConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("bot_token_ref")]
    public string? BotTokenRef { get; init; }

    [JsonPropertyName("allowed_user_ids")]
    public List<long> AllowedUserIds { get; init; } = [];

    [JsonPropertyName("allowed_chat_ids")]
    public List<long> AllowedChatIds { get; init; } = [];

    [JsonPropertyName("poll_seconds")]
    public int PollSeconds { get; init; } = 20;
}
