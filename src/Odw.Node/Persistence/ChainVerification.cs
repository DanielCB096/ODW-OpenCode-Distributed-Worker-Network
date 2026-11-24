using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Odw.Node.Persistence;

public sealed record ChainVerification(bool IsValid, long? FirstBadSeq, string? Detail)
{
    public static ChainVerification Valid { get; } = new(true, null, null);
}

/// <summary>
/// Hash encadenado con longitud prefijada por campo. Evita ambigüedades de concatenación y
/// es reproducible en pruebas. No es JCS: se usa solo para las cadenas internas de eventos/auditoría.
/// </summary>
internal static class ChainHash
{
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    public static string Compute(string? previous, params string?[] fields)
    {
        var builder = new StringBuilder(256);
        builder.Append(previous ?? Genesis).Append('\n');

        foreach (var field in fields)
        {
            var value = field ?? "\u0001";
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('\n');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexStringLower(bytes);
    }
}
