using System.Security.Cryptography;
using System.Text;

namespace Odw.Sandbox.Windows;

/// <summary>Lee un secreto protegido con DPAPI de máquina (instalador/servicio elevado).</summary>
public static class DpapiSecret
{
    public static string Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI solo existe en Windows.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("No existe el secreto.", path);
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var bytes = ProtectedData.Unprotect(
                protectedBytes,
                optionalEntropy: null,
                DataProtectionScope.LocalMachine);

            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("No se pudo descifrar el secreto DPAPI.", exception);
        }
    }
}
