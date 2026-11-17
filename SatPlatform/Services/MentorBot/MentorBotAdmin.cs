using System.Net;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

public record BotChatRow(long ChatId, int? UserId, string Name, UserRole? Role, string? Phone, string? Username, DateTime CreatedAt, DateTime LastSeenAt, int Messages);
public record BotMessageRow(long Id, long ChatId, string ChatName, bool Incoming, string Text, DateTime CreatedAt);
public record BotQuestionRow(int Id, MentorQuestionKind Kind, string Name, UserRole? Role, string Text, bool HasFile, string? Answer, string? AnsweredBy, DateTime CreatedAt, DateTime? AnsweredAt);
public record BotBroadcastRow(int Id, BroadcastTarget Target, string? Group, string Text, string Author, DateTime CreatedAt, DateTime? ScheduledAt, DateTime? SentAt, int SentCount);
public record BotStats(int Chats, int Students, int Parents, int Staff, int Unlinked, int Messages24h, int OpenQuestions, int Scheduled, int QuizToday);
public record BotRoleRow(int UserId, string Name, string Phone, UserRole SiteRole, UserRole? BotRole, bool CanUseSite, bool IsActive, bool InBot, bool CanManagePdfs);

/// <summary>Data and actions for the admin "Bot" page: monitoring, the web admin panel (same actions as in the bot) and bot roles.</summary>
public class MentorBotAdmin(
    IDbContextFactory<AppDbContext> factory, SettingsService settings, IHttpClientFactory httpFactory, IConfiguration config, AuditService audit,
    MentorBotSender sender, MentorBotClient client, CrmService crm, AcademyService academy)
{
    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>Roles that can be given inside the bot (students / parents come from their platform records).</summary>
    public static readonly UserRole[] BotStaffRoles = [UserRole.Admin, UserRole.Teacher, UserRole.Assistant];

    // ---------------------------------------------------------------- token

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

    // ---------------------------------------------------------------- monitoring

    public async Task<BotStats> StatsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var roles = await db.MentorBotChats.GroupBy(c => c.User == null ? (UserRole?)null : (c.User.BotRole ?? c.User.Role)).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
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
            c.ChatId, c.UserId, Name = c.User != null ? c.User.FullName : (c.FirstName ?? "—"), Role = c.User != null ? (UserRole?)(c.User.BotRole ?? c.User.Role) : null,
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

    public async Task<List<BotQuestionRow>> QuestionsAsync(bool openOnly = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.MentorQuestions.AsNoTracking();
        if (openOnly) q = q.Where(x => x.AnsweredAt == null);
        var rows = await q.OrderBy(x => x.AnsweredAt != null).ThenByDescending(x => x.CreatedAt).Take(300)
            .Select(x => new { x.Id, x.Kind, Name = x.User != null ? x.User.FullName : "?", Role = x.User != null ? (UserRole?)x.User.Role : null, x.Text, HasFile = x.TelegramFileId != null,
                x.Answer, x.AnsweredByUserId, x.CreatedAt, x.AnsweredAt }).ToListAsync();
        var ids = rows.Where(r => r.AnsweredByUserId != null).Select(r => r.AnsweredByUserId!.Value).Distinct().ToList();
        var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        return rows.Select(r => new BotQuestionRow(r.Id, r.Kind, r.Name, r.Role, r.Text, r.HasFile, r.Answer, r.AnsweredByUserId is int a ? names.GetValueOrDefault(a) : null, r.CreatedAt, r.AnsweredAt)).ToList();
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

    /// <summary>Photo / document attached to a question or receipt, downloaded from Telegram.</summary>
    public async Task<(byte[] Data, string ContentType)?> QuestionFileAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        var fileId = await db.MentorQuestions.Where(q => q.Id == id).Select(q => q.TelegramFileId).FirstOrDefaultAsync();
        if (fileId is null) return null;
        var data = await client.DownloadAsync(fileId);
        if (data is null) return null;
        var type = data.Length > 4 && data[0] == 0x25 && data[1] == 0x50 ? "application/pdf"
            : data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 ? "image/jpeg"
            : data.Length > 4 && data[0] == 0x89 && data[1] == 0x50 ? "image/png" : "application/octet-stream";
        return (data, type);
    }

    // ---------------------------------------------------------------- admin panel actions (same effect as in the bot)

    public async Task<(int Sent, string? Error)> BroadcastAsync(BroadcastTarget target, int? groupId, string text, DateTime? scheduledAtUtc, int userId)
    {
        text = text.Trim();
        if (text.Length == 0) return (0, "Xabar matnini yozing.");
        if (text.Length > 3500) return (0, "Xabar 3500 belgidan oshmasin.");
        if (target == BroadcastTarget.Group && groupId is null) return (0, "Guruhni tanlang.");
        if (scheduledAtUtc is DateTime at && at <= DateTime.UtcNow) return (0, "Rejalashtirilgan vaqt kelajakda bo'lsin.");
        await using var db = await factory.CreateDbContextAsync();
        var b = new MentorBroadcast { Target = target, GroupId = target is BroadcastTarget.Group or BroadcastTarget.Parents ? groupId : null, Text = text, ScheduledAt = scheduledAtUtc, CreatedByUserId = userId };
        db.MentorBroadcasts.Add(b);
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "MentorBroadcast", "MentorBroadcast", b.Id, $"{target} {b.GroupId} (sayt)");
        return scheduledAtUtc is null ? (await sender.BroadcastAsync(b.Id), null) : (0, null);
    }

    public async Task<string?> AnswerAsync(int questionId, string answer, int userId)
    {
        answer = answer.Trim();
        if (answer.Length == 0) return "Javob yozing.";
        await using var db = await factory.CreateDbContextAsync();
        var q = await db.MentorQuestions.FirstOrDefaultAsync(x => x.Id == questionId);
        if (q is null) return "Topilmadi.";
        if (q.AnsweredAt is not null) return "Bu savolga allaqachon javob berilgan.";
        var author = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync();
        q.Answer = answer.Length > 2000 ? answer[..2000] : answer; q.AnsweredAt = DateTime.UtcNow; q.AnsweredByUserId = userId;
        await db.SaveChangesAsync();
        var shortQ = q.Text.Length > 200 ? q.Text[..200] + "…" : q.Text;
        await sender.SendAsync(q.ChatId, $"💬 <b>Mentor javobi</b> ({H(author)})\n\n<i>Savolingiz: {H(shortQ)}</i>\n\n{H(answer)}");
        await audit.LogAsync(userId, "MentorQuestionAnswered", "MentorQuestion", q.Id, "sayt");
        return null;
    }

    public async Task<string?> AcceptReceiptAsync(int questionId, decimal amount, int userId)
    {
        if (amount <= 0) return "Summani kiriting.";
        await using var db = await factory.CreateDbContextAsync();
        var q = await db.MentorQuestions.FirstOrDefaultAsync(x => x.Id == questionId && x.Kind == MentorQuestionKind.Receipt);
        if (q?.UserId is not int uid) return "Chek topilmadi.";
        if (q.AnsweredAt is not null) return "Bu chek allaqachon ko'rib chiqilgan.";
        var sid = await db.Students.Where(s => s.UserId == uid).Select(s => (int?)s.Id).FirstOrDefaultAsync()
                  ?? await db.ParentStudents.Where(p => p.ParentUserId == uid).Select(p => (int?)p.StudentId).FirstOrDefaultAsync();
        if (sid is null) return "Bu foydalanuvchiga bog'langan o'quvchi topilmadi.";
        var err = await crm.AddPaymentAsync(sid.Value, amount, PaymentMethod.Transfer, Tz.Today, "Chek (JavohirsMath bot)", null, userId);
        if (err is not null) return err;
        q.Answer = $"Tasdiqlandi: {Labels.Money(amount)}"; q.AnsweredAt = DateTime.UtcNow; q.AnsweredByUserId = userId;
        await db.SaveChangesAsync();
        var st = await db.Students.AsNoTracking().Include(s => s.User).FirstAsync(s => s.Id == sid);
        var bal = await db.Payments.Where(p => p.StudentId == sid).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var msg = $"✅ To'lovingiz qabul qilindi: <b>{Labels.Money(amount)}</b> so'm.\nBalans: {Labels.Money(bal)} so'm. Rahmat!";
        await sender.SendToUserAsync(st.UserId, msg);
        foreach (var p in await academy.ParentsOfAsync(sid.Value)) await sender.SendToUserAsync(p.Id, $"👶 {H(st.User.FullName)}: " + msg);
        return null;
    }

    public async Task<string?> RejectReceiptAsync(int questionId, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = await db.MentorQuestions.FirstOrDefaultAsync(x => x.Id == questionId);
        if (q is null || q.AnsweredAt is not null) return "Topilmadi yoki allaqachon ko'rib chiqilgan.";
        q.Answer = q.Kind == MentorQuestionKind.Receipt ? "Rad etildi" : "Yopildi"; q.AnsweredAt = DateTime.UtcNow; q.AnsweredByUserId = userId;
        await db.SaveChangesAsync();
        if (q.Kind == MentorQuestionKind.Receipt) await sender.SendAsync(q.ChatId, "❌ Yuborgan chekingiz tasdiqlanmadi. Iltimos, mentor bilan bog'laning.");
        return null;
    }

    public async Task<List<BalanceRow>> DebtorsAsync() =>
        (await crm.BalancesAsync()).Where(b => b.Balance < 0 && b.Status != StudentStatus.Dropped).OrderBy(b => b.Balance).ToList();

    /// <summary>Payment reminder to the student and their parents in the bot. Returns the number of messages delivered.</summary>
    public async Task<int> DebtReminderAsync(int? studentId, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var sent = 0;
        foreach (var b in (await DebtorsAsync()).Where(b => studentId is null || b.StudentId == studentId))
        {
            var studentUser = await db.Students.Where(s => s.Id == b.StudentId).Select(s => s.UserId).FirstAsync();
            var msg = $"💳 <b>To'lov eslatmasi</b>\n{H(b.Name)} bo'yicha qarzdorlik: <b>{Labels.Money(-b.Balance)}</b> so'm." +
                      (b.NextPaymentDate is DateOnly d ? $"\nTo'lov sanasi: {d:dd.MM.yyyy}" : "") + "\n\nTo'lovdan so'ng chekni shu botga yuboring.";
            var kb = new { inline_keyboard = new[] { new[] { new { text = "🧾 Chek yuborish", callback_data = "rcp" } } } };
            if (await sender.SendToUserAsync(studentUser, msg, kb)) sent++;
            foreach (var p in await academy.ParentsOfAsync(b.StudentId)) if (await sender.SendToUserAsync(p.Id, msg, kb)) sent++;
        }
        await audit.LogAsync(userId, "MentorDebtReminder", "Student", studentId, "sayt");
        return sent;
    }

    public async Task<List<MentorBotService.RiskRow>> RiskAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var all = await db.Students.Where(s => s.Status == StudentStatus.Active).Select(s => s.Id).ToListAsync();
        return await MentorBotService.RiskAsync(db, all, default);
    }

    // ---------------------------------------------------------------- bot roles (separate from site roles)

    /// <summary>Staff (site or bot), anyone with a bot role, and everyone who uses the bot.</summary>
    public async Task<List<BotRoleRow>> RolesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var inBot = await db.MentorBotChats.Where(c => c.UserId != null).Select(c => c.UserId!.Value).Distinct().ToListAsync();
        var users = await db.Users.AsNoTracking()
            .Where(u => u.BotRole != null || u.CanManageBotPdfs || !u.CanUseSite || inBot.Contains(u.Id) || u.Role == UserRole.Admin || u.Role == UserRole.SuperAdmin || u.Role == UserRole.Teacher || u.Role == UserRole.Assistant)
            .OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync();
        return users.Select(u => new BotRoleRow(u.Id, u.FullName, u.Phone, u.Role, u.BotRole, u.CanUseSite, u.IsActive, inBot.Contains(u.Id), u.Role == UserRole.SuperAdmin || u.CanManageBotPdfs)).ToList();
    }

    public async Task<string?> SetBotRoleAsync(int targetUserId, UserRole? role, int actorId, bool actorIsSuperAdmin)
    {
        if (role is not null && !BotStaffRoles.Contains(role.Value)) return "Botda faqat admin, o'qituvchi yoki yordamchi roli beriladi.";
        await using var db = await factory.CreateDbContextAsync();
        var u = await db.Users.FindAsync(targetUserId);
        if (u is null) return "Foydalanuvchi topilmadi.";
        if (u.Role == UserRole.SuperAdmin && !actorIsSuperAdmin) return "Super admin sozlamalarini faqat super admin o'zgartiradi.";
        var old = u.BotRole;
        u.BotRole = role == u.Role ? null : role;
        await db.SaveChangesAsync();
        await audit.LogAsync(actorId, "BotRoleChanged", "User", u.Id, $"{old?.ToString() ?? "sayt roli"} -> {u.BotRole?.ToString() ?? "sayt roli"}");
        return null;
    }

    public async Task<string?> SetSiteAccessAsync(int targetUserId, bool canUseSite, int actorId, bool actorIsSuperAdmin)
    {
        if (targetUserId == actorId && !canUseSite) return "O'zingizning saytga kirishingizni yopib bo'lmaydi.";
        await using var db = await factory.CreateDbContextAsync();
        var u = await db.Users.FindAsync(targetUserId);
        if (u is null) return "Foydalanuvchi topilmadi.";
        if (u.Role == UserRole.SuperAdmin && !actorIsSuperAdmin) return "Super admin sozlamalarini faqat super admin o'zgartiradi.";
        u.CanUseSite = canUseSite;
        await db.SaveChangesAsync();
        SiteAccessCache.Invalidate(u.Id);
        await audit.LogAsync(actorId, "SiteAccessChanged", "User", u.Id, canUseSite ? "ochildi" : "yopildi");
        return null;
    }

    /// <summary>Permission to add / edit PDFs in the bot library. Only a super admin can grant or take it.</summary>
    public async Task<string?> SetPdfPermissionAsync(int targetUserId, bool allowed, int actorId, bool actorIsSuperAdmin)
    {
        if (!actorIsSuperAdmin) return "PDF ruxsatini faqat super admin beradi.";
        await using var db = await factory.CreateDbContextAsync();
        var u = await db.Users.FindAsync(targetUserId);
        if (u is null) return "Foydalanuvchi topilmadi.";
        if (u.Role == UserRole.SuperAdmin) return "Super admin PDF qo'sha oladi — bu ruxsatni olib bo'lmaydi.";
        u.CanManageBotPdfs = allowed;
        await db.SaveChangesAsync();
        await audit.LogAsync(actorId, "BotPdfPermission", "User", u.Id, allowed ? "berildi" : "olindi");
        if (allowed) await sender.SendToUserAsync(u.Id, "📚 Sizga botdagi kutubxonaga <b>PDF qo'shish va o'zgartirish</b> ruxsati berildi. «📚 Materiallar» → «➕ PDF qo'shish».");
        return null;
    }

    /// <summary>Gives a bot role to a phone. Unknown phones get a Telegram-only account (no site access).</summary>
    public async Task<string?> AddBotStaffAsync(string fullName, string phoneInput, UserRole role, int actorId, bool actorIsSuperAdmin)
    {
        var phone = PhoneFormat.Normalize(phoneInput);
        if (!PhoneFormat.IsValid(phone)) return PhoneFormat.InvalidMessage;
        if (!BotStaffRoles.Contains(role)) return "Rolni tanlang.";
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        if (existing is not null) return await SetBotRoleAsync(existing.Id, role, actorId, actorIsSuperAdmin);
        if (AuthService.ValidateName(fullName) is string nerr) return nerr;
        var user = new User { FullName = fullName.Trim(), Phone = phone, Role = role, BotRole = role, CanUseSite = false, AvatarColor = AuthService.ColorFor(phone) };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        await audit.LogAsync(actorId, "BotStaffCreated", "User", user.Id, $"{role}, faqat bot");
        return null;
    }
}
