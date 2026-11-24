using System.Threading.Channels;
using Microsoft.Data.Sqlite;

namespace Odw.Node.Persistence;

/// <summary>
/// Único escritor de la base (ADR-008). Cada comando se ejecuta en su propia transacción
/// <c>BEGIN</c>…<c>COMMIT</c>; si falla, se revierte y la conexión sigue utilizable.
/// </summary>
public sealed class SqliteWriter : IAsyncDisposable
{
    private sealed class WriteCommand
    {
        public required Func<SqliteConnection, SqliteTransaction, object?> Work { get; init; }

        public required TaskCompletionSource<object?> Completion { get; init; }

        public CancellationToken CancellationToken { get; init; }
    }

    private readonly SqliteConnection _connection;
    private readonly Channel<WriteCommand> _channel;
    private readonly Task _loop;
    private long _committed;
    private long _failed;
    private bool _disposed;

    internal SqliteWriter(SqliteConnection connection, int capacity = 1024)
    {
        _connection = connection;
        _channel = Channel.CreateBounded<WriteCommand>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        });
        _loop = Task.Run(RunAsync);
    }

    public int QueueDepth => _channel.Reader.Count;

    public long CommittedCount => Interlocked.Read(ref _committed);

    public long FailedCount => Interlocked.Read(ref _failed);

    public ValueTask<T> WriteAsync<T>(
        Func<SqliteConnection, SqliteTransaction, T> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        var completion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var command = new WriteCommand
        {
            Work = (connection, transaction) => work(connection, transaction),
            Completion = completion,
            CancellationToken = cancellationToken,
        };

        if (_channel.Writer.TryWrite(command))
        {
            return new ValueTask<T>(Unwrap<T>(completion.Task));
        }

        return WriteSlowAsync<T>(command, completion, cancellationToken);
    }

    public async ValueTask WriteAsync(
        Action<SqliteConnection, SqliteTransaction> work,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        await WriteAsync<object?>((connection, transaction) =>
        {
            work(connection, transaction);
            return null;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<T> WriteSlowAsync<T>(
        WriteCommand command,
        TaskCompletionSource<object?> completion,
        CancellationToken cancellationToken)
    {
        await _channel.Writer.WriteAsync(command, cancellationToken).ConfigureAwait(false);
        return await Unwrap<T>(completion.Task).ConfigureAwait(false);
    }

    private static async Task<T> Unwrap<T>(Task<object?> task)
    {
        var value = await task.ConfigureAwait(false);
        return (T)value!;
    }

    private async Task RunAsync()
    {
        await foreach (var command in _channel.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (command.CancellationToken.IsCancellationRequested)
            {
                command.Completion.TrySetCanceled(command.CancellationToken);
                continue;
            }

            SqliteTransaction? transaction = null;
            try
            {
                transaction = _connection.BeginTransaction(deferred: false);
                var result = command.Work(_connection, transaction);
                transaction.Commit();
                Interlocked.Increment(ref _committed);
                command.Completion.TrySetResult(result);
            }
            catch (Exception exception)
            {
                try
                {
                    transaction?.Rollback();
                }
                catch
                {
                    // La conexión se descarta en la siguiente operación; el error original es el relevante.
                }

                Interlocked.Increment(ref _failed);
                command.Completion.TrySetException(exception);
            }
            finally
            {
                transaction?.Dispose();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _channel.Writer.TryComplete();
        await _loop.ConfigureAwait(false);
    }
}
