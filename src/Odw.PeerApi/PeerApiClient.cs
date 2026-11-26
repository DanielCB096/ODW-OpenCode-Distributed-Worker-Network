using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Odw.PeerApi;

public sealed record PeerConnection(Uri BaseAddress, string NodeId, string Token);

public sealed record PeerTaskView(
    string TaskId,
    string State,
    long StateSeq,
    bool ApprovalRequired,
    string NextAction,
    bool ResultAvailable,
    bool CancelRequested);

public sealed record PeerPostResult(
    HttpStatusCode StatusCode,
    PeerTaskView? View,
    string? ErrorCode,
    string? Detail,
    bool Replay)
{
    public bool Accepted => StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK;
}

public sealed record PeerResultView(string TaskId, bool Available, string? ResultJson);

public sealed record PeerArtifactView(string Name, string ContentType, byte[] Bytes);

/// <summary>Cliente de la API de pares v1 (lado origen).</summary>
public sealed class PeerApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly PeerConnection _connection;
    private readonly bool _ownsHttp;

    public PeerApiClient(PeerConnection connection, HttpClient? httpClient = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _ownsHttp = httpClient is null;
    }

    public Uri BaseAddress => _connection.BaseAddress;

    public async Task<PeerPostResult> PostTaskAsync(string body, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        using var request = Create(HttpMethod.Post, "/peer/v1/tasks");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new PeerPostResult(
                response.StatusCode,
                ParseView(text),
                null,
                null,
                response.Headers.Contains("Idempotent-Replay"));
        }

        var (code, detail) = ParseError(text);
        return new PeerPostResult(response.StatusCode, null, code, detail, false);
    }

    public async Task<PeerTaskView?> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Get, $"/peer/v1/tasks/{Uri.EscapeDataString(taskId)}");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return ParseView(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<PeerResultView?> GetResultAsync(string taskId, CancellationToken cancellationToken)
    {
        using var request = Create(HttpMethod.Get, $"/peer/v1/tasks/{Uri.EscapeDataString(taskId)}/result");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var id = root.TryGetProperty("task_id", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : taskId;
        var available = root.TryGetProperty("result_available", out var flag)
                        && flag.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && flag.GetBoolean();

        var resultJson = available
                         && root.TryGetProperty("result", out var result)
                         && result.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? result.GetRawText()
            : null;

        return new PeerResultView(id, available, resultJson);
    }

    public async Task<PeerArtifactView?> GetArtifactAsync(
        string taskId,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using var request = Create(
            HttpMethod.Get,
            $"/peer/v1/tasks/{Uri.EscapeDataString(taskId)}/artifacts/{Uri.EscapeDataString(name)}");
        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";

        return new PeerArtifactView(name, contentType, bytes);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = Create(HttpMethod.Get, "/peer/v1/hello");
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(_connection.BaseAddress, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _connection.Token);
        request.Headers.Add("X-Odw-Node", _connection.NodeId);
        return request;
    }

    private static PeerTaskView ParseView(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        return new PeerTaskView(
            String(root, "task_id") ?? throw new PeerProtocolException("Falta task_id."),
            String(root, "state") ?? throw new PeerProtocolException("Falta state."),
            root.TryGetProperty("state_seq", out var seq) && seq.TryGetInt64(out var value) ? value : 0,
            root.TryGetProperty("approval_required", out var approval)
            && approval.ValueKind is JsonValueKind.True or JsonValueKind.False
            && approval.GetBoolean(),
            String(root, "next_action") ?? "none",
            root.TryGetProperty("result_available", out var result)
            && result.ValueKind is JsonValueKind.True or JsonValueKind.False
            && result.GetBoolean(),
            root.TryGetProperty("cancel_requested", out var cancel)
            && cancel.ValueKind is JsonValueKind.True or JsonValueKind.False
            && cancel.GetBoolean());
    }

    private static (string Code, string Detail) ParseError(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return (String(root, "code") ?? "INTERNAL", String(root, "detail") ?? text);
        }
        catch (JsonException)
        {
            return ("INTERNAL", text);
        }
    }

    private static string? String(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

public sealed class PeerProtocolException(string message) : Exception(message);
