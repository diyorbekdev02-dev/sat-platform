using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SatPlatform.Services.MentorBot;

/// <summary>Telegram Bot API client for the mentor bot. The token is set at runtime (from the admin "Bot" page).</summary>
public class MentorBotClient(HttpClient http, ILogger<MentorBotClient> logger)
{
    private volatile string? _token;

    public string? Token => _token;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_token);
    public void SetToken(string? token) => _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();

    private string Url(string method) => $"https://api.telegram.org/bot{_token}/{method}";

    /// <summary>Calls a Bot API method; returns the "result" node or null on failure.</summary>
    public async Task<JsonNode?> CallAsync(string method, object payload, CancellationToken ct = default)
    {
        if (!IsConfigured) return null;
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(Url(method), content, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                // "message is not modified" is harmless when re-rendering a keyboard
                if (!body.Contains("not modified")) logger.LogWarning("Mentor bot {Method} failed: {Status} {Body}", method, (int)resp.StatusCode, body);
                return null;
            }
            return JsonNode.Parse(body)?["result"];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Mentor bot {Method} error", method);
            return null;
        }
    }

    public static async Task<(string? Username, string? Error)> CheckTokenAsync(HttpClient http, string token, CancellationToken ct = default)
    {
        try
        {
            using var resp = await http.GetAsync($"https://api.telegram.org/bot{token.Trim()}/getMe", ct);
            if (!resp.IsSuccessStatusCode) return (null, "Token noto'g'ri (Telegram rad etdi).");
            var node = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
            return (node?["result"]?["username"]?.GetValue<string>(), null);
        }
        catch (Exception ex) { return (null, "Telegram bilan bog'lanib bo'lmadi: " + ex.Message); }
    }

    public async Task<string?> GetMeAsync(CancellationToken ct = default) => (await CallAsync("getMe", new { }, ct))?["username"]?.GetValue<string>();

    public async Task<long?> SendAsync(long chatId, string html, object? markup = null, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["chat_id"] = chatId, ["text"] = Trim(html), ["parse_mode"] = "HTML", ["link_preview_options"] = new { is_disabled = true } };
        if (markup is not null) payload["reply_markup"] = markup;
        return (await CallAsync("sendMessage", payload, ct))?["message_id"]?.GetValue<long>();
    }

    public Task EditAsync(long chatId, long messageId, string html, object? inlineMarkup = null, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?> { ["chat_id"] = chatId, ["message_id"] = messageId, ["text"] = Trim(html), ["parse_mode"] = "HTML", ["link_preview_options"] = new { is_disabled = true } };
        if (inlineMarkup is not null) payload["reply_markup"] = inlineMarkup;
        return CallAsync("editMessageText", payload, ct);
    }

    public Task AnswerCallbackAsync(string callbackId, string? text = null, CancellationToken ct = default) =>
        CallAsync("answerCallbackQuery", new { callback_query_id = callbackId, text }, ct);

    public async Task<long?> CopyAsync(long toChat, long fromChat, long messageId, CancellationToken ct = default) =>
        (await CallAsync("copyMessage", new { chat_id = toChat, from_chat_id = fromChat, message_id = messageId }, ct))?["message_id"]?.GetValue<long>();

    public async Task<bool> SendDocumentAsync(long chatId, byte[] data, string fileName, string? caption = null, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(chatId.ToString()), "chat_id");
            if (caption is not null) { form.Add(new StringContent(Trim(caption, 1000)), "caption"); form.Add(new StringContent("HTML"), "parse_mode"); }
            var file = new ByteArrayContent(data);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, "document", fileName);
            using var resp = await http.PostAsync(Url("sendDocument"), form, ct);
            if (!resp.IsSuccessStatusCode) logger.LogWarning("Mentor bot sendDocument failed: {Body}", await resp.Content.ReadAsStringAsync(ct));
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Mentor bot sendDocument error");
            return false;
        }
    }

    /// <summary>Downloads a file the user sent (photo / document) — up to 20 MB (Bot API limit).</summary>
    public async Task<byte[]?> DownloadAsync(string fileId, CancellationToken ct = default)
    {
        var path = (await CallAsync("getFile", new { file_id = fileId }, ct))?["file_path"]?.GetValue<string>();
        if (path is null) return null;
        try { return await http.GetByteArrayAsync($"https://api.telegram.org/file/bot{_token}/{path}", ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Mentor bot download failed"); return null; }
    }

    public async Task<List<BotUpdate>> GetUpdatesAsync(long offset, int timeoutSeconds, CancellationToken ct)
    {
        var url = Url($"getUpdates?offset={offset}&timeout={timeoutSeconds}&allowed_updates=%5B%22message%22,%22callback_query%22%5D");
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = new List<BotUpdate>();
        foreach (var u in root?["result"]?.AsArray() ?? [])
        {
            if (u is null) continue;
            var upd = new BotUpdate { UpdateId = u["update_id"]!.GetValue<long>() };
            if (u["message"] is JsonNode m)
            {
                upd.ChatId = m["chat"]?["id"]?.GetValue<long>() ?? 0;
                upd.PrivateChat = m["chat"]?["type"]?.GetValue<string>() == "private";
                upd.MessageId = m["message_id"]?.GetValue<long>() ?? 0;
                upd.Text = m["text"]?.GetValue<string>() ?? m["caption"]?.GetValue<string>();
                upd.FromId = m["from"]?["id"]?.GetValue<long>() ?? 0;
                upd.FirstName = m["from"]?["first_name"]?.GetValue<string>();
                upd.Username = m["from"]?["username"]?.GetValue<string>();
                if (m["contact"] is JsonNode c)
                {
                    upd.ContactPhone = c["phone_number"]?.GetValue<string>();
                    upd.ContactUserId = c["user_id"]?.GetValue<long>();
                }
                if (m["photo"] is JsonArray photos && photos.Count > 0)
                {
                    upd.FileId = photos[^1]?["file_id"]?.GetValue<string>();
                    upd.FileName = "rasm.jpg"; upd.MimeType = "image/jpeg"; upd.MediaKind = "photo";
                }
                else if (m["document"] is JsonNode d)
                {
                    upd.FileId = d["file_id"]?.GetValue<string>();
                    upd.FileName = d["file_name"]?.GetValue<string>() ?? "fayl";
                    upd.MimeType = d["mime_type"]?.GetValue<string>() ?? "application/octet-stream";
                    upd.FileSize = d["file_size"]?.GetValue<long>() ?? 0;
                    upd.MediaKind = "document";
                }
                else if (m["voice"] is not null) upd.MediaKind = "voice";
                else if (m["video"] is not null) upd.MediaKind = "video";
            }
            else if (u["callback_query"] is JsonNode q)
            {
                upd.CallbackId = q["id"]?.GetValue<string>();
                upd.CallbackData = q["data"]?.GetValue<string>();
                upd.FromId = q["from"]?["id"]?.GetValue<long>() ?? 0;
                upd.FirstName = q["from"]?["first_name"]?.GetValue<string>();
                upd.Username = q["from"]?["username"]?.GetValue<string>();
                upd.ChatId = q["message"]?["chat"]?["id"]?.GetValue<long>() ?? upd.FromId;
                upd.PrivateChat = (q["message"]?["chat"]?["type"]?.GetValue<string>() ?? "private") == "private";
                upd.MessageId = q["message"]?["message_id"]?.GetValue<long>() ?? 0;
            }
            list.Add(upd);
        }
        return list;
    }

    private static string Trim(string s, int max = 4000) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

public class BotUpdate
{
    public long UpdateId { get; set; }
    public long ChatId { get; set; }
    public bool PrivateChat { get; set; }
    public long MessageId { get; set; }
    public long FromId { get; set; }
    public string? Text { get; set; }
    public string? FirstName { get; set; }
    public string? Username { get; set; }
    public string? ContactPhone { get; set; }
    public long? ContactUserId { get; set; }
    public string? FileId { get; set; }
    public string? FileName { get; set; }
    public string? MimeType { get; set; }
    public long FileSize { get; set; }
    /// <summary>photo / document / voice / video, or null for plain text.</summary>
    public string? MediaKind { get; set; }
    public string? CallbackId { get; set; }
    public string? CallbackData { get; set; }

    public bool IsCallback => CallbackId is not null;
}
