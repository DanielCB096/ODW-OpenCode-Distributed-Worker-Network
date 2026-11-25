using System.Security.Cryptography;
using System.Text;

namespace Odw.Sandbox.Windows;

public sealed record AgentCredentials(string Account, string Password);

/// <summary>Lee las credenciales del agente desde el secreto DPAPI de máquina.</summary>
public static class AgentCredentialsStore
{
    public static AgentCredentials Load(string account, string secretPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretPath);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI solo existe en Windows.");
        }

        if (!File.Exists(secretPath))
        {
            throw new FileNotFoundException("No existe el secreto del agente.", secretPath);
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(secretPath);
            var bytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                DataProtectionScope.LocalMachine);

            var password = Encoding.UTF8.GetString(bytes);
            if (password.Length == 0)
            {
                throw new InvalidOperationException("El secreto del agente está vacío.");
            }

            return new AgentCredentials(account, password);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("No se pudo descifrar el secreto del agente.", exception);
        }
    }
}
