using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

/// <summary>Keys of the configurable values in <see cref="AppSetting"/> and their defaults.</summary>
public static class SettingKeys
{
    public const string CenterName = "CenterName";
    public const string WeakThreshold = "WeakThreshold";          // accuracy below this = weak
    public const string StrongThreshold = "StrongThreshold";      // accuracy above this = strong
    public const string MinSkillSample = "MinSkillSample";        // attempts needed before a skill is judged
    public const string BreakSeconds = "BreakSeconds";
    public const string RwModuleSeconds = "RwModuleSeconds";
    public const string MathModuleSeconds = "MathModuleSeconds";
    public const string RwModuleQuestions = "RwModuleQuestions";
    public const string MathModuleQuestions = "MathModuleQuestions";
    public const string PracticeReminderHour = "PracticeReminderHour"; // local hour (Asia/Tashkent)
    public const string DefaultScoringVersion = "DefaultScoringVersion";
    public const string RecommendedQuestionCount = "RecommendedQuestionCount";
    public const string AllowSelfRegistration = "AllowSelfRegistration";

    public static readonly Dictionary<string, string> Defaults = new()
    {
        [CenterName] = "SAT Center",
        [WeakThreshold] = "50",
        [StrongThreshold] = "75",
        [MinSkillSample] = "5",
        [BreakSeconds] = "600",
        [RwModuleSeconds] = "1920",
        [MathModuleSeconds] = "2100",
        [RwModuleQuestions] = "27",
        [MathModuleQuestions] = "22",
        [PracticeReminderHour] = "18",
        [DefaultScoringVersion] = "SAT_2026_V1",
        [RecommendedQuestionCount] = "15",
        [AllowSelfRegistration] = "true",
    };
}

/// <summary>Cached read access to <see cref="AppSetting"/> rows.</summary>
public class SettingsService(IDbContextFactory<AppDbContext> factory)
{
    private Dictionary<string, string>? _cache;
    private DateTime _loadedAt;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<Dictionary<string, string>> AllAsync()
    {
        if (_cache is not null && DateTime.UtcNow - _loadedAt < TimeSpan.FromSeconds(30)) return _cache;
        await _lock.WaitAsync();
        try
        {
            if (_cache is not null && DateTime.UtcNow - _loadedAt < TimeSpan.FromSeconds(30)) return _cache;
            await using var db = await factory.CreateDbContextAsync();
            var rows = await db.AppSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
            var merged = new Dictionary<string, string>(SettingKeys.Defaults);
            foreach (var (k, v) in rows) merged[k] = v;
            _cache = merged;
            _loadedAt = DateTime.UtcNow;
            return merged;
        }
        finally { _lock.Release(); }
    }

    public async Task<string> GetAsync(string key) => (await AllAsync()).TryGetValue(key, out var v) ? v : SettingKeys.Defaults.GetValueOrDefault(key, "");

    public async Task<int> GetIntAsync(string key)
    {
        var raw = await GetAsync(key);
        return int.TryParse(raw, out var i) ? i : int.Parse(SettingKeys.Defaults[key]);
    }

    public async Task<bool> GetBoolAsync(string key) => (await GetAsync(key)).Equals("true", StringComparison.OrdinalIgnoreCase);

    public async Task SetAsync(string key, string value)
    {
        await using var db = await factory.CreateDbContextAsync();
        var row = await db.AppSettings.FindAsync(key);
        if (row is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else row.Value = value;
        await db.SaveChangesAsync();
        Invalidate();
    }

    public void Invalidate() => _cache = null;
}

/// <summary>Who is using the current Blazor circuit (from the auth cookie claims).</summary>
public class CurrentUser(AuthenticationStateProvider provider)
{
    private ClaimsPrincipal? _principal;

    public async Task<ClaimsPrincipal> PrincipalAsync() => _principal ??= (await provider.GetAuthenticationStateAsync()).User;

    public async Task<int> IdAsync() => int.TryParse((await PrincipalAsync()).FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public async Task<string> NameAsync() => (await PrincipalAsync()).Identity?.Name ?? "";
    public async Task<UserRole?> RoleAsync() => Enum.TryParse<UserRole>((await PrincipalAsync()).FindFirstValue(ClaimTypes.Role), out var r) ? r : null;
    public async Task<int?> StudentIdAsync() => int.TryParse((await PrincipalAsync()).FindFirstValue("student_id"), out var id) ? id : null;
    public async Task<bool> IsAdminAsync() => (await RoleAsync()) is UserRole.Admin or UserRole.SuperAdmin;
    public async Task<bool> IsStaffAsync() => (await RoleAsync()) is UserRole.Admin or UserRole.SuperAdmin or UserRole.Teacher or UserRole.Assistant;
    public async Task<bool> CanManageQuestionsAsync() => (await PrincipalAsync()).HasClaim("can_manage_questions", "true") || await IsAdminAsync();
}

public static class Ids
{
    public static string Token(int bytes = 24) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
    public static string Code(int length = 6)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return string.Create(length, alphabet, (span, a) => { for (var i = 0; i < span.Length; i++) span[i] = a[RandomNumberGenerator.GetInt32(a.Length)]; });
    }
}

public static class Tz
{
    public static readonly TimeZoneInfo Tashkent = Find();

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Asia/Tashkent", "West Asia Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+5", TimeSpan.FromHours(5), "UTC+5", "UTC+5");
    }

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tashkent);
    public static DateTime ToUtc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Tashkent);
    public static DateOnly Today => DateOnly.FromDateTime(ToLocal(DateTime.UtcNow));
    public static string Fmt(DateTime? utc, string format = "dd.MM.yyyy HH:mm") => utc is null ? "—" : ToLocal(utc.Value).ToString(format);
    public static string FmtDate(DateTime? utc) => Fmt(utc, "dd.MM.yyyy");
}

public static class Labels
{
    public static string LeadStatus(LeadStatus s) => s switch { Data.LeadStatus.New => "Yangi", Data.LeadStatus.Contacted => "Bog'lanildi", Data.LeadStatus.Trial => "Sinov darsi", Data.LeadStatus.Won => "O'quvchi bo'ldi", _ => "Yo'qotildi" };
    public static string PaymentMethod(PaymentMethod m) => m switch { Data.PaymentMethod.Cash => "Naqd", Data.PaymentMethod.Card => "Karta", Data.PaymentMethod.Transfer => "O'tkazma", _ => "Hisoblash (qarz)" };
    public static string Money(decimal v) => v.ToString("#,0.##", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
    public static string Section(Section s) => s == Data.Section.RW ? "Reading & Writing" : "Math";
    public static string SectionShort(Section s) => s == Data.Section.RW ? "R&W" : "Math";
    public static string Difficulty(Difficulty d) => d switch { Data.Difficulty.Easy => "Oson", Data.Difficulty.Medium => "O'rta", _ => "Qiyin" };
    public static string Role(UserRole r) => r switch
    {
        UserRole.SuperAdmin => "Super admin",
        UserRole.Admin => "Admin",
        UserRole.Teacher => "O'qituvchi",
        UserRole.Assistant => "Assistent",
        UserRole.Student => "O'quvchi",
        UserRole.Parent => "Ota-ona",
        _ => r.ToString()
    };
    public static string TestType(TestType t) => t switch
    {
        Data.TestType.Diagnostic => "Diagnostik test",
        Data.TestType.FullMock => "To'liq SAT (Mock)",
        Data.TestType.SectionTest => "Bo'lim testi",
        Data.TestType.ModuleTest => "Modul testi",
        Data.TestType.TopicTest => "Mavzu testi",
        _ => t.ToString()
    };
    public static string TestStatus(TestStatus s) => s switch { Data.TestStatus.Draft => "Qoralama", Data.TestStatus.Published => "Chop etilgan", _ => "Arxiv" };
    public static string QuestionStatus(QuestionStatus s) => s switch { Data.QuestionStatus.Draft => "Qoralama", Data.QuestionStatus.Review => "Tekshiruvda", Data.QuestionStatus.Active => "Faol", _ => "Arxiv" };
    public static string AttemptStatus(AttemptStatus s) => s switch
    {
        Data.AttemptStatus.InProgress => "Davom etmoqda",
        Data.AttemptStatus.Break => "Tanaffus",
        Data.AttemptStatus.AutoSubmitted => "Avto-yakunlangan",
        Data.AttemptStatus.Scoring => "Baholanmoqda",
        Data.AttemptStatus.Completed => "Yakunlangan",
        _ => "Bekor qilingan"
    };
    public static string Attendance(AttendanceStatus s) => s switch { AttendanceStatus.Present => "Keldi", AttendanceStatus.Absent => "Kelmadi", AttendanceStatus.Late => "Kechikdi", _ => "Sababli" };
    public static string StudentStatus(StudentStatus s) => s switch { Data.StudentStatus.Applicant => "Nomzod", Data.StudentStatus.Active => "Faol", Data.StudentStatus.Frozen => "Muzlatilgan", Data.StudentStatus.Dropped => "Ketgan", _ => "Bitirgan" };
    public static string Submission(SubmissionStatus s, DateTime deadline) => s switch
    {
        SubmissionStatus.NotStarted => deadline < DateTime.UtcNow ? "Muddati o'tgan" : "Boshlanmagan",
        SubmissionStatus.InProgress => "Davom etmoqda",
        SubmissionStatus.Submitted => "Topshirilgan",
        _ => "Tekshirilgan"
    };
    public static string Material(MaterialCategory c) => c switch
    {
        MaterialCategory.RW => "Reading & Writing",
        MaterialCategory.Math => "Math",
        MaterialCategory.Grammar => "Grammatika",
        MaterialCategory.Vocabulary => "Lug'at",
        MaterialCategory.Practice => "Mashq",
        _ => "Boshqa"
    };
    public static string PracticeMode(PracticeMode m) => m switch
    {
        Data.PracticeMode.Quick => "Tezkor mashq",
        Data.PracticeMode.Standard => "Standart mashq",
        Data.PracticeMode.Intensive => "Intensiv mashq",
        Data.PracticeMode.Custom => "Maxsus mashq",
        Data.PracticeMode.Weakness => "Zaif skilllar",
        Data.PracticeMode.Mistake => "Xatolar ustida ishlash",
        Data.PracticeMode.Homework => "Uy vazifasi",
        _ => "Tavsiya qilingan mashq"
    };
    public static string Time(int seconds) => seconds >= 3600 ? $"{seconds / 3600}:{seconds % 3600 / 60:00}:{seconds % 60:00}" : $"{seconds / 60:00}:{seconds % 60:00}";
    public static string Minutes(int seconds) => $"{(seconds + 30) / 60} daqiqa";
    public static string Days(string days) => string.Join(" / ", days.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim() switch
    {
        "1" => "Du", "2" => "Se", "3" => "Cho", "4" => "Pa", "5" => "Ju", "6" => "Sha", "7" => "Ya", var x => x
    }));
}

public class AuditService(IDbContextFactory<AppDbContext> factory, ILogger<AuditService> logger)
{
    public async Task LogAsync(int? userId, string action, string entityType, int? entityId, string? details = null)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync();
            db.AuditLogs.Add(new AuditLog { UserId = userId, Action = action, EntityType = entityType, EntityId = entityId, Details = details?.Length > 4000 ? details[..4000] : details });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogWarning(ex, "Audit log failed: {Action}", action); }
    }
}

public class AppException(string message) : Exception(message);
