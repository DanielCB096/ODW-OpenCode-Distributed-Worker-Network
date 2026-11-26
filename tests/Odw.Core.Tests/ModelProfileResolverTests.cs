using Odw.Core.Model;
using Xunit;

namespace Odw.Core.Tests;

public class ModelProfileResolverTests
{
    private static readonly ModelProfile Paid =
        new("paid-deepseek", "opencode-go", "deepseek-v4.1-flash", 2m);

    private static readonly IReadOnlyDictionary<string, ModelProfile> FreeOnly =
        ModelCatalog.DefaultProfiles;

    private static readonly IReadOnlyDictionary<string, ModelProfile> WithPaid =
        new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
        {
            [ModelCatalog.Free.Name] = ModelCatalog.Free,
            [Paid.Name] = Paid,
        };

    [Fact]
    public void Default_resolves_to_free()
    {
        var ok = ModelProfileResolver.TryResolve(
            null, null, ModelCatalog.DefaultProfileName, FreeOnly, out var profile, out _, out _);

        Assert.True(ok);
        Assert.Equal(ModelCatalog.Free, profile);
    }

    [Fact]
    public void Allowed_profile_resolves()
    {
        var ok = ModelProfileResolver.TryResolve(
            Paid.Name, null, ModelCatalog.DefaultProfileName, WithPaid, out var profile, out _, out _);

        Assert.True(ok);
        Assert.Equal(Paid, profile);
        Assert.Equal("opencode-go/deepseek-v4.1-flash", profile!.QualifiedName);
    }

    [Fact]
    public void Disallowed_profile_is_rejected()
    {
        var ok = ModelProfileResolver.TryResolve(
            Paid.Name, null, ModelCatalog.DefaultProfileName, FreeOnly, out var profile, out var code, out _);

        Assert.False(ok);
        Assert.Null(profile);
        Assert.Equal("MODEL_NOT_ALLOWED", code);
    }

    [Fact]
    public void Legacy_hint_resolves_only_if_it_matches_an_allowed_profile()
    {
        var allowed = ModelProfileResolver.TryResolve(
            null,
            Paid.QualifiedName,
            ModelCatalog.DefaultProfileName,
            WithPaid,
            out var profile,
            out _,
            out _);

        Assert.True(allowed);
        Assert.Equal(Paid, profile);

        var denied = ModelProfileResolver.TryResolve(
            null,
            Paid.QualifiedName,
            ModelCatalog.DefaultProfileName,
            FreeOnly,
            out _,
            out var code,
            out _);

        Assert.False(denied);
        Assert.Equal("MODEL_NOT_ALLOWED", code);
    }

    [Fact]
    public void Profile_and_hint_together_are_rejected()
    {
        var ok = ModelProfileResolver.TryResolve(
            ModelCatalog.Free.Name,
            ModelCatalog.Free.QualifiedName,
            ModelCatalog.DefaultProfileName,
            FreeOnly,
            out _,
            out var code,
            out _);

        Assert.False(ok);
        Assert.Equal("CONTRACT_INVALID", code);
    }

    [Fact]
    public void Missing_default_is_reported()
    {
        var ok = ModelProfileResolver.TryResolve(
            null, null, "missing", FreeOnly, out _, out var code, out _);

        Assert.False(ok);
        Assert.Equal("MODEL_NOT_CONFIGURED", code);
    }
}
