using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

/// <summary>
/// JavohirsMath — mentor / teacher Telegram bot. Everything is done inside Telegram (no site links):
/// students see schedule, homework (and submit answers), results, a mini test, materials, ask the mentor and send receipts;
/// parents follow their children; staff (admin / teacher / assistant) run groups, attendance, homework, reviews,
/// broadcasts, the question inbox, debtors, risk signals and reports. Data is the same database as the SAT platform.
/// </summary>
public partial class MentorBotService(
    MentorBotClient client,
    MentorBotSender sender,
    MentorBotStatus status,
    IDbContextFactory<AppDbContext> factory,
    SettingsService settings,
    AuthService auth,
    AcademyService academy,
    HomeworkService homework,
    MaterialService materials,
    CrmService crm,
    MediaService media,
    AuditService audit,
    IConfiguration config,
    ILogger<MentorBotService> logger) : BackgroundService
{
    public const string TokenSetting = "MentorBotToken";

    private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    // ---------------------------------------------------------------- keyboards

    private const string BtnSchedule = "📅 Jadval", BtnHomework = "📝 Vazifalar", BtnResults = "📊 Natijalarim", BtnQuiz = "🧠 Mini test",
        BtnMaterials = "📚 Materiallar", BtnAsk = "❓ Mentorga savol", BtnPayment = "💳 To'lov", BtnHelp = "ℹ️ Yordam", BtnChild = "👶 Farzandim",
        BtnGroups = "👥 Guruhlar", BtnAttendance = "✅ Davomat", BtnNewHw = "📝 Vazifa berish", BtnSubmissions = "📥 Topshirilganlar",
        BtnBroadcast = "📣 Xabar yuborish", BtnInbox = "❓ Savollar", BtnNewStudents = "🆕 Yangi o'quvchilar", BtnDebtors = "💰 Qarzdorlar",
        BtnRisk = "⚠️ Xavf signallari", BtnReport = "📈 Hisobot";

    private static object Reply(params string[][] rows) => new { keyboard = rows.Select(r => r.Select(t => new { text = t })), resize_keyboard = true, is_persistent = true };

    private static readonly object StudentMenu = Reply([BtnSchedule, BtnHomework], [BtnResults, BtnQuiz], [BtnMaterials, BtnAsk], [BtnPayment, BtnHelp]);
    private static readonly object ParentMenu = Reply([BtnChild, BtnSchedule], [BtnPayment, BtnAsk], [BtnMaterials, BtnHelp]);
    private static readonly object StaffMenu = Reply([BtnGroups, BtnAttendance], [BtnNewHw, BtnSubmissions], [BtnBroadcast, BtnInbox], [BtnNewStudents, BtnDebtors], [BtnRisk, BtnReport], [BtnMaterials]);
    private static readonly object TeacherMenu = Reply([BtnGroups, BtnAttendance], [BtnNewHw, BtnSubmissions], [BtnBroadcast, BtnInbox], [BtnRisk, BtnReport], [BtnMaterials]);
    private static readonly object ContactKeyboard = new { keyboard = new[] { new[] { new { text = "📱 Telefon raqamni yuborish", request_contact = true } } }, resize_keyboard = true, one_time_keyboard = true };
    private static readonly object RemoveKeyboard = new { remove_keyboard = true };

    private record B(string Text, string Data);
    private static object Inline(IEnumerable<IEnumerable<B>> rows) => new { inline_keyboard = rows.Select(r => r.Select(b => new { text = b.Text, callback_data = b.Data })) };
    private static object Inline(params B[][] rows) => Inline(rows.AsEnumerable());
    private static object Column(IEnumerable<B> buttons) => Inline(buttons.Select(b => new[] { b }));

    /// <summary>The user as the bot sees them: the bot role (if set) replaces the site role. Detached copy, never saved.</summary>
    internal static User BotView(User u) => u.BotRole is not UserRole r || r == u.Role ? u
        : new User { Id = u.Id, FullName = u.FullName, Phone = u.Phone, Role = r, IsActive = u.IsActive, TelegramChatId = u.TelegramChatId, TelegramUsername = u.TelegramUsername, CanUseSite = u.CanUseSite, Student = u.Student };

    private static bool IsStaff(User? u) => u?.Role is UserRole.Admin or UserRole.SuperAdmin or UserRole.Teacher or UserRole.Assistant;
    private static bool IsAdmin(User? u) => u?.Role is UserRole.Admin or UserRole.SuperAdmin;

    // a student who also got a staff role in the bot keeps the student buttons under the staff ones
    private static readonly string[][] StudentRows = [[BtnSchedule, BtnHomework], [BtnResults, BtnQuiz], [BtnAsk, BtnPayment]];
    private static readonly object StaffStudentMenu = Reply([[BtnGroups, BtnAttendance], [BtnNewHw, BtnSubmissions], [BtnBroadcast, BtnInbox], [BtnNewStudents, BtnDebtors], [BtnRisk, BtnReport], [BtnMaterials], .. StudentRows]);
    private static readonly object TeacherStudentMenu = Reply([[BtnGroups, BtnAttendance], [BtnNewHw, BtnSubmissions], [BtnBroadcast, BtnInbox], [BtnRisk, BtnReport], [BtnMaterials], .. StudentRows]);

    private static object MenuFor(User u) => u.Role switch
    {
        UserRole.Student => StudentMenu,
        UserRole.Parent => ParentMenu,
        UserRole.Admin or UserRole.SuperAdmin => u.Student is null ? StaffMenu : StaffStudentMenu,
        _ => u.Student is null ? TeacherMenu : TeacherStudentMenu
    };

    // ---------------------------------------------------------------- polling

    private async Task<string?> ConfiguredTokenAsync()
    {
        var t = await settings.GetAsync(TokenSetting);
        if (string.IsNullOrWhiteSpace(t)) t = config["MentorBot:Token"];
        return string.IsNullOrWhiteSpace(t) ? null : t.Trim();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        long offset = 0;
        while (!ct.IsCancellationRequested)
        {
            var token = await SafeTokenAsync();
            if (token != client.Token)
            {
                client.SetToken(token);
                offset = 0;
                status.Running = false; status.Username = null; status.LastError = null;
                if (token is not null) await BootAsync(ct);
            }
            if (!client.IsConfigured)
            {
                try { await Task.Delay(10000, ct); } catch (OperationCanceledException) { }
                continue;
            }
            try
            {
                var updates = await client.GetUpdatesAsync(offset, 25, ct);
                status.Running = true; status.LastError = null;
                foreach (var u in updates)
                {
                    offset = u.UpdateId + 1;
                    status.LastUpdateAt = DateTime.UtcNow; status.HandledUpdates++;
                    try { await HandleAsync(u, ct); }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Mentor bot: error handling update {Id}", u.UpdateId);
                        try { await sender.SendAsync(u.ChatId, "⚠️ Xatolik yuz berdi. Qaytadan urinib ko'ring yoki /start.", null, ct); } catch { }
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                status.LastError = "Bu token bilan boshqa server ham ishlayapti (409).";
                try { await Task.Delay(15000, ct); } catch (OperationCanceledException) { }
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                status.Running = false; status.LastError = "Token noto'g'ri yoki bekor qilingan.";
                try { await Task.Delay(30000, ct); } catch (OperationCanceledException) { }
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
                logger.LogWarning(ex, "Mentor bot polling error, retrying in 5s");
                try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { }
            }
        }
        status.Running = false;
    }

    private async Task<string?> SafeTokenAsync()
    {
        try { return await ConfiguredTokenAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Mentor bot: cannot read token"); return client.Token; }
    }

    private async Task BootAsync(CancellationToken ct)
    {
        await client.CallAsync("deleteWebhook", new { drop_pending_updates = false }, ct);
        status.Username = await client.GetMeAsync(ct);
        status.StartedAt = DateTime.UtcNow;
        await client.CallAsync("setMyCommands", new
        {
            commands = new[]
            {
                new { command = "start", description = "Bosh menyu / ro'yxatdan o'tish" },
                new { command = "jadval", description = "Dars jadvali" },
                new { command = "vazifa", description = "Joriy vazifalar" },
                new { command = "test", description = "Mini test" },
                new { command = "natija", description = "Mening natijalarim" },
                new { command = "materiallar", description = "Kutubxona" },
                new { command = "savol", description = "Mentorga savol" },
                new { command = "tolov", description = "To'lov holati" },
                new { command = "admin", description = "Mentor paneli (xodimlar)" },
                new { command = "cancel", description = "Amalni bekor qilish" },
            }
        }, ct);
        logger.LogInformation("Mentor bot started (@{Bot})", status.Username);
    }

    // ---------------------------------------------------------------- state helpers

    private sealed class Ctx
    {
        public required BotUpdate U { get; init; }
        public required AppDbContext Db { get; init; }
        public required MentorBotChat Chat { get; init; }
        public User? User { get; set; }
        public CancellationToken Ct { get; init; }
        public long ChatId => Chat.ChatId;
        public string Text => (U.Text ?? "").Trim();

        private Dictionary<string, string>? _data;
        public Dictionary<string, string> Data => _data ??= string.IsNullOrEmpty(Chat.Data) ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(Chat.Data) ?? [];
        public string? Get(string k) => Data.GetValueOrDefault(k);
        public int GetInt(string k) => int.TryParse(Get(k), out var i) ? i : 0;
        public void Set(string k, string? v) { if (v is null) Data.Remove(k); else Data[k] = v; }

        public async Task SaveAsync()
        {
            if (_data is not null) Chat.Data = _data.Count == 0 ? null : JsonSerializer.Serialize(_data);
            await Db.SaveChangesAsync(Ct);
        }

        public async Task StepAsync(string step, bool clearData = false)
        {
            Chat.Step = step;
            if (clearData) { _data = []; }
            await SaveAsync();
        }

        public Task ResetAsync() => StepAsync("", clearData: true);
    }

    private Task<long?> Say(Ctx c, string html, object? markup = null) => sender.SendAsync(c.ChatId, html, markup, c.Ct);

    /// <summary>Edits the message the button was on (callbacks) or sends a new one.</summary>
    private async Task Show(Ctx c, string html, object? inline = null)
    {
        if (c.U.IsCallback && c.U.MessageId != 0) await sender.EditAsync(c.ChatId, c.U.MessageId, html, inline, c.Ct);
        else await Say(c, html, inline);
    }

    // ---------------------------------------------------------------- dispatch

    internal async Task HandleAsync(BotUpdate u, CancellationToken ct)
    {
        if (u.ChatId == 0 || !u.PrivateChat) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var chat = await db.MentorBotChats.FindAsync([u.ChatId], ct);
        if (chat is null)
        {
            chat = new MentorBotChat { ChatId = u.ChatId };
            db.MentorBotChats.Add(chat);
        }
        chat.FirstName = u.FirstName ?? chat.FirstName; chat.Username = u.Username ?? chat.Username;
        chat.LastSeenAt = DateTime.UtcNow; chat.IsBlocked = false;
        await db.SaveChangesAsync(ct);

        var user = chat.UserId is int uid ? await db.Users.AsNoTracking().Include(x => x.Student).FirstOrDefaultAsync(x => x.Id == uid, ct) : null;
        if (user is not null) user = BotView(user);
        if (user is not null && !user.IsActive) user = null;
        var c = new Ctx { U = u, Db = db, Chat = chat, User = user, Ct = ct };

        await sender.LogAsync(u.ChatId, true, u.IsCallback ? $"[tugma] {u.CallbackData}" : DescribeIncoming(u));
        if (u.IsCallback)
        {
            await client.AnswerCallbackAsync(u.CallbackId!, null, ct);
            await HandleCallbackAsync(c, u.CallbackData ?? "");
            return;
        }
        await HandleMessageAsync(c);
    }

    private static string DescribeIncoming(BotUpdate u)
    {
        if (u.ContactPhone is not null) return $"[kontakt] {u.ContactPhone}";
        var media = u.MediaKind switch { "photo" => "[rasm] ", "document" => $"[fayl: {u.FileName}] ", "voice" => "[ovozli xabar] ", "video" => "[video] ", _ => "" };
        return media + (u.Text ?? "");
    }

    private async Task HandleMessageAsync(Ctx c)
    {
        var t = c.Text;

        if (c.U.ContactPhone is not null) { await OnContactAsync(c); return; }

        if (t.StartsWith("/start", StringComparison.OrdinalIgnoreCase) || t.Equals("/menu", StringComparison.OrdinalIgnoreCase))
        {
            await c.ResetAsync();
            await GreetAsync(c);
            return;
        }
        if (t.Equals("/cancel", StringComparison.OrdinalIgnoreCase) || t is "❌ Bekor qilish")
        {
            await c.ResetAsync();
            await Say(c, "Bekor qilindi.", c.User is null ? ContactKeyboard : MenuFor(c.User));
            return;
        }

        // registration wizard (not linked yet)
        if (c.User is null)
        {
            if (c.Chat.Step.StartsWith("reg_")) { await RegistrationStepAsync(c); return; }
            await GreetAsync(c);
            return;
        }

        // a wizard step waiting for input wins over menu buttons only for free text (menu buttons always work)
        if (!IsMenuText(t) && !t.StartsWith('/') && c.Chat.Step.Length > 0)
        {
            if (await HandleStepAsync(c)) return;
        }
        if (IsMenuText(t) || t.StartsWith('/')) { c.Chat.Step = ""; c.Chat.Data = null; await c.Db.SaveChangesAsync(c.Ct); }

        var cmd = t.Split(' ', 2)[0].ToLowerInvariant();
        if (t == BtnMaterials || cmd == "/materiallar") { await MaterialsAsync(c, null); return; }
        if (IsStaff(c.User))
        {
            switch (t)
            {
                case BtnGroups: await StaffGroupsAsync(c, "g"); return;
                case BtnAttendance: await StaffGroupsAsync(c, "att"); return;
                case BtnNewHw: await StaffGroupsAsync(c, "hwn"); return;
                case BtnSubmissions: await SubmissionsAsync(c); return;
                case BtnBroadcast: await BroadcastStartAsync(c); return;
                case BtnInbox: await InboxAsync(c); return;
                case BtnNewStudents: await NewStudentsAsync(c); return;
                case BtnDebtors: await DebtorsAsync(c); return;
                case BtnRisk: await RiskAsync(c); return;
                case BtnReport: await StaffGroupsAsync(c, "rep"); return;
            }
            if (cmd is "/admin" or "/help" or "/start") { await Say(c, StaffHelp(c.User!), MenuFor(c.User!)); return; }
        }
        else if (cmd == "/admin") { await Say(c, "⛔ Mentor paneli faqat xodimlar uchun.", MenuFor(c.User)); return; }

        if (c.User.Role == UserRole.Parent)
        {
            switch (t)
            {
                case BtnChild: await ChildrenAsync(c); return;
                case BtnSchedule or "/jadval": await ScheduleAsync(c); return;
                case BtnPayment or "/tolov": await PaymentAsync(c); return;
                case BtnAsk or "/savol": await AskStartAsync(c); return;
                case "/natija": await ChildrenAsync(c); return;
            }
        }
        if (c.User.Role == UserRole.Student || (IsStaff(c.User) && c.User.Student is not null))
        {
            switch (cmd == "" ? t : t.StartsWith('/') ? cmd : t)
            {
                case BtnSchedule or "/jadval": await ScheduleAsync(c); return;
                case BtnHomework or "/vazifa": await StudentHomeworkAsync(c); return;
                case BtnResults or "/natija": await ResultsAsync(c); return;
                case BtnQuiz or "/test": await QuizStartAsync(c); return;
                case BtnMaterials or "/materiallar": await MaterialsAsync(c, null); return;
                case BtnAsk or "/savol": await AskStartAsync(c); return;
                case BtnPayment or "/tolov": await PaymentAsync(c); return;
            }
        }
        if (t is BtnHelp || cmd == "/help") { await Say(c, UserHelp(c.User), MenuFor(c.User)); return; }

        await Say(c, "Menyudan tanlang 👇", MenuFor(c.User));
    }

    private static readonly HashSet<string> MenuTexts = [BtnSchedule, BtnHomework, BtnResults, BtnQuiz, BtnMaterials, BtnAsk, BtnPayment, BtnHelp, BtnChild,
        BtnGroups, BtnAttendance, BtnNewHw, BtnSubmissions, BtnBroadcast, BtnInbox, BtnNewStudents, BtnDebtors, BtnRisk, BtnReport];
    private static bool IsMenuText(string t) => MenuTexts.Contains(t);

    private async Task<bool> HandleStepAsync(Ctx c) => c.Chat.Step switch
    {
        "ask" or "receipt" => await AskReceiveAsync(c),
        "sub" => await SubmitReceiveAsync(c),
        "msearch" => await MaterialsSearchAsync(c),
        _ when c.Chat.Step.StartsWith("pdf_") => await PdfStepAsync(c),
        _ when c.Chat.Step.StartsWith("s_") && IsStaff(c.User) => await StaffStepAsync(c),
        _ => false
    };

    private async Task HandleCallbackAsync(Ctx c, string data)
    {
        if (data == "x") return;
        if (data == "cancel") { await c.ResetAsync(); await Show(c, "Bekor qilindi."); return; }
        var p = data.Split(':');
        int I(int i) => p.Length > i && int.TryParse(p[i], out var v) ? v : 0;

        if (c.User is null)
        {
            if (p[0] is "reg" or "rc") { await RegistrationCallbackAsync(c, p); return; }
            await GreetAsync(c);
            return;
        }

        // student / parent callbacks
        switch (p[0])
        {
            case "sh": await StudentHomeworkDetailAsync(c, I(1)); return;
            case "sb": await SubmitStartAsync(c, I(1)); return;
            case "m": await MaterialSendAsync(c, I(1)); return;
            case "pdfadd" or "pdfe" or "pdfr" or "pdff" or "pdfc" or "pdfcat" or "pdfd" or "pdfdd" or "pdfann": await PdfCallbackAsync(c, p); return;
            case "msr": await c.StepAsync("msearch", true); await Say(c, "🔎 Qidiruv so'zini yozing (masalan: <i>kvadrat tenglama</i>):"); return;
            case "qs": await QuizBeginAsync(c, p.Length > 1 ? p[1] : "x"); return;
            case "qz": await QuizAnswerAsync(c, I(1), p.Length > 2 ? p[2] : ""); return;
            case "rcp": await c.StepAsync("receipt", true); await Say(c, "🧾 To'lov chekining rasmini (yoki PDF) yuboring:"); return;
        }
        if (!IsStaff(c.User)) { await Say(c, "⛔ Bu amal faqat xodimlar uchun."); return; }
        await StaffCallbackAsync(c, p);
    }

    // ---------------------------------------------------------------- start / registration

    private async Task GreetAsync(Ctx c)
    {
        if (c.User is not null)
        {
            var hello = $"Assalomu alaykum, <b>{H(c.User.FullName)}</b>! 👋\n\n" + (IsStaff(c.User) ? StaffHelp(c.User) : UserHelp(c.User));
            await Say(c, hello, MenuFor(c.User));
            return;
        }
        await Say(c,
            "Assalomu alaykum! 👋\n<b>JavohirsMath</b> — o'qituvchi va mentor boti.\n\n" +
            "Bu yerda dars jadvali, uy vazifalari, testlar, natijalar, materiallar va to'lovlar — hammasi Telegramda.\n\n" +
            "Boshlash uchun pastdagi tugma orqali <b>telefon raqamingizni</b> yuboring 👇", ContactKeyboard);
    }

    private static string UserHelp(User u) => u.Role == UserRole.Parent
        ? "👶 <b>Farzandim</b> — davomat, vazifalar, ballar\n📅 <b>Jadval</b> — darslar vaqti\n💳 <b>To'lov</b> — balans va chek yuborish\n❓ <b>Mentorga savol</b>\n\nFarzandingiz darsga kelmasa, shu yerga darhol xabar keladi. Har yakshanba haftalik hisobot yuboriladi."
        : "📅 /jadval — dars jadvali (dars oldidan eslatma keladi)\n📝 /vazifa — uy vazifalari va javob yuborish\n🧠 /test — mini test\n📊 /natija — natijalarim\n📚 /materiallar — kutubxona\n❓ /savol — mentorga savol\n💳 /tolov — to'lov holati\n/cancel — amalni bekor qilish";

    private static string StaffHelp(User u) =>
        "🛠 <b>Mentor paneli</b>\n\n" +
        "👥 Guruhlar — o'quvchilar, davomat, vazifa, xabar, hisobot\n✅ Davomat — bir bosishda belgilash\n📝 Vazifa berish — matn/fayl, muddat\n" +
        "📥 Topshirilganlar — ko'rib chiqish, baho va izoh\n📣 Xabar yuborish — guruh, o'quvchilar, ota-onalar, rejalashtirish\n❓ Savollar — o'quvchi savollari va to'lov cheklari\n" +
        (u.Role is UserRole.Admin or UserRole.SuperAdmin ? "🆕 Yangi o'quvchilar — guruhga qo'shish\n💰 Qarzdorlar — eslatma, to'lov kiritish\n" : "") +
        "⚠️ Xavf signallari — dars qoldirgan / vazifa topshirmaganlar\n📈 Hisobot — guruh statistikasi va CSV";

    /// <summary>Phones from the MentorBot:AdminPhones server setting (comma separated) become admins when they share their contact.</summary>
    private HashSet<string> AdminPhones() => (config["MentorBot:AdminPhones"] ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries)
        .Select(PhoneFormat.Normalize).Where(PhoneFormat.IsValid).ToHashSet();

    private async Task<User> EnsureAdminAsync(Ctx c, string phone, User? user)
    {
        if (user is null)
        {
            user = new User { FullName = string.IsNullOrWhiteSpace(c.U.FirstName) ? "Admin" : c.U.FirstName.Trim(), Phone = phone, Role = UserRole.Admin,
                AvatarColor = AuthService.ColorFor(phone), TelegramChatId = c.ChatId, TelegramUsername = c.U.Username };
            c.Db.Users.Add(user);
            await c.Db.SaveChangesAsync(c.Ct);
            await audit.LogAsync(user.Id, "MentorBotAdminCreated", "User", user.Id, phone);
        }
        else if (user.Role is not (UserRole.Admin or UserRole.SuperAdmin) || !user.IsActive)
        {
            var old = user.Role;
            if (user.Role is not (UserRole.Admin or UserRole.SuperAdmin)) user.Role = UserRole.Admin;
            user.IsActive = true;
            await c.Db.SaveChangesAsync(c.Ct);
            await audit.LogAsync(user.Id, "MentorBotAdminPromoted", "User", user.Id, $"{old} -> {user.Role}");
        }
        return user;
    }

    private async Task OnContactAsync(Ctx c)
    {
        if (c.U.ContactUserId is long cu && cu != c.U.FromId) { await Say(c, "Iltimos, o'zingizning raqamingizni yuboring.", ContactKeyboard); return; }
        var phone = PhoneFormat.Normalize(c.U.ContactPhone);
        if (!PhoneFormat.IsValid(phone)) { await Say(c, "⚠️ Faqat O'zbekiston raqamlari (+998) qabul qilinadi.", ContactKeyboard); return; }
        c.Chat.Phone = phone;
        var user = await c.Db.Users.Include(x => x.Student).FirstOrDefaultAsync(x => x.Phone == phone, c.Ct);
        if (AdminPhones().Contains(phone)) user = await EnsureAdminAsync(c, phone, user);
        if (user is not null)
        {
            if (!user.IsActive) { await c.ResetAsync(); await Say(c, "⛔ Hisobingiz faol emas. Administratorga murojaat qiling.", RemoveKeyboard); return; }
            c.Chat.UserId = user.Id;
            user.TelegramChatId ??= c.ChatId;
            user.TelegramUsername ??= c.U.Username;
            c.User = BotView(user);
            await c.ResetAsync();
            await audit.LogAsync(user.Id, "MentorBotLinked", "User", user.Id, $"chat {c.ChatId}");
            await Say(c, $"✅ Hisobingiz bog'landi: <b>{H(user.FullName)}</b> ({Labels.Role(c.User.Role)}).", MenuFor(c.User));
            await GreetAsync(c);
            return;
        }
        await c.StepAsync("reg_role", true);
        await Say(c, $"📱 {PhoneFormat.Display(phone)}\n\nBu raqam hali ro'yxatdan o'tmagan. Ro'yxatdan o'tamiz! Siz kimsiz?",
            Inline([new B("🎓 O'quvchiman", "reg:s"), new B("👨‍👩‍👧 Ota-onaman", "reg:p")]));
        await Say(c, "Tanlang 👆", RemoveKeyboard);
    }

    private static readonly string[] Courses = ["SAT Math", "SAT R&W", "IELTS", "DTM", "5–11-sinf matematikasi"];

    private async Task RegistrationCallbackAsync(Ctx c, string[] p)
    {
        if (c.Chat.Phone is null) { await GreetAsync(c); return; }
        if (p[0] == "reg")
        {
            c.Set("role", p.ElementAtOrDefault(1) == "p" ? "Parent" : "Student");
            await c.StepAsync("reg_name");
            await Show(c, "✍️ Ism-familiyangizni yozing (masalan: <i>Ali Valiyev</i>):");
            return;
        }
        if (p[0] == "rc" && c.Chat.Step == "reg_course" && int.TryParse(p.ElementAtOrDefault(1), out var i) && i >= 0 && i < Courses.Length)
        {
            await Show(c, $"Kurs: <b>{H(Courses[i])}</b>");
            await FinishRegistrationAsync(c, Courses[i], null);
        }
    }

    private async Task RegistrationStepAsync(Ctx c)
    {
        var t = c.Text;
        switch (c.Chat.Step)
        {
            case "reg_name":
                if (AuthService.ValidateName(t) is string err) { await Say(c, err); return; }
                c.Set("name", t);
                if (c.Get("role") == "Parent")
                {
                    await c.StepAsync("reg_child");
                    await Say(c, "Farzandingizning <b>ota-ona kodini</b> yozing (6 belgili kod, uni mentor yoki farzandingiz beradi):");
                    return;
                }
                await c.StepAsync("reg_grade");
                await Say(c, "🏫 Nechanchi sinfda o'qiysiz yoki yoshingiz nechada? (masalan: <i>10-sinf</i> yoki <i>17 yosh</i>)");
                return;
            case "reg_grade":
                if (t.Length is < 1 or > 30) { await Say(c, "Qisqa yozing, masalan: 10-sinf"); return; }
                c.Set("grade", t);
                await c.StepAsync("reg_course");
                await Say(c, "📘 Qaysi kursda o'qimoqchisiz?", Column(Courses.Select((x, i) => new B(x, $"rc:{i}"))));
                return;
            case "reg_child":
                await FinishRegistrationAsync(c, null, t);
                return;
            default:
                await GreetAsync(c);
                return;
        }
    }

    private async Task FinishRegistrationAsync(Ctx c, string? course, string? childCode)
    {
        var role = c.Get("role") == "Parent" ? UserRole.Parent : UserRole.Student;
        var r = await auth.RegisterFromBotAsync(c.Chat.Phone!, c.ChatId, c.U.Username, c.Get("name") ?? "", role, childCode);
        if (r.Error is not null)
        {
            if (role == UserRole.Parent && c.Chat.Step == "reg_child") { await Say(c, "❌ " + H(r.Error) + "\nKodni qayta yozing yoki /cancel."); return; }
            await c.ResetAsync();
            await Say(c, "❌ " + H(r.Error));
            return;
        }
        var user = r.User!;
        c.Chat.UserId = user.Id; c.User = user;
        int? studentId = null;
        if (role == UserRole.Student)
        {
            var st = await c.Db.Students.FirstAsync(s => s.UserId == user.Id, c.Ct);
            st.Notes = $"Sinf/yosh: {c.Get("grade")}; Kurs: {course} (JavohirsMath bot)";
            st.Status = StudentStatus.Applicant;
            studentId = st.Id;
        }
        await c.ResetAsync();
        await Say(c, $"🎉 Ro'yxatdan o'tdingiz, <b>{H(user.FullName)}</b>!\n\n" +
            (role == UserRole.Student ? "Mentor sizni guruhga qo'shgach, dars jadvali va vazifalar shu yerda chiqadi." : "Farzandingiz ma'lumotlari endi shu yerda.") +
            "\n\n" + UserHelp(user), MenuFor(user));

        var info = role == UserRole.Student
            ? $"🆕 <b>Yangi o'quvchi</b> (bot orqali)\n👤 {H(user.FullName)}\n📱 {PhoneFormat.Display(user.Phone)}\n🏫 {H(c.Get("grade"))}\n📘 {H(course)}"
            : $"🆕 <b>Yangi ota-ona</b> (bot orqali)\n👤 {H(user.FullName)}\n📱 {PhoneFormat.Display(user.Phone)}";
        await NotifyAdminsAsync(info, studentId is int sid ? Inline([new B("➕ Guruhga qo'shish", $"en:{sid}"), new B("👤 Karta", $"st:{sid}")]) : null, c.Ct);
    }

    // ---------------------------------------------------------------- shared helpers

    private async Task<List<int>> AdminUserIdsAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Users.Where(u => u.IsActive && ((u.BotRole ?? u.Role) == UserRole.Admin || (u.BotRole ?? u.Role) == UserRole.SuperAdmin)).Select(u => u.Id).ToListAsync(ct);
    }

    private async Task NotifyAdminsAsync(string html, object? markup, CancellationToken ct)
    {
        foreach (var id in await AdminUserIdsAsync(ct)) await sender.SendToUserAsync(id, html, markup, ct);
    }

    /// <summary>Teachers / assistants of the student's active groups.</summary>
    private async Task<List<int>> TeachersOfStudentAsync(AppDbContext db, int studentId, CancellationToken ct)
    {
        var groups = await db.Enrollments.Where(e => e.StudentId == studentId && e.Status == EnrollmentStatus.Active).Select(e => new { e.Group.TeacherUserId, e.Group.AssistantUserId }).ToListAsync(ct);
        return groups.SelectMany(g => new[] { g.TeacherUserId, g.AssistantUserId }).OfType<int>().Distinct().ToList();
    }

    private async Task<int?> StudentIdAsync(Ctx c) => c.User is null ? null : await c.Db.Students.Where(s => s.UserId == c.User.Id).Select(s => (int?)s.Id).FirstOrDefaultAsync(c.Ct);

    /// <summary>Students the current user acts for: themself (student) or their children (parent).</summary>
    private async Task<List<Student>> MyStudentsAsync(Ctx c)
    {
        if (c.User is null) return [];
        var q = c.Db.Students.AsNoTracking().Include(s => s.User).AsQueryable();
        return c.User.Role == UserRole.Parent
            ? await q.Where(s => s.Parents.Any(p => p.ParentUserId == c.User.Id)).ToListAsync(c.Ct)
            : await q.Where(s => s.UserId == c.User.Id).ToListAsync(c.Ct);
    }

    private static string Time(Group g) => g.StartTime is null ? "" : $"{g.StartTime:HH\\:mm}" + (g.EndTime is null ? "" : $"–{g.EndTime:HH\\:mm}");
}
