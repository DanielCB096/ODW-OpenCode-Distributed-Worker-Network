using Odw.Node.Configuration;

namespace Odw.Node.Hosting;

/// <summary>
/// Guardas de configuración que evitan declarar aislamiento cuando la topología lo rompe.
/// Todas las tareas comparten el SID del agente: dos workspaces vivos a la vez son visibles
/// entre sí, y un workspace conservado queda legible por tareas posteriores.
/// </summary>
public static class SandboxGuard
{
    public static void Validate(
        SandboxConfig sandbox,
        SchedulerConfig scheduler,
        WorkspacesConfig workspaces)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(workspaces);

        if (!sandbox.RunAsAgent)
        {
            return;
        }

        if (scheduler.Enabled && scheduler.MaxConcurrent > 1)
        {
            throw new NodeConfigurationException(
                "sandbox.run_as_agent comparte el SID entre tareas: pon scheduler.max_concurrent=1 "
                + "o desactiva el agente restringido. Con concurrencia, una tarea puede leer y "
                + "escribir el workspace de otra.");
        }

        if (workspaces.Keep)
        {
            throw new NodeConfigurationException(
                "sandbox.run_as_agent con workspaces.keep=true deja workspaces legibles por tareas "
                + "posteriores (mismo SID). Usa keep=false o desactiva el agente restringido.");
        }
    }
}
