using System.Net;

namespace Odw.Node.Diagnostics;

public enum DoctorStatus
{
    Pass,
    Warn,
    Fail,
}

public sealed record DoctorCheck(string Name, DoctorStatus Status, string Detail);

public sealed record DoctorReport(IReadOnlyList<DoctorCheck> Checks)
{
    public bool Ok => Checks.All(check => check.Status != DoctorStatus.Fail);

    public int Failures => Checks.Count(check => check.Status == DoctorStatus.Fail);

    public int Warnings => Checks.Count(check => check.Status == DoctorStatus.Warn);
}

public sealed record DoctorOptions
{
    public required string DatabasePath { get; init; }

    public string PeerBind { get; init; } = "tailscale";

    public int PeerPort { get; init; } = 7421;

    public string McpBind { get; init; } = "127.0.0.1";

    public int McpPort { get; init; } = 7422;

    public string? McpToken { get; init; }

    public string? OpenCodeExecutable { get; init; }

    public string? PinnedOpenCodeVersion { get; init; }

    public string? SpecPath { get; init; }

    public string? ExpectedSpecSha256 { get; init; }

    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}

/// <summary>Reglas de bind de ADR-002, compartidas por el doctor y el host.</summary>
public static class BindPolicy
{
    public static bool IsTailnetBind(string? bind)
    {
        if (string.IsNullOrWhiteSpace(bind))
        {
            return false;
        }

        var value = bind.Trim();

        if (value.Equals("tailscale", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.EndsWith(".ts.net", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".tailscale.net", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(value, out var address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // 100.64.0.0/10
            return bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
        }

        // fd7a:115c:a1e0::/48
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
               && bytes[0] == 0xfd && bytes[1] == 0x7a && bytes[2] == 0x11 && bytes[3] == 0x5c
               && bytes[4] == 0xa1 && bytes[5] == 0xe0;
    }

    public static bool IsLoopbackBind(string? bind)
    {
        if (string.IsNullOrWhiteSpace(bind))
        {
            return false;
        }

        var value = bind.Trim();
        if (value.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IPAddress.TryParse(value, out var address))
        {
            return false;
        }

        if (address.Equals(IPAddress.IPv6Loopback))
        {
            return true;
        }

        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
               && address.GetAddressBytes()[0] == 127;
    }

    public static bool IsAnyAddress(string bind)
    {
        var value = bind.Trim();
        return value is "*" or "0.0.0.0" or "::"
               || (IPAddress.TryParse(value, out var address) && address.Equals(IPAddress.Any))
               || (IPAddress.TryParse(value, out address) && address.Equals(IPAddress.IPv6Any));
    }
}
