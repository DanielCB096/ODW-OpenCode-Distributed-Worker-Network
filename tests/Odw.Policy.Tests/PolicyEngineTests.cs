using Odw.Core.Contracts;
using Odw.Core.Model;
using Xunit;

namespace Odw.Policy.Tests;

public class PolicyEngineTests
{
    private static readonly TimeZoneInfo PlusTwo = TimeZoneInfo.CreateCustomTimeZone(
        "Test/Plus2", TimeSpan.FromHours(2), "Test +2", "Test +2");

    [Fact]
    public void Lower_priority_number_wins()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"auto-late","priority":30,"effect":"AUTO","reason":"auto"},
            {"id":"deny-first","priority":10,"effect":"DENY","reason":"deny"}
            """));

        var decision = engine.Evaluate(PolicyFixtures.Request());

        Assert.Equal(PolicyEffect.Deny, decision.Effect);
        Assert.Equal("deny-first", decision.MatchedRuleId);
    }

    [Fact]
    public void Ties_use_declaration_order()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"first-auto","priority":10,"effect":"AUTO","reason":"primera"},
            {"id":"second-deny","priority":10,"effect":"DENY","reason":"segunda"}
            """));

        var decision = engine.Evaluate(PolicyFixtures.Request());

        Assert.Equal(PolicyEffect.Auto, decision.Effect);
        Assert.Equal("first-auto", decision.MatchedRuleId);
    }

    [Fact]
    public void Default_applies_when_nothing_matches()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"solo-code","priority":10,"effect":"AUTO","reason":"x",
             "when":{"kind":"code_change"}}
            """, "ASK"));

        var decision = engine.Evaluate(PolicyFixtures.Request());

        Assert.Equal(PolicyEffect.Ask, decision.Effect);
        Assert.Equal("default", decision.MatchedRuleId);
    }

    [Fact]
    public void Direction_filters_in_and_out()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"out-deny","priority":10,"effect":"DENY","reason":"saliente",
             "when":{"direction":"out"}}
            """));

        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(PolicyFixtures.Request(role: TaskRole.Target)).Effect);
        Assert.Equal(PolicyEffect.Deny, engine.Evaluate(PolicyFixtures.Request(role: TaskRole.Origin)).Effect);
    }

    [Fact]
    public void Kind_and_mode_accept_string_or_array()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"analisis","priority":10,"effect":"AUTO","reason":"x",
             "when":{"kind":["analysis","test_run"],"mode":"read_only"}}
            """));

        Assert.Equal(PolicyEffect.Auto, engine.Evaluate(PolicyFixtures.Request()).Effect);

        var writable = PolicyFixtures.Request(PolicyFixtures.TaskRequest(mode: WorkspaceMode.ReadWriteWorktree));
        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(writable).Effect);
    }

    [Fact]
    public void Peer_and_trust_are_checked()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"par-conocido","priority":10,"effect":"AUTO","reason":"x",
             "when":{"peer":"PC-A","peer_trust":["trusted","limited"]}}
            """));

        Assert.Equal(PolicyEffect.Auto, engine.Evaluate(PolicyFixtures.Request()).Effect);
        Assert.Equal(
            PolicyEffect.Ask,
            engine.Evaluate(PolicyFixtures.Request(trust: "blocked")).Effect);
    }

    [Fact]
    public void Repo_is_checked()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"solo-repo","priority":10,"effect":"AUTO","reason":"x",
             "when":{"repo":["SistemaMaestro","Otro"]}}
            """));

        Assert.Equal(PolicyEffect.Auto, engine.Evaluate(PolicyFixtures.Request()).Effect);
        Assert.Equal(
            PolicyEffect.Ask,
            engine.Evaluate(PolicyFixtures.Request(PolicyFixtures.TaskRequest(repo: "Tercero"))).Effect);
    }

    [Fact]
    public void Limits_and_network_are_checked()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"limitada","priority":10,"effect":"AUTO","reason":"x",
             "when":{"limits":{"max_minutes":{"lte":60,"gte":10},
                               "max_memory_gb":{"gte":8},
                               "max_cost_usd":{"lte":10},
                               "network":"none"}}}
            """));

        Assert.Equal(PolicyEffect.Auto, engine.Evaluate(PolicyFixtures.Request()).Effect);
        Assert.Equal(
            PolicyEffect.Ask,
            engine.Evaluate(PolicyFixtures.Request(PolicyFixtures.TaskRequest(minutes: 90))).Effect);
        Assert.Equal(
            PolicyEffect.Ask,
            engine.Evaluate(PolicyFixtures.Request(PolicyFixtures.TaskRequest(network: NetworkAccess.Allow))).Effect);
    }

    [Fact]
    public void Capabilities_all_and_none_are_checked()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"capaz","priority":10,"effect":"AUTO","reason":"x",
             "when":{"capabilities_all":["git","dotnet"],"capabilities_none":["gpu"]}}
            """));

        Assert.Equal(PolicyEffect.Auto, engine.Evaluate(PolicyFixtures.Request()).Effect);
        Assert.Equal(
            PolicyEffect.Ask,
            engine.Evaluate(PolicyFixtures.Request(capabilities: ["git", "dotnet", "gpu"])).Effect);
        Assert.Equal(
            PolicyEffect.Ask,
            engine.Evaluate(PolicyFixtures.Request(capabilities: ["git"])).Effect);
    }

    [Fact]
    public void Parent_is_checked()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"sub","priority":10,"effect":"DENY","reason":"x","when":{"parent":true}}
            """));

        Assert.Equal(PolicyEffect.Deny, engine.Evaluate(
            PolicyFixtures.Request(PolicyFixtures.TaskRequest(parent: "01JAZ6AAAAAAAAAAAAAAAAAAAAA"))).Effect);
        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(PolicyFixtures.Request()).Effect);
    }

    [Fact]
    public void Any_action_matches_request_actions()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"peligrosa","priority":10,"effect":"DENY","reason":"x",
             "when":{"any_action":["shell","publish"]}}
            """));

        Assert.Equal(PolicyEffect.Deny, engine.Evaluate(
            PolicyFixtures.Request(actions: ["shell"])).Effect);
        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(
            PolicyFixtures.Request(actions: ["read"])).Effect);
        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(PolicyFixtures.Request()).Effect);
    }

    [Fact]
    public void Time_window_same_day_uses_timezone()
    {
        var engine = PolicyEngine.FromJson(
            PolicyFixtures.Document(
                """
                {"id":"horario","priority":10,"effect":"DENY","reason":"x",
                 "when":{"time_window":{"days":[1,2,3,4,5,6,7],"from":"09:00","to":"18:00","tz":"Test/Plus2"}}}
                """),
            _ => PlusTwo);

        var inside = new DateTimeOffset(2026, 11, 24, 15, 0, 0, TimeSpan.Zero);   // 17:00 local
        var outside = new DateTimeOffset(2026, 11, 24, 16, 30, 0, TimeSpan.Zero); // 18:30 local

        Assert.Equal(PolicyEffect.Deny, engine.Evaluate(PolicyFixtures.Request(now: inside)).Effect);
        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(PolicyFixtures.Request(now: outside)).Effect);
    }

    [Fact]
    public void Time_window_can_cross_midnight()
    {
        var engine = PolicyEngine.FromJson(
            PolicyFixtures.Document(
                """
                {"id":"noche","priority":10,"effect":"DENY","reason":"x",
                 "when":{"time_window":{"days":[5],"from":"22:00","to":"02:00","tz":"UTC"}}}
                """),
            _ => TimeZoneInfo.Utc);

        var fridayLate = new DateTimeOffset(2026, 11, 27, 23, 0, 0, TimeSpan.Zero);
        var saturdayEarly = new DateTimeOffset(2026, 11, 28, 1, 0, 0, TimeSpan.Zero);
        var saturdayNoon = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(PolicyEffect.Deny, engine.Evaluate(PolicyFixtures.Request(now: fridayLate)).Effect);
        Assert.Equal(PolicyEffect.Deny, engine.Evaluate(PolicyFixtures.Request(now: saturdayEarly)).Effect);
        Assert.Equal(PolicyEffect.Ask, engine.Evaluate(PolicyFixtures.Request(now: saturdayNoon)).Effect);
    }

    [Fact]
    public void Decision_is_explainable_and_deterministic()
    {
        var engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """
            {"id":"regla","priority":10,"effect":"AUTO","reason":"porque sí"}
            """));

        var first = engine.Evaluate(PolicyFixtures.Request());
        var second = engine.Evaluate(PolicyFixtures.Request());

        Assert.Equal("regla", first.MatchedRuleId);
        Assert.Equal("porque sí", first.Reason);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Engine_implements_the_core_contract()
    {
        IPolicyEngine engine = PolicyEngine.FromJson(PolicyFixtures.Document(
            """{"id":"ask","priority":10,"effect":"ASK","reason":"x"}"""));

        Assert.True(engine.Evaluate(PolicyFixtures.Request()).RequiresHuman);
    }

    public static IEnumerable<object[]> InvalidDocuments()
    {
        yield return ["""{"version":2,"default":"ASK","rules":[]}"""];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"dup","priority":1,"effect":"ASK","reason":"a"},
              {"id":"dup","priority":2,"effect":"ASK","reason":"b"}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"x1","priority":1,"effect":"MAYBE","reason":"a"}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"X","priority":1,"effect":"ASK","reason":"a"}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1001,"effect":"ASK","reason":"a"}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK"}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK","reason":"a","foo":1}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK","reason":"a","when":{"limits":{}}}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK","reason":"a",
               "when":{"limits":{"max_minutes":{"lte":5,"gte":10}}}}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK","reason":"a",
               "when":{"time_window":{"days":[1],"from":"9:00","to":"10:00"}}}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK","reason":"a",
               "when":{"time_window":{"days":[0],"from":"09:00","to":"10:00"}}}]}
            """,
        ];
        yield return
        [
            """
            {"version":1,"default":"ASK","rules":[
              {"id":"ok","priority":1,"effect":"ASK","reason":"a",
               "when":{"time_window":{"days":[1],"from":"09:00","to":"10:00","tz":"Nope/Zone"}}}]}
            """,
        ];
    }

    [Theory]
    [MemberData(nameof(InvalidDocuments))]
    public void Invalid_documents_are_rejected(string json)
    {
        var exception = Assert.Throws<PolicyValidationException>(() => PolicyEngine.FromJson(json));

        Assert.NotEmpty(exception.Issues);
    }

    [Fact]
    public void Example_configuration_loads_and_evaluates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "policy.example.json");
        var engine = PolicyEngine.FromFile(path);

        Assert.NotEmpty(engine.Document.Rules);

        var analysis = engine.Evaluate(PolicyFixtures.Request());
        Assert.Equal(PolicyEffect.Auto, analysis.Effect);
        Assert.Equal("auto-analysis-trusted", analysis.MatchedRuleId);

        var outgoing = engine.Evaluate(PolicyFixtures.Request(
            role: TaskRole.Origin,
            task: PolicyFixtures.TaskRequest(minutes: 90)));
        Assert.Equal(PolicyEffect.Ask, outgoing.Effect);
        Assert.Equal("ask-own-delegation", outgoing.MatchedRuleId);
    }
}
