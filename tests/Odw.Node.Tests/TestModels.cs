namespace Odw.Node.Tests;

internal static class TestModels
{
    /// <summary>
    /// Modelo para las pruebas de integración. Por defecto uno gratuito.
    /// Si no está disponible, se puede fijar ODW_TEST_MODEL (p. ej. el fallback acordado).
    /// </summary>
    public static string FreeOrFallback =>
        Environment.GetEnvironmentVariable("ODW_TEST_MODEL") ?? "opencode/deepseek-v4.1-flash-free";
}
