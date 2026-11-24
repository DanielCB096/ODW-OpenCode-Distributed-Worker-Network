using System.Text.Json.Nodes;

namespace Odw.Mcp.Tools;

internal static class JsonTools
{
    public static JsonObject Schema(string json)
        => (JsonObject)JsonNode.Parse(json)!;

    public static void AddIfNotNull(JsonObject target, string name, JsonNode? value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }

    public static JsonArray ToArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    public static bool? GetBool(JsonObject arguments, string name)
        => arguments[name] is JsonValue value && value.TryGetValue<bool>(out var result) ? result : null;

    public static string? GetString(JsonObject arguments, string name)
        => arguments[name] is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;

    public static int? GetInt(JsonObject arguments, string name)
        => arguments[name] is JsonValue value && value.TryGetValue<int>(out var result) ? result : null;

    public static decimal? GetDecimal(JsonObject arguments, string name)
    {
        if (arguments[name] is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<decimal>(out var result))
        {
            return result;
        }

        return value.TryGetValue<double>(out var number) ? (decimal)number : null;
    }

    public static IReadOnlyList<string>? GetStrings(JsonObject arguments, string name)
    {
        if (arguments[name] is not JsonArray array)
        {
            return null;
        }

        var values = new List<string>(array.Count);
        foreach (var item in array)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var text))
            {
                return null;
            }

            values.Add(text);
        }

        return values;
    }

    public static JsonObject? GetObject(JsonObject arguments, string name)
        => arguments[name] as JsonObject;
}
