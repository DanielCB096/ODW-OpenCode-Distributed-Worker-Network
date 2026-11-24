using System.Text.Json.Nodes;

namespace Odw.Mcp;

/// <summary>Constructores de respuestas JSON-RPC 2.0.</summary>
public static class JsonRpc
{
    public const string Version = "2.0";

    public static class ErrorCodes
    {
        public const int ParseError = -32700;
        public const int InvalidRequest = -32600;
        public const int MethodNotFound = -32601;
        public const int InvalidParams = -32602;
        public const int InternalError = -32603;
    }

    public static JsonObject Success(JsonNode? id, JsonNode result) => new()
    {
        ["jsonrpc"] = Version,
        ["id"] = id?.DeepClone(),
        ["result"] = result,
    };

    public static JsonObject Error(JsonNode? id, int code, string message, JsonNode? data = null)
    {
        var error = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
        };

        if (data is not null)
        {
            error["data"] = data;
        }

        return new JsonObject
        {
            ["jsonrpc"] = Version,
            ["id"] = id?.DeepClone(),
            ["error"] = error,
        };
    }
}
