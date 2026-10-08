using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SatPlatform.Services.Telegram;

public class TelegramOptions
{
    public string? BotToken { get; set; }
    public string? BotUsername { get; set; }
}

/// <summary>Minimal Telegram Bot API client (getUpdates / sendMessage / getMe) built on HttpClient.</summary>
public class TelegramBotClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramBotClient> logger)
{
    private readonly string? _token = options.Value.BotToken?.Trim();
    private string? _username = options.Value.BotUsername?.TrimStart('@');

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_token);
    public string? BotUsername => _username;

    private string Url(string method) => $"https://api.telegram.org/bot{_token}/{method}";

    public async Task<string?> GetMeAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            using var resp = await http.GetAsync(Url("getMe"), ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var name = doc.RootElement.GetProperty("result").GetProperty("username").GetString();
            if (!string.IsNullOrEmpty(name)) _username = name;
            return name;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Telegram getMe failed");
            return null;
        }
    }

    public async Task<bool> SendMessageAsync(long chatId, string text, object? replyMarkup = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;
        var payload = new Dictionary<string, object?> { ["chat_id"] = chatId, ["text"] = text, ["parse_mode"] = "HTML", ["disable_web_page_preview"] = true };
        if (replyMarkup is not null) payload["reply_markup"] = replyMarkup;
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(Url("sendMessage"), content, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Telegram sendMessage failed: {Status} {Body}", resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Telegram sendMessage error");
            return false;
        }
    }

    public async Task<List<TelegramUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct)
    {
        using var resp = await http.GetAsync(Url($"getUpdates?offset={offset}&timeout={timeoutSeconds}&allowed_updates=%5B%22message%22%5D"), ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var list = new List<TelegramUpdate>();
        if (!doc.RootElement.TryGetProperty("result", out var result)) return list;
        foreach (var u in result.EnumerateArray())
        {
            var update = new TelegramUpdate { UpdateId = u.GetProperty("update_id").GetInt64() };
            if (u.TryGetProperty("message", out var msg))
            {
                update.ChatId = msg.GetProperty("chat").GetInt64Property("id");
                update.Text = msg.TryGetProperty("text", out var t) ? t.GetString() : null;
                if (msg.TryGetProperty("from", out var from))
                {
                    update.FirstName = from.TryGetProperty("first_name", out var fn) ? fn.GetString() : null;
                    update.Username = from.TryGetProperty("username", out var un) ? un.GetString() : null;
                    update.FromId = from.GetInt64Property("id");
                }
                if (msg.TryGetProperty("contact", out var contact))
                {
                    update.ContactPhone = contact.TryGetProperty("phone_number", out var p) ? p.GetString() : null;
                    update.ContactUserId = contact.TryGetProperty("user_id", out var cu) ? cu.GetInt64() : null;
                }
            }
            list.Add(update);
        }
        return list;
    }
}

public class TelegramUpdate
{
    public long UpdateId { get; set; }
    public long ChatId { get; set; }
    public long FromId { get; set; }
    public string? Text { get; set; }
    public string? FirstName { get; set; }
    public string? Username { get; set; }
    public string? ContactPhone { get; set; }
    public long? ContactUserId { get; set; }
}

file static class JsonExt
{
    public static long GetInt64Property(this JsonElement el, string name) => el.TryGetProperty(name, out var p) ? p.GetInt64() : 0;
}
