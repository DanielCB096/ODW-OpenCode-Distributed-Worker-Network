using System.Text.Json.Nodes;

namespace Odw.Mcp;

public sealed record McpTool(
    string Name,
    string Description,
    JsonObject InputSchema,
    Func<JsonObject, CancellationToken, Task<McpToolResult>> Handler);

public sealed record McpToolResult(string Text, JsonObject? StructuredContent, bool IsError)
{
    public static McpToolResult Ok(string text, JsonObject? structuredContent = null)
        => new(Truncate(text), structuredContent, false);

    public static McpToolResult Error(
        string code,
        string detail,
        bool retryable = false,
        JsonObject? extra = null)
    {
        var structured = new JsonObject
        {
            ["code"] = code,
            ["detail"] = detail,
            ["retryable"] = retryable,
        };

        if (extra is not null)
        {
            foreach (var (key, value) in extra)
            {
                structured[key] = value?.DeepClone();
            }
        }

        return new McpToolResult(Truncate($"{code}: {detail}"), structured, true);
    }

    private static string Truncate(string text)
        => text.Length <= 400 ? text : text[..397] + "...";
}
