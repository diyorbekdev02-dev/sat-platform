using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;
using SatPlatform.Services.Telegram;

namespace SatPlatform.Services;

public enum CodeDelivery { Telegram, NeedLink, Dev }

public record CodeRequestResult(CodeDelivery Delivery, string? DevCode, string? BotUsername, string? DeepLink, string? Error);
public record CodeStatus(bool Delivered);
public record LoginResult(User? User, string? Error);

public class AuthService(
    IDbContextFactory<AppDbContext> factory,
    TelegramBotClient bot,
    SettingsService settings,
    AuditService audit,
    IHostEnvironment env,
    ILogger<AuthService> logger)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(45);
    private const int MaxAttempts = 5;
    private const int MaxPasswordFailures = 8;
    private static readonly TimeSpan PasswordLockout = TimeSpan.FromMinutes(15);
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly ConcurrentDictionary<string, (int Failures, DateTime Since)> PasswordFailures = new();

    public string? BotUsername => bot.BotUsername;
    public bool BotConfigured => bot.IsConfigured;

    // ---------------------------------------------------------------- validation

    public static string? ValidatePassword(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < 6 ? "Parol kamida 6 ta belgidan iborat bo'lishi kerak." :
        password.Length > 128 ? "Parol juda uzun." : null;

    public static string? ValidateName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2 ? "Ism-familiyani kiriting." :
        name.Trim().Length > 120 ? "Ism juda uzun." : null;

    public static string HashPassword(User user, string password) => Hasher.HashPassword(user, password);

    public static string ColorFor(string seed)
    {
        string[] palette = ["#0f8f7e", "#2563eb", "#7c3aed", "#db2777", "#ea580c", "#16a34a", "#0891b2", "#9333ea"];
        var h = 0; foreach (var c in seed) h = (h * 31 + c) & 0x7fffffff;
        return palette[h % palette.Length];
    }

    // ---------------------------------------------------------------- password login

    public async Task<LoginResult> LoginWithPasswordAsync(string phoneInput, string password)
    {
        var phone = PhoneFormat.Normalize(phoneInput);
        if (!PhoneFormat.IsValid(phone)) return new(null, PhoneFormat.InvalidMessage);

        if (PasswordFailures.TryGetValue(phone, out var f) && f.Failures >= MaxPasswordFailures)
        {
            if (DateTime.UtcNow - f.Since < PasswordLockout)
                return new(null, "Juda ko'p noto'g'ri urinish. 15 daqiqadan so'ng qayta urining yoki Telegram kodi orqali kiring.");
            PasswordFailures.TryRemove(phone, out _);
        }

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.Include(u => u.Student).FirstOrDefaultAsync(u => u.Phone == phone);
        if (user is null) return new(null, "Bu raqam ro'yxatdan o'tmagan.");
        if (user.PasswordHash is null) return new(null, "Bu hisob uchun parol o'rnatilmagan. Telegram kodi orqali kiring va profilda parol o'rnating.");
        if (!user.IsActive) return new(null, "Hisobingiz faol emas. Administratorga murojaat qiling.");

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            PasswordFailures.AddOrUpdate(phone, (1, DateTime.UtcNow), (_, old) => (old.Failures + 1, old.Since));
            return new(null, "Parol noto'g'ri.");
        }
        PasswordFailures.TryRemove(phone, out _);
        if (result == PasswordVerificationResult.SuccessRehashNeeded) user.PasswordHash = Hasher.HashPassword(user, password);
        user.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new(user, null);
    }

    // ---------------------------------------------------------------- Telegram verification codes

    public string? DeepLinkFor(string? token) => token is null || bot.BotUsername is null ? null : $"https://t.me/{bot.BotUsername}?start={token}";

    /// <summary>Sends a code to the phone's Telegram (or returns the deep link to open the bot first).</summary>
    public async Task<CodeRequestResult> RequestCodeAsync(string phoneInput, string purpose)
    {
        var phone = PhoneFormat.Normalize(phoneInput);
        if (!PhoneFormat.IsValid(phone)) return new(CodeDelivery.Dev, null, bot.BotUsername, null, PhoneFormat.InvalidMessage);

        await using var db = await factory.CreateDbContextAsync();
        var exists = await db.Users.AnyAsync(u => u.Phone == phone);
        if (purpose == "register")
        {
            if (exists) return new(CodeDelivery.Dev, null, bot.BotUsername, null, "Bu raqam allaqachon ro'yxatdan o'tgan. Kirish bo'limidan foydalaning.");
            if (!await settings.GetBoolAsync(SettingKeys.AllowSelfRegistration))
                return new(CodeDelivery.Dev, null, bot.BotUsername, null, "Mustaqil ro'yxatdan o'tish o'chirilgan. O'quv markazi administratoriga murojaat qiling.");
        }
        else if (!exists) return new(CodeDelivery.Dev, null, bot.BotUsername, null, "Bu raqam ro'yxatdan o'tmagan.");

        var last = await db.PhoneVerifications.Where(v => v.Phone == phone).OrderByDescending(v => v.CreatedAt).FirstOrDefaultAsync();
        if (last is not null && DateTime.UtcNow - last.CreatedAt < ResendCooldown)
        {
            var wait = (int)(ResendCooldown - (DateTime.UtcNow - last.CreatedAt)).TotalSeconds;
            return new(CodeDelivery.Dev, null, bot.BotUsername, DeepLinkFor(last.LinkToken), $"Yangi kodni {wait} soniyadan so'ng so'rang.");
        }

        await db.PhoneVerifications.Where(v => v.Phone == phone && !v.Consumed).ExecuteUpdateAsync(s => s.SetProperty(v => v.Consumed, true));

        var code = RandomNumberGenerator.GetInt32(10000, 99999).ToString();
        var verification = new PhoneVerification { Phone = phone, Code = code, LinkToken = Ids.Token(16), ExpiresAt = DateTime.UtcNow + CodeLifetime, Purpose = purpose };
        db.PhoneVerifications.Add(verification);

        if (!bot.IsConfigured)
        {
            await db.SaveChangesAsync();
            if (env.IsDevelopment() || Environment.GetEnvironmentVariable("SAT_DEV_CODES") == "1")
            {
                logger.LogWarning("DEV verification code for {Phone}: {Code}", phone, code);
                return new(CodeDelivery.Dev, code, null, null, null);
            }
            return new(CodeDelivery.Dev, null, null, null, "Telegram bot sozlanmagan. Administratorga murojaat qiling.");
        }

        var link = await db.TelegramLinks.FindAsync(phone);
        if (link is null)
        {
            await db.SaveChangesAsync();
            return new(CodeDelivery.NeedLink, null, bot.BotUsername, DeepLinkFor(verification.LinkToken), null);
        }

        var sent = await bot.SendMessageAsync(link.ChatId, $"🔐 Tasdiqlash kodingiz: <code>{code}</code>\n\nKod 10 daqiqa amal qiladi. Agar bu siz bo'lmasangiz, e'tibor bermang.");
        if (sent) verification.DeliveredAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return sent ? new(CodeDelivery.Telegram, null, bot.BotUsername, null, null)
                    : new(CodeDelivery.NeedLink, null, bot.BotUsername, DeepLinkFor(verification.LinkToken), null);
    }

    public async Task<CodeStatus> GetCodeStatusAsync(string phoneInput)
    {
        var phone = PhoneFormat.Normalize(phoneInput);
        await using var db = await factory.CreateDbContextAsync();
        var delivered = await db.PhoneVerifications
            .Where(v => v.Phone == phone && !v.Consumed && v.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(v => v.CreatedAt).Select(v => v.DeliveredAt != null).FirstOrDefaultAsync();
        return new(delivered);
    }

    public async Task<string?> GetPhoneForTokenAsync(string token)
    {
        token = token.Trim().ToLowerInvariant();
        if (token.Length != 32) return null;
        await using var db = await factory.CreateDbContextAsync();
        return await db.PhoneVerifications.Where(x => x.LinkToken == token && !x.Consumed && x.ExpiresAt > DateTime.UtcNow).Select(x => x.Phone).FirstOrDefaultAsync();
    }

    /// <summary>Bot: user shared their contact. Links phone ↔ chat and returns a pending code to deliver.</summary>
    public async Task<string?> LinkTelegramAsync(string phone, long chatId, string? username, string? firstName)
    {
        await using var db = await factory.CreateDbContextAsync();
        var link = await db.TelegramLinks.FindAsync(phone);
        if (link is null) { link = new TelegramLink { Phone = phone }; db.TelegramLinks.Add(link); }
        link.ChatId = chatId; link.Username = username; link.FirstName = firstName; link.LinkedAt = DateTime.UtcNow;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        if (user is not null) { user.TelegramChatId = chatId; user.TelegramUsername = username; }

        var pending = await db.PhoneVerifications
            .Where(v => v.Phone == phone && !v.Consumed && v.DeliveredAt == null && v.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(v => v.CreatedAt).FirstOrDefaultAsync();
        if (pending is not null) pending.DeliveredAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return pending?.Code;
    }

    /// <summary>Checks a code without consuming it (so the user can proceed to the profile step).</summary>
    public async Task<string?> CheckCodeAsync(string phoneInput, string code, bool consume)
    {
        var phone = PhoneFormat.Normalize(phoneInput);
        code = (code ?? "").Trim();
        await using var db = await factory.CreateDbContextAsync();
        var v = await db.PhoneVerifications.Where(x => x.Phone == phone && !x.Consumed).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
        if (v is null || v.ExpiresAt < DateTime.UtcNow) return "Kod muddati tugagan. Yangi kod so'rang.";
        if (v.Attempts >= MaxAttempts) return "Urinishlar soni tugadi. Yangi kod so'rang.";
        if (v.Code != code)
        {
            v.Attempts++;
            await db.SaveChangesAsync();
            return $"Kod noto'g'ri. Qolgan urinishlar: {MaxAttempts - v.Attempts}";
        }
        if (consume) { v.Consumed = true; await db.SaveChangesAsync(); }
        return null;
    }

    // ---------------------------------------------------------------- registration

    public record RegisterRequest(string Phone, string Code, string FullName, string Password, UserRole Role, int? TargetScore, string? ChildCode);

    /// <summary>Site registration: verified phone + name + password. Role is Student or Parent.</summary>
    public async Task<LoginResult> RegisterAsync(RegisterRequest r)
    {
        if (r.Role is not (UserRole.Student or UserRole.Parent)) return new(null, "Faqat o'quvchi yoki ota-ona ro'yxatdan o'ta oladi.");
        if (ValidateName(r.FullName) is string nerr) return new(null, nerr);
        if (ValidatePassword(r.Password) is string perr) return new(null, perr);
        var phone = PhoneFormat.Normalize(r.Phone);
        if (!PhoneFormat.IsValid(phone)) return new(null, PhoneFormat.InvalidMessage);
        if (await CheckCodeAsync(phone, r.Code, consume: false) is string cerr) return new(null, cerr);

        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Phone == phone)) return new(null, "Bu raqam allaqachon ro'yxatdan o'tgan.");

        Student? child = null;
        if (r.Role == UserRole.Parent)
        {
            if (string.IsNullOrWhiteSpace(r.ChildCode)) return new(null, "Farzandingizning kodini kiriting (o'quvchi profilida ko'rsatilgan).");
            child = await db.Students.FirstOrDefaultAsync(s => s.ParentCode == r.ChildCode.Trim().ToUpperInvariant());
            if (child is null) return new(null, "Bunday kodli o'quvchi topilmadi.");
        }

        var link = await db.TelegramLinks.FindAsync(phone);
        var user = new User
        {
            FullName = r.FullName.Trim(), Phone = phone, Role = r.Role, AvatarColor = ColorFor(phone),
            TelegramChatId = link?.ChatId, TelegramUsername = link?.Username, LastSeenAt = DateTime.UtcNow
        };
        user.PasswordHash = Hasher.HashPassword(user, r.Password);
        db.Users.Add(user);
        if (r.Role == UserRole.Student)
            user.Student = new Student { TargetScore = r.TargetScore, ParentCode = await UniqueParentCodeAsync(db) };
        await db.SaveChangesAsync();
        if (child is not null) { db.ParentStudents.Add(new ParentStudent { ParentUserId = user.Id, StudentId = child.Id }); await db.SaveChangesAsync(); }

        await db.PhoneVerifications.Where(v => v.Phone == phone && !v.Consumed).ExecuteUpdateAsync(s => s.SetProperty(v => v.Consumed, true));
        await audit.LogAsync(user.Id, "UserRegistered", "User", user.Id, $"{r.Role} via site");
        return new(user, null);
    }

    /// <summary>Telegram bot registration (contact verified by Telegram itself). No password: the user gets a magic link.</summary>
    public async Task<LoginResult> RegisterFromBotAsync(string phone, long chatId, string? username, string fullName, UserRole role, string? childCode)
    {
        if (role is not (UserRole.Student or UserRole.Parent)) return new(null, "Noto'g'ri rol.");
        if (ValidateName(fullName) is string nerr) return new(null, nerr);
        if (!await settings.GetBoolAsync(SettingKeys.AllowSelfRegistration)) return new(null, "Mustaqil ro'yxatdan o'tish o'chirilgan. O'quv markazi administratoriga murojaat qiling.");

        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Phone == phone)) return new(null, "Bu raqam allaqachon ro'yxatdan o'tgan.");
        Student? child = null;
        if (role == UserRole.Parent)
        {
            child = await db.Students.FirstOrDefaultAsync(s => s.ParentCode == (childCode ?? "").Trim().ToUpperInvariant());
            if (child is null) return new(null, "Bunday kodli o'quvchi topilmadi.");
        }
        var user = new User { FullName = fullName.Trim(), Phone = phone, Role = role, AvatarColor = ColorFor(phone), TelegramChatId = chatId, TelegramUsername = username, LastSeenAt = DateTime.UtcNow };
        db.Users.Add(user);
        if (role == UserRole.Student) user.Student = new Student { ParentCode = await UniqueParentCodeAsync(db) };
        await db.SaveChangesAsync();
        if (child is not null) { db.ParentStudents.Add(new ParentStudent { ParentUserId = user.Id, StudentId = child.Id }); await db.SaveChangesAsync(); }
        await audit.LogAsync(user.Id, "UserRegistered", "User", user.Id, $"{role} via Telegram bot");
        return new(user, null);
    }

    /// <summary>Login by Telegram code (also used for "forgot password").</summary>
    public async Task<LoginResult> LoginWithCodeAsync(string phoneInput, string code)
    {
        var phone = PhoneFormat.Normalize(phoneInput);
        if (await CheckCodeAsync(phone, code, consume: true) is string cerr) return new(null, cerr);
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.Include(u => u.Student).FirstOrDefaultAsync(u => u.Phone == phone);
        if (user is null) return new(null, "Bu raqam ro'yxatdan o'tmagan.");
        if (!user.IsActive) return new(null, "Hisobingiz faol emas. Administratorga murojaat qiling.");
        user.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return new(user, null);
    }

    public static async Task<string> UniqueParentCodeAsync(AppDbContext db)
    {
        while (true)
        {
            var code = Ids.Code(6);
            if (!await db.Students.AnyAsync(s => s.ParentCode == code)) return code;
        }
    }

    // ---------------------------------------------------------------- one-time login tokens (SignalR → cookie)

    public async Task<string> IssueLoginTokenAsync(int userId, string? returnUrl = null, TimeSpan? lifetime = null)
    {
        var token = Ids.Token(32);
        await using var db = await factory.CreateDbContextAsync();
        db.LoginTokens.Add(new LoginToken { Token = token, UserId = userId, ExpiresAt = DateTime.UtcNow + (lifetime ?? TimeSpan.FromMinutes(2)), ReturnUrl = returnUrl });
        await db.LoginTokens.Where(t => t.ExpiresAt < DateTime.UtcNow.AddHours(-1)).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        return token;
    }

    public async Task<(User? User, string? ReturnUrl)> ConsumeLoginTokenAsync(string token)
    {
        await using var db = await factory.CreateDbContextAsync();
        var t = await db.LoginTokens.FindAsync(token);
        if (t is null || t.Used || t.ExpiresAt < DateTime.UtcNow) return (null, null);
        t.Used = true;
        var user = await db.Users.Include(u => u.Student).FirstOrDefaultAsync(u => u.Id == t.UserId && u.IsActive);
        if (user is not null) user.LastSeenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (user, t.ReturnUrl);
    }

    public static ClaimsPrincipal BuildPrincipal(User user, string scheme)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("phone", user.Phone),
            new("can_manage_questions", user.CanManageQuestions ? "true" : "false")
        };
        if (user.Student is not null) claims.Add(new Claim("student_id", user.Student.Id.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
    }

    public static string HomeFor(UserRole role) => role switch
    {
        UserRole.Student => "/dashboard",
        UserRole.Parent => "/parent",
        UserRole.Teacher or UserRole.Assistant => "/teacher",
        _ => "/admin"
    };

    // ---------------------------------------------------------------- profile

    public async Task<string?> ChangePasswordAsync(int userId, string? currentPassword, string newPassword)
    {
        if (ValidatePassword(newPassword) is string err) return err;
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(userId);
        if (user is null) return "Foydalanuvchi topilmadi.";
        if (user.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(currentPassword) || Hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
                return "Joriy parol noto'g'ri.";
        }
        user.PasswordHash = Hasher.HashPassword(user, newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> UpdateProfileAsync(int userId, string fullName, string? email, int? targetScore, bool nTests, bool nAtt, bool nHw, bool nRem)
    {
        if (ValidateName(fullName) is string err) return err;
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.Include(u => u.Student).FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return "Foydalanuvchi topilmadi.";
        user.FullName = fullName.Trim();
        user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        user.NotifyTestResults = nTests; user.NotifyAttendance = nAtt; user.NotifyHomework = nHw; user.NotifyPracticeReminder = nRem;
        user.UpdatedAt = DateTime.UtcNow;
        if (user.Student is not null)
        {
            if (targetScore is not null && (targetScore < 400 || targetScore > 1600)) return "Maqsad ball 400 dan 1600 gacha bo'lishi kerak.";
            user.Student.TargetScore = targetScore;
        }
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<User?> GetUserAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().Include(u => u.Student).FirstOrDefaultAsync(u => u.Id == userId);
    }

    /// <summary>A parent links another child by code from the profile page.</summary>
    public async Task<string?> LinkChildAsync(int parentUserId, string code)
    {
        await using var db = await factory.CreateDbContextAsync();
        var child = await db.Students.FirstOrDefaultAsync(s => s.ParentCode == code.Trim().ToUpperInvariant());
        if (child is null) return "Bunday kodli o'quvchi topilmadi.";
        if (await db.ParentStudents.AnyAsync(p => p.ParentUserId == parentUserId && p.StudentId == child.Id)) return "Bu o'quvchi allaqachon bog'langan.";
        db.ParentStudents.Add(new ParentStudent { ParentUserId = parentUserId, StudentId = child.Id });
        await db.SaveChangesAsync();
        return null;
    }
}
