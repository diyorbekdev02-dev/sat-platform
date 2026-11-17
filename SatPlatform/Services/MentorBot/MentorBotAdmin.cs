using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

public record BotChatRow(long ChatId, int? UserId, string Name, UserRole? Role, string? Phone, string? Username, DateTime CreatedAt, DateTime LastSeenAt, int Messages);
public record BotMessageRow(long Id, long ChatId, string ChatName, bool Incoming, string Text, DateTime CreatedAt);
public record BotQuestionRow(int Id, MentorQuestionKind Kind, string Name, string Text, string? Answer, string? AnsweredBy, DateTime CreatedAt, DateTime? AnsweredAt);
public record BotBroadcastRow(int Id, BroadcastTarget Target, string? Group, string Text, string Author, DateTime CreatedAt, DateTime? ScheduledAt, DateTime? SentAt, int SentCount);
public record BotStats(int Chats, int Students, int Parents, int Staff, int Unlinked, int Messages24h, int OpenQuestions, int Scheduled, int QuizToday);

/// <summary>Read-only data for the admin "Bot" page + token configuration.</summary>
public class MentorBotAdmin(IDbContextFactory<AppDbContext> factory, SettingsService settings, IHttpClientFactory httpFactory, IConfiguration config, AuditService audit)
{
    public async Task<bool> HasTokenAsync() => !string.IsNullOrWhiteSpace(await settings.GetAsync(MentorBotService.TokenSetting)) || !string.IsNullOrWhiteSpace(config["MentorBot:Token"]);

    public async Task<(string? Username, string? Error)> SetTokenAsync(string? token, int userId)
    {
        token = token?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            await settings.SetAsync(MentorBotService.TokenSetting, "");
            await audit.LogAsync(userId, "MentorBotTokenCleared", "Setting", null);
            return (null, null);
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^\d{5,15}:[A-Za-z0-9_-]{30,}$")) return (null, "Token formati noto'g'ri. BotFather bergan to'liq tokenni kiriting (123456789:AA...).");
        if (token == config["Telegram:BotToken"]?.Trim()) return (null, "Bu bildirishnoma botining tokeni. JavohirsMath uchun BotFather'da alohida bot yarating.");
        var (username, error) = await MentorBotClient.CheckTokenAsync(httpFactory.CreateClient("mentorbot"), token);
        if (error is not null) return (null, error);
        await settings.SetAsync(MentorBotService.TokenSetting, token);
        await audit.LogAsync(userId, "MentorBotTokenSet", "Setting", null, "@" + username);
        return (username, null);
    }

    public async Task<BotStats> StatsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var roles = await db.MentorBotChats.GroupBy(c => c.User == null ? (UserRole?)null : c.User.Role).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        int R(params UserRole?[] r) => roles.Where(x => r.Contains(x.Key)).Sum(x => x.N);
        var day = DateTime.UtcNow.AddDays(-1);
        var todayUtc = Tz.ToUtc(Tz.Today.ToDateTime(TimeOnly.MinValue));
        return new BotStats(roles.Sum(x => x.N), R(UserRole.Student), R(UserRole.Parent), R(UserRole.Admin, UserRole.SuperAdmin, UserRole.Teacher, UserRole.Assistant), R([null]),
            await db.MentorBotMessages.CountAsync(m => m.CreatedAt > day),
            await db.MentorQuestions.CountAsync(q => q.AnsweredAt == null),
            await db.MentorBroadcasts.CountAsync(b => b.SentAt == null && b.ScheduledAt != null),
            await db.MentorQuizResults.CountAsync(q => q.CreatedAt >= todayUtc));
    }

    public async Task<List<BotChatRow>> ChatsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var counts = await db.MentorBotMessages.GroupBy(m => m.ChatId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var rows = await db.MentorBotChats.AsNoTracking().Select(c => new
        {
            c.ChatId, c.UserId, Name = c.User != null ? c.User.FullName : (c.FirstName ?? "—"), Role = c.User != null ? (UserRole?)c.User.Role : null,
            Phone = c.User != null ? c.User.Phone : c.Phone, c.Username, c.CreatedAt, c.LastSeenAt
        }).OrderByDescending(c => c.LastSeenAt).ToListAsync();
        return rows.Select(c => new BotChatRow(c.ChatId, c.UserId, c.Name, c.Role, c.Phone, c.Username, c.CreatedAt, c.LastSeenAt, counts.GetValueOrDefault(c.ChatId))).ToList();
    }

    public async Task<List<BotMessageRow>> MessagesAsync(long? chatId, string? search, int take = 200)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.MentorBotMessages.AsNoTracking().AsQueryable();
        if (chatId is long c) q = q.Where(m => m.ChatId == c);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(m => EF.Functions.ILike(m.Text, "%" + search.Trim() + "%"));
        var list = await q.OrderByDescending(m => m.Id).Take(take).ToListAsync();
        var ids = list.Select(m => m.ChatId).Distinct().ToList();
        var names = await db.MentorBotChats.AsNoTracking().Where(x => ids.Contains(x.ChatId))
            .Select(x => new { x.ChatId, Name = x.User != null ? x.User.FullName : (x.FirstName ?? x.ChatId.ToString()) }).ToDictionaryAsync(x => x.ChatId, x => x.Name);
        return list.Select(m => new BotMessageRow(m.Id, m.ChatId, names.GetValueOrDefault(m.ChatId, m.ChatId.ToString()), m.Incoming, m.Text, m.CreatedAt)).Reverse().ToList();
    }

    public async Task<List<BotQuestionRow>> QuestionsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.MentorQuestions.AsNoTracking().OrderBy(q => q.AnsweredAt != null).ThenByDescending(q => q.CreatedAt).Take(300)
            .Select(q => new { q.Id, q.Kind, Name = q.User != null ? q.User.FullName : "?", q.Text, q.Answer, q.AnsweredByUserId, q.CreatedAt, q.AnsweredAt }).ToListAsync();
        var ids = rows.Where(r => r.AnsweredByUserId != null).Select(r => r.AnsweredByUserId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return rows.Select(r => new BotQuestionRow(r.Id, r.Kind, r.Name, r.Text, r.Answer, r.AnsweredByUserId is int a ? names.GetValueOrDefault(a) : null, r.CreatedAt, r.AnsweredAt)).ToList();
    }

    public async Task<List<BotBroadcastRow>> BroadcastsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.MentorBroadcasts.AsNoTracking().OrderByDescending(b => b.Id).Take(200).ToListAsync();
        var gids = rows.Where(r => r.GroupId != null).Select(r => r.GroupId!.Value).Distinct().ToList();
        var groups = await db.Groups.Where(g => gids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name);
        var uids = rows.Select(r => r.CreatedByUserId).Distinct().ToList();
        var users = await db.Users.Where(u => uids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return rows.Select(b => new BotBroadcastRow(b.Id, b.Target, b.GroupId is int g ? groups.GetValueOrDefault(g) : null, b.Text, users.GetValueOrDefault(b.CreatedByUserId, "?"),
            b.CreatedAt, b.ScheduledAt, b.SentAt, b.SentCount)).ToList();
    }
}
