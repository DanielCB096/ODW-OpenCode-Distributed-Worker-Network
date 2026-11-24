namespace Odw.Node.Approvals;

public sealed record ApprovalOptions
{
    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(15);
}
