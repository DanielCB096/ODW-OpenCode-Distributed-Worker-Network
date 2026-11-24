using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Cryptography;

namespace Odw.Mcp;

public sealed record McpOptions
{
    public string Path { get; init; } = "/mcp";

    public required string Token { get; init; }

    public int MaxBodyBytes { get; init; } = 262_144;
}

/// <summary>
/// Fachada MCP local: Streamable HTTP sin estado, solo POST, token bearer obligatorio.
/// No expone SSE ni sesiones; el host la enlaza únicamente a loopback (ADR-002).
/// </summary>
public static class McpEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public static IEndpointConventionBuilder MapOdwMcp(
        this IEndpointRouteBuilder endpoints,
        McpServer server,
        McpOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new ArgumentException("La fachada MCP exige un token.", nameof(options));
        }

        return endpoints
            .MapPost(options.Path, (HttpContext context) => HandleAsync(context, server, options))
            .WithDisplayName("ODW MCP")
            .AllowAnonymous();
    }

    internal static bool IsAuthorized(string? authorizationHeader, string expectedToken)
    {
        if (string.IsNullOrEmpty(authorizationHeader) || string.IsNullOrEmpty(expectedToken))
        {
            return false;
        }

        const string scheme = "Bearer ";
        if (!authorizationHeader.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var provided = authorizationHeader[scheme.Length..].Trim();
        if (provided.Length == 0)
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedToken));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }

    private static async Task HandleAsync(HttpContext context, McpServer server, McpOptions options)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!IsAuthorized(context.Request.Headers.Authorization, options.Token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        var contentType = context.Request.ContentType;
        if (contentType is null
            || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        if (context.Request.ContentLength > options.MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        string body;
        using (var reader = new StreamReader(context.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        if (Encoding.UTF8.GetByteCount(body) > options.MaxBodyBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            await WriteAsync(context, JsonRpc.Error(
                null,
                JsonRpc.ErrorCodes.ParseError,
                "JSON inválido.")).ConfigureAwait(false);
            return;
        }

        if (node is not JsonObject request)
        {
            await WriteAsync(context, JsonRpc.Error(
                null,
                JsonRpc.ErrorCodes.InvalidRequest,
                "Se espera un objeto JSON-RPC; los lotes no están soportados.")).ConfigureAwait(false);
            return;
        }

        var response = await server.HandleAsync(request, context.RequestAborted).ConfigureAwait(false);
        if (response is null)
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await WriteAsync(context, response).ConfigureAwait(false);
    }

    private static async Task WriteAsync(HttpContext context, JsonNode response)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response
            .WriteAsync(response.ToJsonString(JsonOptions), context.RequestAborted)
            .ConfigureAwait(false);
    }
}
