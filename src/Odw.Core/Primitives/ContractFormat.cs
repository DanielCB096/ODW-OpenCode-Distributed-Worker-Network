using System.Text.RegularExpressions;

namespace Odw.Core.Primitives;

/// <summary>
/// Formatos y normalizaciones del contrato. Todo lo que entra de otra máquina pasa por aquí.
/// </summary>
public static partial class ContractFormat
{
    public const int MaxInstructionsLength = 65_536;
    public const int MaxPathLength = 512;

    private static readonly char[] InvalidWindowsChars = ['<', '>', '"', '|', '?', '*'];
    private static readonly string[] ReservedDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{1,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex NodeIdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex RepoNamePattern();

    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex TaskHashPattern();

    public static bool IsValidNodeId(string? value)
        => value is not null && NodeIdPattern().IsMatch(value);

    public static bool IsValidRepoName(string? value)
        => value is not null && RepoNamePattern().IsMatch(value);

    public static bool IsValidTaskHash(string? value)
        => value is not null && TaskHashPattern().IsMatch(value);

    /// <summary>Reglas conservadoras de nombre de rama/tag (subconjunto de git check-ref-format).</summary>
    public static bool IsValidGitRef(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        if (value[0] is '-' or '/' or '.' || value[^1] is '/' or '.')
        {
            return false;
        }

        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains("@{", StringComparison.Ordinal)
            || value.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var c in value)
        {
            var allowed = char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '/' or '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Normaliza y valida una ruta relativa al workspace: separador '/', sin '.', '..', unidad,
    /// UNC, ADS, reparse points ni nombres reservados. Devuelve sin '/' final.
    /// </summary>
    public static bool TryNormalizeRelativePath(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (input is null)
        {
            return false;
        }

        var value = input.Trim();
        if (value.Length is 0 or > MaxPathLength || HasLoneSurrogate(value) || value.Contains('\0'))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (char.IsControl(c) || c == ':')
            {
                return false;
            }
        }

        var unified = value.Replace('\\', '/');
        if (unified.StartsWith('/') || unified.Contains("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (unified.Length >= 2 && char.IsAsciiLetter(unified[0]) && unified[1] == ':')
        {
            return false;
        }

        var segments = unified.Split('/');
        var parts = new List<string>(segments.Length);
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                return false;
            }

            // Windows recorta puntos y espacios finales: se rechazan para evitar ambigüedad.
            if (segment[^1] is ' ' or '.')
            {
                return false;
            }

            if (segment.IndexOfAny(InvalidWindowsChars) >= 0)
            {
                return false;
            }

            var baseName = segment.Split('.')[0].ToUpperInvariant();
            if (ReservedDeviceNames.Contains(baseName, StringComparer.Ordinal))
            {
                return false;
            }

            parts.Add(segment);
        }

        if (parts.Count == 0)
        {
            return false;
        }

        normalized = string.Join('/', parts);
        return true;
    }

    /// <summary>
    /// Normaliza las instrucciones: CRLF/CR a LF, sin espacios finales por línea, sin
    /// espacios al inicio/fin. No agrega salto final (los vectores de hash dependen de esto).
    /// </summary>
    public static string NormalizeInstructions(string instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);

        var unified = instructions.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = unified.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd(' ', '\t');
        }

        return string.Join('\n', lines).Trim();
    }

    public static bool HasLoneSurrogate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return true;
                }

                i++;
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                return true;
            }
        }

        return false;
    }
}
