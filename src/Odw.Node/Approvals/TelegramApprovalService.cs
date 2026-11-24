using Odw.Approvals;

namespace Odw.Node.Approvals;

public sealed record TelegramApprovalOptions
{
    public required string BotToken { get; init; }

    public required IReadOnlyList<long> AllowedUserIds { get; init; }

    public required IReadOnlyList<long> AllowedChatIds { get; init; }

    public int PollTimeoutSeconds { get; init; } = 20;

    public TimeSpan NotifyInterval { get; init; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Notifica aprobaciones pendientes por Telegram y procesa los botones con allowlist.
/// Los callbacks se aplican con <see cref="ApprovalCoordinator"/> (hash + un solo uso).
/// </summary>
public sealed class TelegramApprovalService : IAsyncDisposable
{
    private readonly ApprovalService _approvals;
    private readonly ApprovalCoordinator _coordinator;
    private readonly TelegramClient _client;
    private readonly TelegramApprovalOptions _options;
    private readonly Action<string>? _log;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private long _offset;
    private bool _disposed;

    public TelegramApprovalService(
        ApprovalService approvals,
        ApprovalCoordinator coordinator,
        TelegramClient client,
        TelegramApprovalOptions options,
        Action<string>? log = null)
    {
        _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log;

        if (options.AllowedUserIds.Count == 0 || options.AllowedChatIds.Count == 0)
        {
            throw new ArgumentException("Telegram requiere allowlist de usuarios y chats.", nameof(options));
        }
    }

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _loop = Task.Run(() => LoopAsync(_stopping.Token));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping.Cancel();

        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _client.Dispose();
        _stopping.Dispose();
    }

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await NotifyPendingAsync(cancellationToken).ConfigureAwait(false);
                await PollAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _log?.Invoke($"telegram: {exception.Message}");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task NotifyPendingAsync(CancellationToken cancellationToken)
    {
        var pending = _approvals.ListPending()
            .Where(record => record.NotifiedAt is null)
            .ToArray();

        if (pending.Length == 0)
        {
            return;
        }

        var notified = new List<string>(pending.Length);

        foreach (var record in pending)
        {
            foreach (var chatId in _options.AllowedChatIds)
            {
                var shortHash = record.TaskHash.Length > 15 ? record.TaskHash[7..15] : record.TaskHash;

                var text =
                    $"ODW: aprobación pendiente\n{record.Summary}\n"
                    + $"task: {record.TaskId}\nhash: {shortHash}\n"
                    + $"expira: {record.ExpiresAt:yyyy-MM-dd HH:mm}Z";

                await _client.SendApprovalAsync(
                    chatId,
                    text,
                    $"approve:{record.ApprovalId}",
                    $"deny:{record.ApprovalId}",
                    cancellationToken).ConfigureAwait(false);
            }

            notified.Add(record.ApprovalId);
        }

        await _approvals.MarkNotifiedAsync(notified, cancellationToken).ConfigureAwait(false);
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        var updates = await _client.GetUpdatesAsync(
            _offset,
            _options.PollTimeoutSeconds,
            cancellationToken).ConfigureAwait(false);

        foreach (var update in updates)
        {
            _offset = Math.Max(_offset, update.UpdateId + 1);

            if (!_options.AllowedUserIds.Contains(update.UserId)
                || !_options.AllowedChatIds.Contains(update.ChatId))
            {
                continue;
            }

            var separator = update.Data.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var decision = update.Data[..separator];
            var approvalId = update.Data[(separator + 1)..];
            var approver = $"human:telegram:{update.UserId}";

            string answer;
            try
            {
                answer = decision switch
                {
                    "approve" => (await _coordinator.ApproveAsync(
                        approvalId, approver, cancellationToken).ConfigureAwait(false))
                        .Status == ApprovalDecisionStatus.Approved ? "Aprobado" : "No se pudo aprobar",
                    "deny" => (await _coordinator.DenyAsync(
                        approvalId, approver, "telegram", cancellationToken).ConfigureAwait(false))
                        .Status == ApprovalDecisionStatus.Denied ? "Denegado" : "No se pudo denegar",
                    _ => "Comando inválido",
                };
            }
            catch (Exception exception)
            {
                _log?.Invoke($"telegram: {exception.Message}");
                answer = "Error";
            }

            await _client.AnswerCallbackAsync(update.CallbackId, answer, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
