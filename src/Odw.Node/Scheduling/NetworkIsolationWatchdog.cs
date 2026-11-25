namespace Odw.Node.Scheduling;

/// <summary>
/// Comprueba periódicamente que el aislamiento de red sigue activo durante la tarea. Si WFP
/// desaparece (un administrador o un producto de seguridad lo retira), cancela la ejecución.
/// </summary>
public sealed class NetworkIsolationWatchdog : IAsyncDisposable
{
    private readonly Func<bool> _probe;
    private readonly TimeSpan _interval;
    private readonly Action _onLost;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;
    private int _lost;

    public NetworkIsolationWatchdog(Func<bool> probe, TimeSpan interval, Action onLost)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(onLost);

        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        _probe = probe;
        _interval = interval;
        _onLost = onLost;
        _loop = Task.Run(LoopAsync);
    }

    public bool Lost => Volatile.Read(ref _lost) == 1;

    public async ValueTask DisposeAsync()
    {
        _stopping.Cancel();

        try
        {
            await _loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _stopping.Dispose();
    }

    private async Task LoopAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var enforced = false;
            try
            {
                enforced = _probe();
            }
            catch (Exception)
            {
                enforced = false;
            }

            if (!enforced)
            {
                if (Interlocked.Exchange(ref _lost, 1) == 0)
                {
                    _onLost();
                }

                return;
            }
        }
    }
}
