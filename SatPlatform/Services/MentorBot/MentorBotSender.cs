using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

/// <summary>Live status of the mentor bot poller (shown on the admin "Bot" page).</summary>
public class MentorBotStatus
{
    public bool Running { get; set; }
    public string? Username { get; set; }
    public string? LastError { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastUpdateAt { get; set; }
    public int HandledUpdates { get; set; }
}

/// <summary>Sends mentor-bot messages and writes them to the message log.</summary>
public partial class MentorBotSender(MentorBotClient bot, IDbContextFactory<AppDbContext> factory, ILogger<MentorBotSender> logger)
{
    public bool IsConfigured => bot.IsConfigured;
    public MentorBotClient Client => bot;

    public static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Plain text of an HTML message, for the log.</summary>
    public static string Plain(string html) => WebUtility.HtmlDecode(TagRegex().Replace(html, "")).Trim();

    public async Task<long?> SendAsync(long chatId, string html, object? markup = null, CancellationToken ct = default)
    {
        var id = await bot.SendAsync(chatId, html, markup, ct);
        await LogAsync(chatId, false, Plain(html) + (id is null ? "  ⚠ yuborilmadi" : ""));
        return id;
    }

    public async Task EditAsync(long chatId, long messageId, string html, object? inlineMarkup = null, CancellationToken ct = default)
    {
        await bot.EditAsync(chatId, messageId, html, inlineMarkup, ct);
        await LogAsync(chatId, false, "✎ " + Plain(html));
    }

    public async Task<bool> SendDocumentAsync(long chatId, byte[] data, string fileName, string? caption = null, CancellationToken ct = default)
    {
        var ok = await bot.SendDocumentAsync(chatId, data, fileName, caption, ct);
        await LogAsync(chatId, false, $"[fayl: {fileName}] {Plain(caption ?? "")}");
        return ok;
    }

    /// <summary>Sends to a platform user if they use the mentor bot. Returns false when they don't.</summary>
    public async Task<bool> SendToUserAsync(int userId, string html, object? markup = null, CancellationToken ct = default)
    {
        if (!bot.IsConfigured) return false;
        await using var db = await factory.CreateDbContextAsync(ct);
        var chats = await db.MentorBotChats.Where(c => c.UserId == userId && !c.IsBlocked).Select(c => c.ChatId).ToListAsync(ct);
        var any = false;
        foreach (var c in chats) any |= await SendAsync(c, html, markup, ct) is not null;
        return any;
    }

    /// <summary>Sends a stored broadcast to its recipients (only users who use the mentor bot). Returns the number of chats reached.</summary>
    public async Task<int> BroadcastAsync(int broadcastId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var b = await db.MentorBroadcasts.FirstOrDefaultAsync(x => x.Id == broadcastId, ct);
        if (b is null || b.SentAt is not null) return 0;
        b.SentAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var activeStudents = db.Students.Where(s => s.Status == StudentStatus.Active || s.Status == StudentStatus.Applicant);
        IQueryable<int> students = b.Target switch
        {
            BroadcastTarget.Group or BroadcastTarget.Parents when b.GroupId is int g => db.Enrollments.Where(e => e.GroupId == g && e.Status == EnrollmentStatus.Active).Select(e => e.StudentId),
            BroadcastTarget.Student when b.StudentId is int s => db.Students.Where(x => x.Id == s).Select(x => x.Id),
            _ => activeStudents.Select(s => s.Id)
        };
        var studentIds = await students.Distinct().ToListAsync(ct);
        var userIds = b.Target switch
        {
            BroadcastTarget.Everyone => await db.MentorBotChats.Where(c => c.UserId != null).Select(c => c.UserId!.Value).ToListAsync(ct),
            BroadcastTarget.Parents => await db.ParentStudents.Where(p => studentIds.Contains(p.StudentId)).Select(p => p.ParentUserId).ToListAsync(ct),
            BroadcastTarget.Student => (await db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.UserId).ToListAsync(ct))
                .Concat(await db.ParentStudents.Where(p => studentIds.Contains(p.StudentId)).Select(p => p.ParentUserId).ToListAsync(ct)).ToList(),
            _ => await db.Students.Where(s => studentIds.Contains(s.Id)).Select(s => s.UserId).ToListAsync(ct)
        };
        var author = await db.Users.Where(u => u.Id == b.CreatedByUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var chats = await db.MentorBotChats.Where(c => c.UserId != null && userIds.Contains(c.UserId.Value) && !c.IsBlocked).Select(c => c.ChatId).Distinct().ToListAsync(ct);
        var html = (b.Target == BroadcastTarget.Student ? "✉️ <b>Mentordan xabar</b>" : "📣 <b>E'lon</b>") + (author is null ? "" : $" · {H(author)}") + "\n\n" + H(b.Text);
        var sent = 0;
        foreach (var chat in chats)
        {
            if (await SendAsync(chat, html, null, ct) is not null) sent++;
            await Task.Delay(50, ct); // stay well under Telegram's 30 msg/s limit
        }
        b.SentCount = sent;
        await db.SaveChangesAsync(ct);
        return sent;
    }

    public async Task LogAsync(long chatId, bool incoming, string text)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            db.MentorBotMessages.Add(new MentorBotMessage { ChatId = chatId, Incoming = incoming, Text = text.Length > 2000 ? text[..2000] : text });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogWarning(ex, "Mentor bot log failed"); }
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
