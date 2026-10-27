using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.Telegram;

/// <summary>
/// Long-polls the Telegram bot.
/// - "/start TOKEN" (deep link from the site) → asks for the contact → delivers the pending verification code.
/// - A contact from an unknown phone starts the in-bot registration wizard (role → name → [child code]) and
///   ends with a one-time login link, so a student or parent can register without opening the site first.
/// - "/login" sends a one-time login link to a known user.
/// </summary>
public class TelegramBotService(TelegramBotClient bot, AuthService auth, IDbContextFactory<AppDbContext> factory, IConfiguration config, ILogger<TelegramBotService> logger) : BackgroundService
{
    private static readonly object ContactKeyboard = new
    {
        keyboard = new[] { new[] { new { text = "📱 Telefon raqamni yuborish", request_contact = true } } },
        resize_keyboard = true,
        one_time_keyboard = true
    };
    private static readonly object RoleKeyboard = new
    {
        keyboard = new[] { new[] { new { text = "🎓 O'quvchi" }, new { text = "👨‍👩‍👧 Ota-ona" } }, new[] { new { text = "❌ Bekor qilish" } } },
        resize_keyboard = true,
        one_time_keyboard = true
    };
    private static readonly object RemoveKeyboard = new { remove_keyboard = true };

    private static readonly ConcurrentDictionary<long, string> ExpectedPhones = new();

    private string PublicUrl => (config["App:PublicUrl"] ?? "").TrimEnd('/');

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!bot.IsConfigured)
        {
            logger.LogWarning("Telegram bot is NOT configured (Telegram:BotToken is empty). Verification codes are shown on screen in Development.");
            return;
        }
        var me = await bot.GetMeAsync(ct);
        logger.LogInformation("Telegram bot polling started (@{Bot})", me ?? bot.BotUsername);
        long offset = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdatesAsync(offset, 30, ct);
                foreach (var u in updates)
                {
                    offset = u.UpdateId + 1;
                    try { await HandleAsync(u, ct); }
                    catch (Exception ex) { logger.LogError(ex, "Error handling Telegram update {Id}", u.UpdateId); }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogWarning("Telegram: another server is polling this bot (409); retrying in 15s");
                try { await Task.Delay(15000, ct); } catch (OperationCanceledException) { }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telegram polling error, retrying in 5s");
                try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { }
            }
        }
    }

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    private async Task<string> MagicLinkAsync(int userId)
    {
        var token = await auth.IssueLoginTokenAsync(userId, null, TimeSpan.FromMinutes(15));
        return $"{PublicUrl}/auth/token?t={token}";
    }

    private async Task HandleAsync(TelegramUpdate u, CancellationToken ct)
    {
        if (u.ChatId == 0) return;
        await using var db = await factory.CreateDbContextAsync();
        var state = await db.BotStates.FindAsync([u.ChatId], ct);

        // ---- contact shared ----
        if (!string.IsNullOrEmpty(u.ContactPhone))
        {
            if (u.ContactUserId is long cid && cid != u.FromId)
            {
                await bot.SendMessageAsync(u.ChatId, "Iltimos, faqat o'zingizning raqamingizni yuboring.", ContactKeyboard, ct);
                return;
            }
            var phone = PhoneFormat.Normalize(u.ContactPhone);
            if (!PhoneFormat.IsValid(phone))
            {
                await bot.SendMessageAsync(u.ChatId, "⚠️ Faqat O'zbekiston raqamlari (+998) qabul qilinadi. Iltimos, O'zbekiston raqamingizni yuboring.", ContactKeyboard, ct);
                return;
            }
            var code = await auth.LinkTelegramAsync(phone, u.ChatId, u.Username, u.FirstName);

            if (ExpectedPhones.TryRemove(u.ChatId, out var expected) && expected != phone)
            {
                await bot.SendMessageAsync(u.ChatId,
                    $"⚠️ Saytda <b>{PhoneFormat.Display(expected)}</b> raqamini kiritgansiz, lekin Telegram hisobingiz <b>{PhoneFormat.Display(phone)}</b> raqamida.\n\nSaytga qaytib <b>{PhoneFormat.Display(phone)}</b> raqami bilan davom eting — kod shu yerga keladi.",
                    RemoveKeyboard, ct);
                return;
            }

            var user = await db.Users.FirstOrDefaultAsync(x => x.Phone == phone, ct);
            if (user is not null)
            {
                var text = $"✅ Raqamingiz bog'landi: <b>{PhoneFormat.Display(phone)}</b>\n";
                if (code is not null) text += $"\n🔐 Tasdiqlash kodingiz: <code>{code}</code>\nSaytga qaytib kodni kiriting.\n";
                text += $"\nSaytga parolsiz kirish uchun havola (15 daqiqa amal qiladi):\n{await MagicLinkAsync(user.Id)}\n\nBildirishnomalar (natijalar, davomat, uy vazifasi) shu yerga keladi.";
                await bot.SendMessageAsync(u.ChatId, text, RemoveKeyboard, ct);
                return;
            }

            if (code is not null)
            {
                // the user is registering on the site: just deliver the code
                await bot.SendMessageAsync(u.ChatId, $"✅ Raqamingiz saqlandi: <b>{PhoneFormat.Display(phone)}</b>\n\n🔐 Tasdiqlash kodingiz: <code>{code}</code>\n\nSaytga qaytib kodni kiriting.", RemoveKeyboard, ct);
                return;
            }

            // unknown phone → in-bot registration wizard
            await SetStateAsync(db, u.ChatId, "role", phone: phone, ct: ct);
            await bot.SendMessageAsync(u.ChatId,
                $"✅ Raqamingiz: <b>{PhoneFormat.Display(phone)}</b>\n\nBu raqam hali ro'yxatdan o'tmagan. Ro'yxatdan o'tamizmi? Siz kimsiz?",
                RoleKeyboard, ct);
            return;
        }

        var text0 = (u.Text ?? "").Trim();

        // ---- commands ----
        if (text0.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text0.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var sitePhone = parts.Length == 2 ? await auth.GetPhoneForTokenAsync(parts[1]) : null;
            if (sitePhone is not null) ExpectedPhones[u.ChatId] = sitePhone; else ExpectedPhones.TryRemove(u.ChatId, out _);
            await ClearStateAsync(db, u.ChatId, ct);

            var known = await db.Users.AnyAsync(x => x.TelegramChatId == u.ChatId, ct);
            var text = "Salom! 👋 Bu SAT tayyorlov platformasining boti.\n\n";
            text += sitePhone is not null
                ? $"Saytda <b>{PhoneFormat.Display(sitePhone)}</b> raqami kiritildi. Tasdiqlash uchun pastdagi tugma orqali telefon raqamingizni yuboring — kod shu yerga keladi."
                : known
                    ? "Hisobingiz bog'langan. /login — saytga kirish havolasi, /help — yordam."
                    : "Ro'yxatdan o'tish yoki hisobni bog'lash uchun pastdagi tugma orqali telefon raqamingizni yuboring.";
            await bot.SendMessageAsync(u.ChatId, text, known && sitePhone is null ? RemoveKeyboard : ContactKeyboard, ct);
            return;
        }
        if (text0.Equals("/login", StringComparison.OrdinalIgnoreCase))
        {
            var user = await db.Users.FirstOrDefaultAsync(x => x.TelegramChatId == u.ChatId && x.IsActive, ct);
            if (user is null) { await bot.SendMessageAsync(u.ChatId, "Avval telefon raqamingizni yuboring (/start).", ContactKeyboard, ct); return; }
            await bot.SendMessageAsync(u.ChatId, $"🔗 Saytga kirish havolasi (15 daqiqa amal qiladi):\n{await MagicLinkAsync(user.Id)}", RemoveKeyboard, ct);
            return;
        }
        if (text0.Equals("/help", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendMessageAsync(u.ChatId, "/start — boshlash yoki hisobni bog'lash\n/login — saytga parolsiz kirish havolasi\n/cancel — amalni bekor qilish\n\nSayt: " + PublicUrl, RemoveKeyboard, ct);
            return;
        }
        if (text0.Equals("/cancel", StringComparison.OrdinalIgnoreCase) || text0.StartsWith("❌"))
        {
            await ClearStateAsync(db, u.ChatId, ct);
            await bot.SendMessageAsync(u.ChatId, "Bekor qilindi. Qaytadan boshlash uchun /start.", RemoveKeyboard, ct);
            return;
        }

        // ---- registration wizard ----
        if (state is not null && state.Phone is not null)
        {
            switch (state.Step)
            {
                case "role":
                    if (text0.Contains("quvchi", StringComparison.OrdinalIgnoreCase)) state.Role = "Student";
                    else if (text0.Contains("Ota", StringComparison.OrdinalIgnoreCase)) state.Role = "Parent";
                    else { await bot.SendMessageAsync(u.ChatId, "Iltimos, tugmalardan birini tanlang.", RoleKeyboard, ct); return; }
                    state.Step = "name"; state.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    await bot.SendMessageAsync(u.ChatId, "Ism-familiyangizni yozing (masalan: Ali Valiyev):", RemoveKeyboard, ct);
                    return;
                case "name":
                    if (AuthService.ValidateName(text0) is string nerr) { await bot.SendMessageAsync(u.ChatId, nerr, null, ct); return; }
                    state.FullName = text0; state.UpdatedAt = DateTime.UtcNow;
                    if (state.Role == "Parent")
                    {
                        state.Step = "childcode";
                        await db.SaveChangesAsync(ct);
                        await bot.SendMessageAsync(u.ChatId, "Farzandingizning <b>ota-ona kodini</b> yozing (o'quvchi profilida ko'rsatilgan 6 belgili kod):", null, ct);
                        return;
                    }
                    await db.SaveChangesAsync(ct);
                    await FinishRegistrationAsync(db, state, u, null, ct);
                    return;
                case "childcode":
                    await FinishRegistrationAsync(db, state, u, text0, ct);
                    return;
            }
        }

        var linked = await db.Users.AnyAsync(x => x.TelegramChatId == u.ChatId, ct);
        await bot.SendMessageAsync(u.ChatId,
            linked ? "Buyruqlar: /login — saytga kirish, /help — yordam." : "Boshlash uchun /start ni bosing va telefon raqamingizni yuboring.",
            linked ? RemoveKeyboard : ContactKeyboard, ct);
    }

    private async Task FinishRegistrationAsync(AppDbContext db, BotState state, TelegramUpdate u, string? childCode, CancellationToken ct)
    {
        var role = state.Role == "Parent" ? UserRole.Parent : UserRole.Student;
        var result = await auth.RegisterFromBotAsync(state.Phone!, u.ChatId, u.Username, state.FullName ?? "", role, childCode);
        if (result.Error is not null)
        {
            if (role == UserRole.Parent && state.Step == "childcode")
            {
                await bot.SendMessageAsync(u.ChatId, result.Error + "\n\nKodni qayta yozing yoki /cancel.", null, ct);
                return;
            }
            await ClearStateAsync(db, u.ChatId, ct);
            await bot.SendMessageAsync(u.ChatId, "❌ " + result.Error, RemoveKeyboard, ct);
            return;
        }
        await ClearStateAsync(db, u.ChatId, ct);
        var link = await MagicLinkAsync(result.User!.Id);
        var text = $"🎉 Ro'yxatdan o'tdingiz, <b>{H(result.User.FullName)}</b>!\n\nSaytga kirish havolasi (15 daqiqa amal qiladi):\n{link}\n\n" +
                   "Saytda <b>Profil</b> bo'limida parol o'rnating — keyin telefon raqam va parol bilan ham kira olasiz. Istalgan vaqt /login buyrug'i yangi havola beradi.";
        if (role == UserRole.Student) text += "\n\nBirinchi qadam: saytda <b>Diagnostik test</b>ni ishlang — tizim zaif mavzularingizni aniqlab beradi.";
        await bot.SendMessageAsync(u.ChatId, text, RemoveKeyboard, ct);
    }

    private static async Task SetStateAsync(AppDbContext db, long chatId, string step, string? phone = null, CancellationToken ct = default)
    {
        var s = await db.BotStates.FindAsync([chatId], ct);
        if (s is null) { s = new BotState { ChatId = chatId }; db.BotStates.Add(s); }
        s.Step = step; s.Phone = phone ?? s.Phone; s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static async Task ClearStateAsync(AppDbContext db, long chatId, CancellationToken ct)
    {
        var s = await db.BotStates.FindAsync([chatId], ct);
        if (s is not null) { db.BotStates.Remove(s); await db.SaveChangesAsync(ct); }
    }
}
