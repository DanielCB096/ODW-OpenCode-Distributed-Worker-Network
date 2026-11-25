using System.Security.Principal;

namespace Odw.Sandbox.Windows;

public sealed record SandboxReport(
    bool IsWindows,
    bool IsAdministrator,
    bool AgentAccountExists,
    bool AclsSupported,
    string Detail)
{
    public bool FullyEnforced => IsWindows && IsAdministrator && AgentAccountExists;
}

/// <summary>Comprueba si el equipo puede aplicar el aislamiento real de H3.</summary>
public static class SandboxInspector
{
    public static SandboxReport Inspect(string agentAccount = "odw-agent")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentAccount);

        if (!OperatingSystem.IsWindows())
        {
            return new SandboxReport(false, false, false, false,
                "El sandbox de H3 requiere Windows (usuario restringido y ACLs).");
        }

        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        var admin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        var exists = AccountExists(agentAccount);

        var detail = (admin, exists) switch
        {
            (false, false) =>
                $"Falta '{agentAccount}' y la sesión no es de administrador: no se puede provisionar "
                + "el usuario ni aplicar el aislamiento real.",
            (false, true) =>
                $"'{agentAccount}' existe, pero la sesión no es de administrador: faltan "
                + "SeAssignPrimaryToken/SeIncreaseQuota y la regla de red.",
            (true, false) =>
                $"Sesión elevada: falta crear '{agentAccount}' y la regla de red.",
            _ => "Usuario y ACLs listos. Red sin aislar (firewall por usuario no soportado) y "
                 + "OpenCode todavía no se lanza con el token del agente.",
        };

        return new SandboxReport(true, admin, exists, true, detail);
    }

    public static bool AccountExists(string account)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(account))
        {
            return false;
        }

        var value = account.Trim().TrimStart('*');

        try
        {
            _ = new SecurityIdentifier(value);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IdentityNotMappedException)
        {
        }

        try
        {
            _ = new NTAccount(value).Translate(typeof(SecurityIdentifier));
            return true;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
    }
}
