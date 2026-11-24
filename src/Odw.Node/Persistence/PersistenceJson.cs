using System.Globalization;
using System.Text.Json;

namespace Odw.Node.Persistence;

/// <summary>Formatos estables de persistencia: ISO 8601 con offset y JSON camelCase.</summary>
public static class PersistenceJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Timestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static string? TimestampOrNull(DateTimeOffset? value)
        => value is null ? null : Timestamp(value.Value);

    public static DateTimeOffset ParseTimestamp(string value)
        => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset? ParseTimestampOrNull(string? value)
        => string.IsNullOrEmpty(value) ? null : ParseTimestamp(value);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
}
