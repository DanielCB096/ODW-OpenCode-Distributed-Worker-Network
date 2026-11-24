using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Odw.Core.Contracts;
using Odw.Core.Primitives;

namespace Odw.Policy;

/// <summary>
/// Carga y valida <c>policy.json</c>. Fail-closed: cualquier problema impide usar la política
/// (el host no debe arrancar con un documento inválido).
/// </summary>
public static class PolicyDocumentLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = false,
    };

    private static readonly Regex IdPattern = new(
        "^[a-z0-9-]{3,64}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TimePattern = new(
        "^([01][0-9]|2[0-3]):[0-5][0-9]$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
    {
        "analysis", "test_run", "code_change",
    };

    private static readonly HashSet<string> Modes = new(StringComparer.Ordinal)
    {
        "read_only", "read_write_worktree",
    };

    private static readonly HashSet<string> Trusts = new(StringComparer.OrdinalIgnoreCase)
    {
        "trusted", "limited", "blocked", "unknown",
    };

    private static readonly HashSet<string> Networks = new(StringComparer.Ordinal)
    {
        "none", "allow",
    };

    public static PolicyDocument LoadFile(
        string path,
        Func<string, TimeZoneInfo>? zoneResolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Load(File.ReadAllText(path), zoneResolver);
    }

    public static PolicyDocument Load(
        string json,
        Func<string, TimeZoneInfo>? zoneResolver = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        zoneResolver ??= ResolveZone;

        DocumentDto? document;
        try
        {
            document = JsonSerializer.Deserialize<DocumentDto>(json, Options);
        }
        catch (JsonException exception)
        {
            throw new PolicyValidationException(
                [new PolicyIssue("JSON_INVALID", "$", exception.Message)]);
        }

        var issues = new List<PolicyIssue>();

        if (document is null)
        {
            throw new PolicyValidationException([new PolicyIssue("JSON_INVALID", "$", "Documento vacío.")]);
        }

        if (document.Version != 1)
        {
            Add(issues, "CONTRACT_INVALID", "version", "Se espera version = 1.");
        }

        var defaultEffect = ParseEffect(document.Default, "default", issues)
                            ?? PolicyEffect.Ask;

        var rules = new List<PolicyRule>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        if (document.Rules is null)
        {
            Add(issues, "CONTRACT_INVALID", "rules", "Falta el array de reglas.");
        }
        else if (document.Rules.Count > 256)
        {
            Add(issues, "CONTRACT_INVALID", "rules", "Máximo 256 reglas.");
        }
        else
        {
            for (var index = 0; index < document.Rules.Count; index++)
            {
                var mapped = MapRule(document.Rules[index], $"rules[{index}]", zoneResolver, issues, seenIds);
                if (mapped is not null)
                {
                    rules.Add(mapped);
                }
            }
        }

        if (issues.Count > 0)
        {
            throw new PolicyValidationException(issues);
        }

        return new PolicyDocument(1, defaultEffect, rules);
    }

    private static PolicyRule? MapRule(
        RuleDto dto,
        string path,
        Func<string, TimeZoneInfo> zoneResolver,
        List<PolicyIssue> issues,
        HashSet<string> seenIds)
    {
        var start = issues.Count;

        var id = dto.Id;
        if (string.IsNullOrWhiteSpace(id) || !IdPattern.IsMatch(id))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.id", "ID inválido (^[a-z0-9-]{3,64}$).");
        }
        else if (!seenIds.Add(id))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.id", $"ID duplicado: {id}.");
        }

        if (dto.Priority is null || dto.Priority is < 0 or > 1000)
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.priority", "priority debe estar en 0..1000.");
        }

        var effect = ParseEffect(dto.Effect, $"{path}.effect", issues);

        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Length > 512)
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.reason", "reason es obligatorio (1..512).");
        }

        var when = MapWhen(dto.When, $"{path}.when", zoneResolver, issues);

        if (issues.Count != start)
        {
            return null;
        }

        return new PolicyRule(id!, dto.Priority!.Value, effect!.Value, dto.Reason!, when ?? new PolicyWhen());
    }

    private static PolicyWhen? MapWhen(
        WhenDto? dto,
        string path,
        Func<string, TimeZoneInfo> zoneResolver,
        List<PolicyIssue> issues)
    {
        if (dto is null)
        {
            return new PolicyWhen();
        }

        var start = issues.Count;

        if (dto.Direction is not null && dto.Direction is not ("in" or "out"))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.direction", "direction debe ser in u out.");
        }

        var kind = MapStrings(dto.Kind, $"{path}.kind", Kinds, issues);
        var mode = MapStrings(dto.Mode, $"{path}.mode", Modes, issues);
        var trust = MapStrings(dto.PeerTrust, $"{path}.peer_trust", Trusts, issues);
        var peer = MapStrings(dto.Peer, $"{path}.peer", null, issues);
        var repo = MapStrings(dto.Repo, $"{path}.repo", null, issues);
        var actions = MapStrings(dto.AnyAction, $"{path}.any_action", null, issues);
        var capabilitiesAll = MapStrings(dto.CapabilitiesAll, $"{path}.capabilities_all", null, issues);
        var capabilitiesNone = MapStrings(dto.CapabilitiesNone, $"{path}.capabilities_none", null, issues);

        if (peer is not null && peer.Any(node => !ContractFormat.IsValidNodeId(node)))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.peer", "Algún node_id es inválido.");
        }

        if (repo is not null && repo.Any(name => !ContractFormat.IsValidRepoName(name)))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.repo", "Algún nombre de repo es inválido.");
        }

        var limits = MapLimits(dto.Limits, $"{path}.limits", issues);
        var window = MapTimeWindow(dto.TimeWindow, $"{path}.time_window", zoneResolver, issues);

        return issues.Count == start
            ? new PolicyWhen
            {
                Direction = dto.Direction,
                Kind = kind,
                Mode = mode,
                Peer = peer,
                PeerTrust = trust,
                Repo = repo,
                Parent = dto.Parent,
                AnyAction = actions,
                CapabilitiesAll = capabilitiesAll,
                CapabilitiesNone = capabilitiesNone,
                Limits = limits,
                TimeWindow = window,
            }
            : null;
    }

    private static LimitsWhen? MapLimits(LimitsDto? dto, string path, List<PolicyIssue> issues)
    {
        if (dto is null)
        {
            return null;
        }

        var start = issues.Count;

        var minutes = MapRange(dto.MaxMinutes, $"{path}.max_minutes", issues);
        var memory = MapRange(dto.MaxMemoryGb, $"{path}.max_memory_gb", issues);
        var cost = MapRange(dto.MaxCostUsd, $"{path}.max_cost_usd", issues);

        if (dto.Network is not null && !Networks.Contains(dto.Network))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.network", "network debe ser none o allow.");
        }

        if (minutes is null && memory is null && cost is null && dto.Network is null)
        {
            Add(issues, "CONTRACT_INVALID", path, "limits no puede estar vacío.");
        }

        return issues.Count == start
            ? new LimitsWhen
            {
                MaxMinutes = minutes,
                MaxMemoryGb = memory,
                MaxCostUsd = cost,
                Network = dto.Network,
            }
            : null;
    }

    private static RangeWhen? MapRange(RangeDto? dto, string path, List<PolicyIssue> issues)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.Lte is null && dto.Gte is null)
        {
            Add(issues, "CONTRACT_INVALID", path, "El rango necesita lte o gte.");
            return null;
        }

        if (dto.Lte is { } lte && dto.Gte is { } gte && gte > lte)
        {
            Add(issues, "CONTRACT_INVALID", path, "gte no puede superar a lte.");
            return null;
        }

        if (dto.Lte is { } lte2 && !double.IsFinite(lte2))
        {
            Add(issues, "CONTRACT_INVALID", path, "Los límites deben ser números finitos.");
            return null;
        }

        if (dto.Gte is { } gte2 && !double.IsFinite(gte2))
        {
            Add(issues, "CONTRACT_INVALID", path, "Los límites deben ser números finitos.");
            return null;
        }

        return new RangeWhen(dto.Lte, dto.Gte);
    }

    private static TimeWindowWhen? MapTimeWindow(
        TimeWindowDto? dto,
        string path,
        Func<string, TimeZoneInfo> zoneResolver,
        List<PolicyIssue> issues)
    {
        if (dto is null)
        {
            return null;
        }

        var start = issues.Count;

        if (dto.Days is null || dto.Days.Length == 0)
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.days", "Debe indicar al menos un día.");
        }
        else
        {
            if (dto.Days.Any(day => day is < 1 or > 7))
            {
                Add(issues, "CONTRACT_INVALID", $"{path}.days", "Los días van de 1 (lunes) a 7 (domingo).");
            }

            if (dto.Days.Distinct().Count() != dto.Days.Length)
            {
                Add(issues, "CONTRACT_INVALID", $"{path}.days", "Hay días duplicados.");
            }
        }

        if (dto.From is null || !TimePattern.IsMatch(dto.From))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.from", "from debe ser HH:mm.");
        }

        if (dto.To is null || !TimePattern.IsMatch(dto.To))
        {
            Add(issues, "CONTRACT_INVALID", $"{path}.to", "to debe ser HH:mm.");
        }

        if (dto.TimeZone is not null)
        {
            try
            {
                _ = zoneResolver(dto.TimeZone);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                Add(issues, "CONTRACT_INVALID", $"{path}.tz", $"Zona horaria desconocida: {dto.TimeZone}.");
            }
        }

        return issues.Count == start
            ? new TimeWindowWhen(dto.Days!, dto.From!, dto.To!, dto.TimeZone)
            : null;
    }

    private static IReadOnlyCollection<string>? MapStrings(
        string[]? values,
        string path,
        HashSet<string>? allowed,
        List<PolicyIssue> issues)
    {
        if (values is null)
        {
            return null;
        }

        var list = new List<string>(values.Length);
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
            {
                Add(issues, "CONTRACT_INVALID", $"{path}[{index}]", "Valor vacío o demasiado largo.");
                continue;
            }

            if (allowed is not null && !allowed.Contains(value))
            {
                Add(issues, "CONTRACT_INVALID", $"{path}[{index}]", $"Valor no permitido: {value}.");
                continue;
            }

            list.Add(value);
        }

        return list;
    }

    private static PolicyEffect? ParseEffect(string? value, string path, List<PolicyIssue> issues)
    {
        return value?.ToUpperInvariant() switch
        {
            "AUTO" => PolicyEffect.Auto,
            "ASK" => PolicyEffect.Ask,
            "DENY" => PolicyEffect.Deny,
            null => AddEffectError(issues, path, "Falta effect."),
            _ => AddEffectError(issues, path, $"Effect inválido: {value}."),
        };
    }

    private static PolicyEffect? AddEffectError(List<PolicyIssue> issues, string path, string message)
    {
        Add(issues, "CONTRACT_INVALID", path, message);
        return null;
    }

    private static void Add(List<PolicyIssue> issues, string code, string path, string message)
        => issues.Add(new PolicyIssue(code, path, message));

    private static TimeZoneInfo ResolveZone(string id)
        => TimeZoneInfo.FindSystemTimeZoneById(id);

    private sealed class DocumentDto
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("version")]
        public int? Version { get; set; }

        [JsonPropertyName("default")]
        public string? Default { get; set; }

        [JsonPropertyName("rules")]
        public List<RuleDto>? Rules { get; set; }
    }

    private sealed class RuleDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }

        [JsonPropertyName("effect")]
        public string? Effect { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("when")]
        public WhenDto? When { get; set; }
    }

    private sealed class WhenDto
    {
        [JsonPropertyName("direction")]
        public string? Direction { get; set; }

        [JsonPropertyName("kind")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? Kind { get; set; }

        [JsonPropertyName("mode")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? Mode { get; set; }

        [JsonPropertyName("peer")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? Peer { get; set; }

        [JsonPropertyName("peer_trust")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? PeerTrust { get; set; }

        [JsonPropertyName("repo")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? Repo { get; set; }

        [JsonPropertyName("parent")]
        public bool? Parent { get; set; }

        [JsonPropertyName("any_action")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? AnyAction { get; set; }

        [JsonPropertyName("capabilities_all")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? CapabilitiesAll { get; set; }

        [JsonPropertyName("capabilities_none")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public string[]? CapabilitiesNone { get; set; }

        [JsonPropertyName("limits")]
        public LimitsDto? Limits { get; set; }

        [JsonPropertyName("time_window")]
        public TimeWindowDto? TimeWindow { get; set; }
    }

    private sealed class LimitsDto
    {
        [JsonPropertyName("max_minutes")]
        public RangeDto? MaxMinutes { get; set; }

        [JsonPropertyName("max_memory_gb")]
        public RangeDto? MaxMemoryGb { get; set; }

        [JsonPropertyName("max_cost_usd")]
        public RangeDto? MaxCostUsd { get; set; }

        [JsonPropertyName("network")]
        public string? Network { get; set; }
    }

    private sealed class RangeDto
    {
        [JsonPropertyName("lte")]
        public double? Lte { get; set; }

        [JsonPropertyName("gte")]
        public double? Gte { get; set; }
    }

    private sealed class TimeWindowDto
    {
        [JsonPropertyName("days")]
        public int[]? Days { get; set; }

        [JsonPropertyName("from")]
        public string? From { get; set; }

        [JsonPropertyName("to")]
        public string? To { get; set; }

        [JsonPropertyName("tz")]
        public string? TimeZone { get; set; }
    }
}

/// <summary>Acepta un string o un array de strings (condiciones OR).</summary>
internal sealed class StringOrArrayConverter : JsonConverter<string[]>
{
    public override string[] Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return [reader.GetString()!];
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Se espera un string o un array de strings.");
        }

        var values = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("El array solo admite strings.");
            }

            values.Add(reader.GetString()!);
        }

        return values.ToArray();
    }

    public override void Write(Utf8JsonWriter writer, string[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
