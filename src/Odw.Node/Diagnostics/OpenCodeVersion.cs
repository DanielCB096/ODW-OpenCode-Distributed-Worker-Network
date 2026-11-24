using System.Text.RegularExpressions;

namespace Odw.Node.Diagnostics;

public static partial class OpenCodeVersion
{
    public static bool TryParse(string? output, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        var match = VersionPattern().Match(output);
        if (!match.Success)
        {
            return false;
        }

        version = match.Value;
        return true;
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
