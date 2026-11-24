using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Odw.Executors.OpenCode;

/// <summary>
/// Cliente tipado de la API V2 de OpenCode (v2.3.2). Escrito a mano contra el spec fijado:
/// el esquema usa <c>contentMediaType</c> y SSE, que los generadores no manejan bien.
/// Solo acepta una base loopback: `opencode serve` nunca se expone (ADR-002).
/// </summary>
public sealed class OpenCodeClient : IOpenCodeClient, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;
    private readonly bool _ownsHttp;

    public OpenCodeClient(OpenCodeConnection connection, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!IsLoopback(connection.BaseAddress))
        {
            throw new ArgumentException(
                $"La base de OpenCode debe ser loopback, no {connection.BaseAddress}.",
                nameof(connection));
        }

        BaseAddress = connection.BaseAddress;
        _authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.Username}:{connection.Password}")));

        _http = httpClient ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _ownsHttp = httpClient is null;
    }

    public Uri BaseAddress { get; }

    public async Task<OpenCodeServerInfo> GetInfoAsync(CancellationToken cancellationToken)
    {
        var text = await SendAsync(HttpMethod.Get, "/api/info", null, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;

        return new OpenCodeServerInfo(
            OpenCodeJson.String(root, "version") ?? "desconocida",
            root.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var value) ? value : 0,
            OpenCodeJson.Strings(root, "urls"));
    }

    public async Task<OpenCodeSession> CreateSessionAsync(
        OpenCodeSessionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new JsonObject
        {
            ["location"] = new JsonObject { ["directory"] = request.Directory },
        };

        if (request.Title is not null)
        {
            body["title"] = request.Title;
        }

        if (request.Agent is not null)
        {
            body["agent"] = request.Agent;
        }

        if (request.ModelProvider is not null && request.ModelId is not null)
        {
            body["model"] = new JsonObject
            {
                ["providerID"] = request.ModelProvider,
                ["id"] = request.ModelId,
            };
        }

        if (request.Permissions is { Count: > 0 })
        {
            var permissions = new JsonArray();
            foreach (var rule in request.Permissions)
            {
                permissions.Add(new JsonObject
                {
                    ["action"] = rule.Action,
                    ["resource"] = rule.Resource,
                    ["effect"] = rule.EffectValue,
                });
            }

            body["permissions"] = permissions;
        }

        var text = await SendAsync(HttpMethod.Post, "/api/session", body, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text);
        return OpenCodeJson.ParseSession(document.RootElement);
    }

    public async Task SendPromptAsync(
        string sessionId,
        string text,
        string delivery,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(text);

        var body = new JsonObject
        {
            ["text"] = text,
            ["delivery"] = delivery,
        };

        await SendAsync(
            HttpMethod.Post,
            $"/api/session/{Uri.EscapeDataString(sessionId)}/prompt",
            body,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OpenCodeSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var text = await SendAsync(
            HttpMethod.Get,
            $"/api/session/{Uri.EscapeDataString(sessionId)}",
            null,
            cancellationToken,
            allowNotFound: true).ConfigureAwait(false);

        if (text.Length == 0)
        {
            return null;
        }

        using var document = JsonDocument.Parse(text);
        return OpenCodeJson.ParseSession(document.RootElement);
    }

    public async Task<IReadOnlyList<OpenCodeMessage>> GetMessagesAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var text = await SendAsync(
            HttpMethod.Get,
            $"/api/session/{Uri.EscapeDataString(sessionId)}/message",
            null,
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text);
        return OpenCodeJson.ParseMessages(document.RootElement);
    }

    public async Task<IReadOnlyList<OpenCodePermissionRequest>> GetPendingPermissionsAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var text = await SendAsync(
            HttpMethod.Get,
            $"/api/session/{Uri.EscapeDataString(sessionId)}/permission",
            null,
            cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(text);
        return OpenCodeJson.ParsePermissions(document.RootElement);
    }

    public async Task ReplyPermissionAsync(
        string sessionId,
        string permissionId,
        OpenCodePermissionReply reply,
        string? message,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionId);

        var body = new JsonObject
        {
            ["decision"] = reply switch
            {
                OpenCodePermissionReply.Once => "once",
                OpenCodePermissionReply.Always => "always",
                _ => "reject",
            },
        };

        if (message is not null)
        {
            body["message"] = message;
        }

        await SendAsync(
            HttpMethod.Post,
            $"/api/session/{Uri.EscapeDataString(sessionId)}/permission/{Uri.EscapeDataString(permissionId)}/reply",
            body,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task InterruptAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await SendAsync(
            HttpMethod.Post,
            $"/api/session/{Uri.EscapeDataString(sessionId)}/interrupt?resume=false",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<OpenCodeEvent> SubscribeAsync(
        IReadOnlyCollection<string>? types,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var path = "/api/event";
        if (types is { Count: > 0 })
        {
            path += "?" + string.Join("&", types.Select(type => "types=" + Uri.EscapeDataString(type)));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, path));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new OpenCodeApiException("GET", path, (int)response.StatusCode, body);
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            if (line.Length == 0 || line[0] == ':')
            {
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var payload = line[5..].TrimStart();
            var parsed = TryParseEvent(payload);
            if (parsed is not null)
            {
                yield return parsed;
            }
        }
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private async Task<string> SendAsync(
        HttpMethod method,
        string path,
        JsonNode? body,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, new Uri(BaseAddress, path));
        request.Headers.Authorization = _authorization;

        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(SerializerOptions), Encoding.UTF8, "application/json");
        }

        using var response = await _http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
        {
            return string.Empty;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new OpenCodeApiException(method.Method, path, (int)response.StatusCode, text);
        }

        return text;
    }

    private static OpenCodeEvent? TryParseEvent(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var type = OpenCodeJson.String(root, "type");
            if (string.IsNullOrEmpty(type))
            {
                return null;
            }

            var data = root.TryGetProperty("data", out var value) ? value : default;

            return new OpenCodeEvent(
                OpenCodeJson.String(root, "id") ?? string.Empty,
                type,
                OpenCodeJson.Date(root, "created"),
                OpenCodeJson.String(data, "sessionID"),
                data.ValueKind == JsonValueKind.Undefined ? default : data.Clone(),
                root.Clone());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsLoopback(Uri baseAddress)
    {
        if (!baseAddress.IsAbsoluteUri || baseAddress.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (baseAddress.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(baseAddress.Host, out var address)
               && IPAddress.IsLoopback(address);
    }
}
