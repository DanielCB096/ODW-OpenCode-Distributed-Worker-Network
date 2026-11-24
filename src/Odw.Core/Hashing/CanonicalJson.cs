using System.Globalization;
using System.Text;

namespace Odw.Core.Hashing;

/// <summary>
/// Serializador canónico (subconjunto de RFC 8785 / JCS): claves ordenadas por unidades
/// UTF-16, sin espacios, números en forma canónica y cadenas escapadas como JSON.stringify.
/// Solo se usa para la proyección de hash, no es un serializador general.
/// </summary>
internal static class CanonicalJson
{
    public static string Serialize(CanonicalNode node)
    {
        var builder = new StringBuilder(512);
        Write(node, builder);
        return builder.ToString();
    }

    private static void Write(CanonicalNode node, StringBuilder builder)
    {
        switch (node)
        {
            case CanonicalNull:
                builder.Append("null");
                break;

            case CanonicalBoolean boolean:
                builder.Append(boolean.Value ? "true" : "false");
                break;

            case CanonicalString text:
                WriteString(text.Value, builder);
                break;

            case CanonicalNumber number:
                builder.Append(FormatNumber(number.Value));
                break;

            case CanonicalArray array:
                builder.Append('[');
                for (var i = 0; i < array.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    Write(array[i], builder);
                }

                builder.Append(']');
                break;

            case CanonicalObject obj:
                builder.Append('{');
                var first = true;
                foreach (var (key, value) in obj.Members)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(key, builder);
                    builder.Append(':');
                    Write(value, builder);
                }

                builder.Append('}');
                break;

            default:
                throw new InvalidOperationException($"Nodo canónico no soportado: {node.GetType().Name}");
        }
    }

    private static void WriteString(string value, StringBuilder builder)
    {
        builder.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else if (char.IsHighSurrogate(c))
                    {
                        if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                        {
                            throw new FormatException("La cadena contiene un surrogate alto sin pareja.");
                        }

                        builder.Append(c);
                        builder.Append(value[++i]);
                    }
                    else if (char.IsLowSurrogate(c))
                    {
                        throw new FormatException("La cadena contiene un surrogate bajo sin pareja.");
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    /// <summary>
    /// Forma canónica de un número decimal en el rango del contrato (0..100). JCS exige la
    /// forma de ECMAScript; para este rango equivale a quitar ceros finales y el punto sobrante.
    /// </summary>
    private static string FormatNumber(decimal value)
    {
        if (value == 0m)
        {
            return "0";
        }

        if (value == decimal.Truncate(value))
        {
            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        var text = value.ToString(CultureInfo.InvariantCulture);
        text = text.TrimEnd('0').TrimEnd('.');

        return text is "-0" ? "0" : text;
    }
}

internal abstract class CanonicalNode;

internal sealed class CanonicalNull : CanonicalNode
{
    public static CanonicalNull Instance { get; } = new();

    private CanonicalNull()
    {
    }
}

internal sealed class CanonicalBoolean(bool value) : CanonicalNode
{
    public bool Value { get; } = value;
}

internal sealed class CanonicalString(string value) : CanonicalNode
{
    public string Value { get; } = value;
}

internal sealed class CanonicalNumber(decimal value) : CanonicalNode
{
    public decimal Value { get; } = value;
}

internal sealed class CanonicalArray : CanonicalNode
{
    private readonly List<CanonicalNode> _items = [];

    public int Count => _items.Count;

    public CanonicalNode this[int index] => _items[index];

    public void Add(CanonicalNode node) => _items.Add(node);
}

internal sealed class CanonicalObject : CanonicalNode
{
    private readonly SortedDictionary<string, CanonicalNode> _members =
        new(StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<string, CanonicalNode>> Members => _members;

    public void Add(string key, CanonicalNode value) => _members.Add(key, value);
}
