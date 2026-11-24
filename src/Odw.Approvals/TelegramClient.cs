using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Odw.Approvals;

public sealed record TelegramCallback(
    long UpdateId,
    long UserId,
    long ChatId,
    string CallbackId,
    string Data);

/// <summary>Cliente mínimo del Bot API (long polling y teclado inline).</summary>
public sealed class TelegramClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly bool _ownsHttp;

    public TelegramClient(string token, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _ownsHttp = httpClient is null;
    }

    public async Task<IReadOnlyList<TelegramCallback>> GetUpdatesAsync(
        long offset,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["offset"] = offset,
            ["timeout"] = Math.Clamp(timeoutSeconds, 1, 50),
            ["allowed_updates"] = new JsonArray("callback_query"),
        };

        using var response = await _http.PostAsJsonAsync(
            $"https://api.telegram.org/bot{_token}/getUpdates", body, cancellationToken)
            .ConfigureAwait(false);

        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Telegram getUpdates: {(int)response.StatusCode} {text}");
        }

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (!root.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            throw new InvalidOperationException("Telegram getUpdates devolvió ok=false.");
        }

        var callbacks = new List<TelegramCallback>();

        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
        {
            return callbacks;
        }

        foreach (var update in result.EnumerateArray())
        {
            if (!update.TryGetProperty("update_id", out var updateId) || !updateId.TryGetInt64(out var id))
            {
                continue;
            }

            if (!update.TryGetProperty("callback_query", out var callback))
            {
                continue;
            }

            var userId = callback.TryGetProperty("from", out var from)
                         && from.TryGetProperty("id", out var user)
                         && user.TryGetInt64(out var userValue)
                ? userValue
                : 0;

            var chatId = callback.TryGetProperty("message", out var message)
                         && message.TryGetProperty("chat", out var chat)
                         && chat.TryGetProperty("id", out var chatValue)
                         && chatValue.TryGetInt64(out var chatNumber)
                ? chatNumber
                : 0;

            var callbackId = callback.TryGetProperty("id", out var cb) && cb.ValueKind == JsonValueKind.String
                ? cb.GetString()!
                : string.Empty;

            var data = callback.TryGetProperty("data", out var payload) && payload.ValueKind == JsonValueKind.String
                ? payload.GetString()!
                : string.Empty;

            if (callbackId.Length > 0 && data.Length > 0)
            {
                callbacks.Add(new TelegramCallback(id, userId, chatId, callbackId, data));
            }
        }

        return callbacks;
    }

    public async Task SendApprovalAsync(
        long chatId,
        string text,
        string approveData,
        string denyData,
        CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["chat_id"] = chatId,
            ["text"] = text,
            ["disable_web_page_preview"] = true,
            ["reply_markup"] = new JsonObject
            {
                ["inline_keyboard"] = new JsonArray(
                    new JsonArray(
                        new JsonObject { ["text"] = "Aprobar", ["callback_data"] = approveData },
                        new JsonObject { ["text"] = "Denegar", ["callback_data"] = denyData })),
            },
        };

        await PostAsync("sendMessage", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task AnswerCallbackAsync(
        string callbackId,
        string text,
        CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["callback_query_id"] = callbackId,
            ["text"] = text,
            ["show_alert"] = false,
        };

        await PostAsync("answerCallbackQuery", body, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }

    private async Task PostAsync(string method, JsonObject body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(
            $"https://api.telegram.org/bot{_token}/{method}", body, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Telegram {method}: {(int)response.StatusCode} {text}");
        }
    }
}
