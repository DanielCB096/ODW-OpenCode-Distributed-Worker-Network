using System.Text.Json;
using System.Text.Json.Serialization;

namespace Odw.Executors.OpenCode;

public sealed record OpenCodeConnection(Uri BaseAddress, string Username, string Password);

public enum OpenCodePermissionEffect
{
    Allow,
    Deny,
    Ask,
}

public sealed record OpenCodePermissionRule(string Action, string Resource, OpenCodePermissionEffect Effect)
{
    public string EffectValue => Effect switch
    {
        OpenCodePermissionEffect.Allow => "allow",
        OpenCodePermissionEffect.Deny => "deny",
        _ => "ask",
    };
}

public sealed record OpenCodeSessionRequest(
    string Directory,
    string? Title = null,
    IReadOnlyList<OpenCodePermissionRule>? Permissions = null,
    string? Agent = null,
    string? ModelProvider = null,
    string? ModelId = null);

public sealed record OpenCodeSession(
    string Id,
    string? Title,
    string? Directory,
    DateTimeOffset CreatedAt,
    DateTimeOffset? BusySince,
    DateTimeOffset? IdleSince)
{
    public bool IsBusy => BusySince is not null && (IdleSince is null || BusySince > IdleSince);
}

public sealed record OpenCodeMessage(string Id, string Type, IReadOnlyList<string> TextParts)
{
    public string Text => string.Join("\n", TextParts);
}

public sealed record OpenCodePermissionRequest(
    string Id,
    string SessionId,
    string Action,
    IReadOnlyList<string> Resources,
    IReadOnlyList<string> Save,
    string? Message);

public enum OpenCodePermissionReply
{
    Once,
    Always,
    Reject,
}

public sealed record OpenCodeServerInfo(string Version, int Pid, IReadOnlyList<string> Urls);

public sealed class OpenCodeApiException : Exception
{
    public OpenCodeApiException(string method, string path, int statusCode, string body)
        : base($"OpenCode {method} {path} devolvió {statusCode}: {Trim(body)}")
    {
        Method = method;
        Path = path;
        StatusCode = statusCode;
        Body = body;
    }

    public string Method { get; }

    public string Path { get; }

    public int StatusCode { get; }

    public string Body { get; }

    private static string Trim(string body)
        => body.Length <= 300 ? body : body[..297] + "...";
}

internal static class OpenCodeJson
{
    public static JsonElement Payload(JsonElement root)
        => root.ValueKind == JsonValueKind.Object
           && root.TryGetProperty("data", out var data)
           && data.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? data
            : root;

    public static string? String(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static double? Double(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetDouble(out var number)
            ? number
            : null;

    public static DateTimeOffset? Date(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(name, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetInt64(out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : null;

    public static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToArray();
    }

    public static string? NestedString(JsonElement element, string outer, string inner)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(outer, out var nested)
            ? String(nested, inner)
            : null;

    public static OpenCodeSession ParseSession(JsonElement root)
    {
        var data = Payload(root);
        var time = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("time", out var t)
            ? t
            : default;

        var created = Date(time, "created") ?? DateTimeOffset.UnixEpoch;

        return new OpenCodeSession(
            String(data, "id") ?? throw new OpenCodeProtocolException("Sesión sin id."),
            String(data, "title"),
            NestedString(data, "location", "directory"),
            created,
            Date(time, "busy"),
            Date(time, "idle"));
    }

    public static IReadOnlyList<OpenCodePermissionRequest> ParsePermissions(JsonElement root)
    {
        var payload = Payload(root);
        if (payload.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<OpenCodePermissionRequest>();
        foreach (var item in payload.EnumerateArray())
        {
            var id = String(item, "id");
            var sessionId = String(item, "sessionID");
            var action = String(item, "action");

            if (id is null || sessionId is null || action is null)
            {
                continue;
            }

            result.Add(new OpenCodePermissionRequest(
                id,
                sessionId,
                action,
                Strings(item, "resources"),
                Strings(item, "save"),
                String(item, "message")));
        }

        return result;
    }

    public static IReadOnlyList<OpenCodeMessage> ParseMessages(JsonElement root)
    {
        var payload = Payload(root);
        if (payload.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var messages = new List<OpenCodeMessage>();
        foreach (var item in payload.EnumerateArray())
        {
            var id = String(item, "id") ?? string.Empty;
            var type = String(item, "type") ?? "unknown";
            var texts = new List<string>();

            if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in content.EnumerateArray())
                {
                    if (String(part, "type") == "text" && String(part, "text") is { } text)
                    {
                        texts.Add(text);
                    }
                }
            }

            messages.Add(new OpenCodeMessage(id, type, texts));
        }

        return messages;
    }
}

public sealed class OpenCodeProtocolException(string message) : Exception(message);
