namespace Odw.Core.Model;

/// <summary>Perfil de modelo permitido por el nodo destino.</summary>
public sealed record ModelProfile(
    string Name,
    string Provider,
    string Model,
    decimal MaxCostUsd)
{
    /// <summary>Identificador que entiende OpenCode: provider/model.</summary>
    public string QualifiedName => $"{Provider}/{Model}";
}

/// <summary>Perfil gratuito por defecto, compartido por host, runner y pruebas.</summary>
public static class ModelCatalog
{
    public const string DefaultProfileName = "free";

    public static ModelProfile Free { get; } =
        new(DefaultProfileName, "opencode", "deepseek-v4.1-flash-free", 0m);

    public static IReadOnlyDictionary<string, ModelProfile> DefaultProfiles { get; } =
        new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
        {
            [Free.Name] = Free,
        };
}

/// <summary>
/// Resuelve el perfil solicitado contra la allowlist del nodo. El nodo destino es la autoridad:
/// el origen solo puede pedir un nombre de perfil, nunca provider/model ni credenciales.
/// </summary>
public static class ModelProfileResolver
{
    public static bool TryResolve(
        string? requestedProfile,
        string? modelHint,
        string defaultProfile,
        IReadOnlyDictionary<string, ModelProfile> profiles,
        out ModelProfile? profile,
        out string? errorCode,
        out string? errorDetail)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        profile = null;
        errorCode = null;
        errorDetail = null;

        var hasProfile = !string.IsNullOrWhiteSpace(requestedProfile);
        var hasHint = !string.IsNullOrWhiteSpace(modelHint);

        if (hasProfile && hasHint)
        {
            errorCode = "CONTRACT_INVALID";
            errorDetail = "Usa model_profile o model_hint, no ambos.";
            return false;
        }

        if (hasProfile)
        {
            if (!profiles.TryGetValue(requestedProfile!, out profile))
            {
                errorCode = "MODEL_NOT_ALLOWED";
                errorDetail = $"El perfil de modelo '{requestedProfile}' no esta permitido en este nodo.";
                return false;
            }

            return true;
        }

        if (hasHint)
        {
            profile = profiles.Values.FirstOrDefault(
                candidate => string.Equals(candidate.QualifiedName, modelHint, StringComparison.Ordinal));

            if (profile is null)
            {
                errorCode = "MODEL_NOT_ALLOWED";
                errorDetail = $"El modelo '{modelHint}' no esta permitido en este nodo.";
                return false;
            }

            return true;
        }

        if (!profiles.TryGetValue(defaultProfile, out profile))
        {
            errorCode = "MODEL_NOT_CONFIGURED";
            errorDetail = $"No hay perfil de modelo por defecto '{defaultProfile}'.";
            return false;
        }

        return true;
    }
}
