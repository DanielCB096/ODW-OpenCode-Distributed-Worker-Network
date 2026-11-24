using Odw.Core.Model;
using Odw.Node.Approvals;
using Xunit;

namespace Odw.Node.Tests;

public class ApprovalServiceTests
{
    private static ApprovalService Create(TestDatabase fixture)
        => new(fixture.Database, fixture.Time);

    private static Task<ApprovalRecord> CreateRequest(ApprovalService service, string? hash = null)
        => service.CreateAsync(
            TestRows.NewId(),
            TaskRole.Origin,
            hash ?? "sha256:" + new string('a', 64),
            "analysis read_only SistemaMaestro: analizar",
            TimeSpan.FromMinutes(15));

    [Fact]
    public async Task Create_lists_pending_and_audits()
    {
        await using var fixture = TestDatabase.Create();
        var service = Create(fixture);

        var record = await CreateRequest(service);

        Assert.Equal(ApprovalStatus.Pending, record.Status);
        Assert.False(string.IsNullOrWhiteSpace(record.Nonce));
        Assert.Single(service.ListPending());

        using var connection = fixture.Database.OpenReadConnection();
        Assert.Equal(1L, MigrationTests.ScalarLong(
            connection,
            "SELECT COUNT(*) FROM audit_log WHERE entry_type = 'approval.requested';"));
    }

    [Fact]
    public async Task Decision_is_bound_to_the_hash()
    {
        await using var fixture = TestDatabase.Create();
        var service = Create(fixture);
        var record = await CreateRequest(service);

        var mismatch = await service.DecideAsync(
            record.ApprovalId, true, "human:cli:test", null, "sha256:" + new string('b', 64));

        Assert.Equal(ApprovalDecisionStatus.HashMismatch, mismatch.Status);
        Assert.Single(service.ListPending());

        var approved = await service.DecideAsync(
            record.ApprovalId, true, "human:cli:test", null, record.TaskHash);

        Assert.Equal(ApprovalDecisionStatus.Approved, approved.Status);
        Assert.Equal("human:cli:test", approved.Record!.DecidedBy);
        Assert.Empty(service.ListPending());
        Assert.True(await service.ConsumeAsync(record.ApprovalId));
        Assert.False(await service.ConsumeAsync(record.ApprovalId));
    }

    [Fact]
    public async Task Second_decision_is_rejected()
    {
        await using var fixture = TestDatabase.Create();
        var service = Create(fixture);
        var record = await CreateRequest(service);

        await service.DecideAsync(record.ApprovalId, false, "human:cli:test", "no");
        var second = await service.DecideAsync(record.ApprovalId, true, "human:cli:test", null);

        Assert.Equal(ApprovalDecisionStatus.AlreadyDecided, second.Status);
    }

    [Fact]
    public async Task Expired_approval_cannot_be_decided()
    {
        await using var fixture = TestDatabase.Create();
        var service = Create(fixture);

        var record = await service.CreateAsync(
            TestRows.NewId(),
            TaskRole.Origin,
            "sha256:" + new string('a', 64),
            "resumen",
            TimeSpan.FromSeconds(1));

        fixture.Time.Advance(TimeSpan.FromSeconds(2));

        var result = await service.DecideAsync(record.ApprovalId, true, "human:cli:test", null, record.TaskHash);

        Assert.Equal(ApprovalDecisionStatus.Expired, result.Status);
        Assert.Empty(service.ListPending());
        Assert.Equal(ApprovalStatus.Expired, service.Get(record.ApprovalId)!.Status);
    }

    [Fact]
    public async Task Unknown_approval_is_not_found()
    {
        await using var fixture = TestDatabase.Create();

        var result = await Create(fixture).DecideAsync("01JAZ5Z8Q4XK3V9C2M1N7P6R8T", true, "human:cli:test", null);

        Assert.Equal(ApprovalDecisionStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Decisions_are_signed_and_tamper_evident()
    {
        await using var fixture = TestDatabase.Create();
        var signer = new ApprovalSigner(new byte[32]);
        var service = new ApprovalService(fixture.Database, fixture.Time, signer);
        var record = await CreateRequest(service);

        var approved = await service.DecideAsync(
            record.ApprovalId, true, "human:cli:test", null, record.TaskHash);

        Assert.Equal(ApprovalDecisionStatus.Approved, approved.Status);
        Assert.StartsWith("hmac-sha256:", approved.Record!.Signature);
        Assert.True(service.Verify(approved.Record));

        var tampered = approved.Record with { Status = ApprovalStatus.Denied };
        Assert.False(service.Verify(tampered));

        var other = new ApprovalSigner(Enumerable.Repeat((byte)7, 32).ToArray());
        Assert.False(other.Verify(approved.Record));
    }
}
