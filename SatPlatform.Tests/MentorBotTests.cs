using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SatPlatform.Data;
using SatPlatform.Services;
using SatPlatform.Services.MentorBot;
using Xunit;

namespace SatPlatform.Tests;

/// <summary>Records Bot API calls instead of sending them to Telegram.</summary>
public class FakeTelegramHandler : HttpMessageHandler
{
    public record Call(string Method, JsonNode? Body, string Raw);
    public ConcurrentQueue<Call> Calls { get; } = new();
    private int _messageId = 1000;

    public void Clear() => Calls.Clear();
    public List<Call> Sent(long chatId) => Calls.Where(c => c.Method is "sendMessage" or "editMessageText" && c.Body?["chat_id"]?.GetValue<long>() == chatId).ToList();
    public string LastText(long chatId) => Sent(chatId).LastOrDefault()?.Body?["text"]?.GetValue<string>() ?? "";

    /// <summary>callback_data of every inline button in the last message to the chat.</summary>
    public List<string> LastButtons(long chatId) =>
        Sent(chatId).LastOrDefault()?.Body?["reply_markup"]?["inline_keyboard"]?.AsArray().SelectMany(r => r!.AsArray()).Select(b => b!["callback_data"]!.GetValue<string>()).ToList() ?? [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.StartsWith("/file/")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 fake")) };
        var method = path.Split('/').Last();
        var raw = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        JsonNode? body = null;
        try { body = raw.StartsWith('{') ? JsonNode.Parse(raw) : null; } catch { }
        Calls.Enqueue(new Call(method, body, raw));
        var result = method switch
        {
            "getMe" => """{"ok":true,"result":{"username":"JavohirsMathTestBot"}}""",
            "getFile" => """{"ok":true,"result":{"file_path":"documents/a.pdf"}}""",
            _ => "{\"ok\":true,\"result\":{\"message_id\":" + Interlocked.Increment(ref _messageId) + "}}"
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(result, Encoding.UTF8, "application/json") };
    }
}

[Collection("engine")]
public class MentorBotTests(EngineFixture f)
{
    private T Get<T>() where T : notnull => f.Services.GetRequiredService<T>();
    private FakeTelegramHandler Tg => f.FakeTelegram;
    private MentorBotService Bot => Get<MentorBotService>();
    private Task<AppDbContext> DbAsync() => Get<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    private long _update = 1;
    private Task Text(long chat, string text) => Bot.HandleAsync(new BotUpdate { UpdateId = _update++, ChatId = chat, FromId = chat, PrivateChat = true, MessageId = _update, Text = text, FirstName = "T" }, default);
    private Task Contact(long chat, string phone) => Bot.HandleAsync(new BotUpdate { UpdateId = _update++, ChatId = chat, FromId = chat, PrivateChat = true, ContactPhone = phone, ContactUserId = chat }, default);
    private Task Press(long chat, string data) => Bot.HandleAsync(new BotUpdate { UpdateId = _update++, ChatId = chat, FromId = chat, PrivateChat = true, MessageId = 77, CallbackId = "cb", CallbackData = data }, default);
    private Task File(long chat, string caption) => Bot.HandleAsync(new BotUpdate { UpdateId = _update++, ChatId = chat, FromId = chat, PrivateChat = true, MessageId = _update, Text = caption, FileId = "F1", FileName = "javob.pdf", MimeType = "application/pdf", FileSize = 14, MediaKind = "document" }, default);

    private void Configure() => Get<MentorBotClient>().SetToken("123456:TEST");

    private async Task LinkAsync(long chat, string phone)
    {
        await Text(chat, "/start");
        await Contact(chat, phone);
    }

    [Fact]
    public async Task New_student_registers_inside_the_bot()
    {
        if (!f.Available) return;
        Configure();
        const long chat = 700001;
        await Text(chat, "/start");
        Assert.Contains("telefon raqamingizni", Tg.LastText(chat));
        await Contact(chat, "+998977001122");
        Assert.Contains("reg:s", Tg.Sent(chat).SelectMany(c => c.Body?["reply_markup"]?["inline_keyboard"]?.AsArray().SelectMany(r => r!.AsArray()).Select(b => b!["callback_data"]!.GetValue<string>()) ?? []));
        await Press(chat, "reg:s");
        await Text(chat, "Jasur Karimov");
        await Text(chat, "10-sinf");
        Assert.Contains("rc:0", Tg.LastButtons(chat));
        await Press(chat, "rc:0");

        await using var db = await DbAsync();
        var user = await db.Users.Include(u => u.Student).FirstAsync(u => u.Phone == "+998977001122");
        Assert.Equal(UserRole.Student, user.Role);
        Assert.Equal(StudentStatus.Applicant, user.Student!.Status);
        Assert.Contains("SAT Math", user.Student.Notes);
        Assert.Equal(user.Id, (await db.MentorBotChats.FindAsync(chat))!.UserId);
        Assert.Contains("Ro'yxatdan o'tdingiz", string.Join("\n", Tg.Sent(chat).Select(c => c.Body?["text"]?.GetValue<string>())));
        // no site links in the mentor bot
        Assert.DoesNotContain(Tg.Sent(chat), c => (c.Body?["text"]?.GetValue<string>() ?? "").Contains("/auth/token"));
    }

    [Fact]
    public async Task Admin_runs_attendance_homework_review_and_broadcast_from_the_bot()
    {
        if (!f.Available) return;
        Configure();
        const long admin = 700100, student = 700101;
        await LinkAsync(admin, "+998900000000");
        Assert.Contains("Mentor paneli", string.Join("\n", Tg.Sent(admin).Select(c => c.Body?["text"]?.GetValue<string>())));
        await LinkAsync(student, "+998902000000");

        await using var db0 = await DbAsync();
        var gid = await db0.Groups.Where(g => g.Name == "SAT Intensive A").Select(g => g.Id).FirstAsync();
        var sid = await db0.Students.Where(s => s.User.Phone == "+998902000000").Select(s => s.Id).FirstAsync();
        await db0.Attendances.Where(a => a.GroupId == gid && a.Date == Tz.Today).ExecuteDeleteAsync();

        // ---- attendance: student 0 absent, everyone else present
        await Text(admin, "✅ Davomat");
        Assert.Contains($"att:{gid}", Tg.LastButtons(admin));
        await Press(admin, $"att:{gid}");
        await Press(admin, $"ac:{gid}:{sid}"); // ⬜ → ✅
        await Press(admin, $"ac:{gid}:{sid}"); // ✅ → ❌
        await Press(admin, $"asv:{gid}");
        await using (var db = await DbAsync())
        {
            var rows = await db.Attendances.Where(a => a.GroupId == gid && a.Date == Tz.Today).ToListAsync();
            Assert.Equal(6, rows.Count);
            Assert.Equal(AttendanceStatus.Absent, rows.Single(r => r.StudentId == sid).Status);
            Assert.Equal(5, rows.Count(r => r.Status == AttendanceStatus.Present));
        }

        // ---- homework
        Tg.Clear();
        await Press(admin, $"hwn:{gid}");
        await Text(admin, "Bot vazifasi 1");
        await Text(admin, "1-20 misollar");
        await Text(admin, "10");
        await Press(admin, "hd:3");
        int hwId;
        await using (var db = await DbAsync())
        {
            var hw = await db.Homeworks.OrderByDescending(h => h.Id).FirstAsync();
            Assert.Equal("Bot vazifasi 1", hw.Title);
            Assert.Equal(HomeworkKind.File, hw.Kind);
            Assert.Contains("Maksimal ball: 10", hw.Description);
            Assert.Equal(6, await db.HomeworkSubmissions.CountAsync(s => s.HomeworkId == hw.Id));
            hwId = hw.Id;
        }

        // ---- student submits a file
        await Text(student, "📝 Vazifalar");
        Assert.Contains($"sh:{hwId}", Tg.LastButtons(student));
        await Press(student, $"sh:{hwId}");
        await Press(student, $"sb:{hwId}");
        await File(student, "Javobim ilova qilindi");
        int subId;
        await using (var db = await DbAsync())
        {
            var sub = await db.HomeworkSubmissions.FirstAsync(s => s.HomeworkId == hwId && s.StudentId == sid);
            Assert.Equal(SubmissionStatus.Submitted, sub.Status);
            Assert.NotNull(sub.FileId);
            Assert.Equal("Javobim ilova qilindi", sub.TextAnswer);
            subId = sub.Id;
        }

        // ---- admin reviews
        await Text(admin, "📥 Topshirilganlar");
        Assert.Contains($"sub:{subId}", Tg.LastButtons(admin));
        await Press(admin, $"sub:{subId}");
        Assert.Contains(Tg.Calls, c => c.Method == "sendDocument");
        await Press(admin, $"rv:{subId}");
        await Text(admin, "8");
        await Text(admin, "Yaxshi, 7-misolni qayta ko'ring");
        await using (var db = await DbAsync())
        {
            var sub = await db.HomeworkSubmissions.FirstAsync(s => s.Id == subId);
            Assert.Equal(SubmissionStatus.Reviewed, sub.Status);
            Assert.Equal(8, sub.Score);
            Assert.Equal(10, sub.MaxScore);
        }

        // ---- broadcast to the group reaches the student's chat
        Tg.Clear();
        await Press(admin, $"btg:{gid}");
        await Text(admin, "Ertangi dars 19:00 da");
        await Press(admin, "bs:now");
        Assert.Contains(Tg.Sent(student), c => (c.Body?["text"]?.GetValue<string>() ?? "").Contains("Ertangi dars 19:00 da"));

        // ---- scheduled broadcast goes out through the jobs
        Tg.Clear();
        await Press(admin, "bt:0");
        await Text(admin, "Rejadagi elon");
        await Press(admin, "bs:later");
        await Text(admin, Tz.ToLocal(DateTime.UtcNow.AddMinutes(2)).ToString("dd.MM.yyyy HH:mm"));
        await using (var db = await DbAsync())
        {
            var b = await db.MentorBroadcasts.OrderByDescending(x => x.Id).FirstAsync();
            Assert.Null(b.SentAt);
            b.ScheduledAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        await Get<MentorBotJobs>().RunAsync(default);
        Assert.Contains(Tg.Sent(student), c => (c.Body?["text"]?.GetValue<string>() ?? "").Contains("Rejadagi elon"));

        // ---- report
        Tg.Clear();
        await Press(admin, $"rep:{gid}");
        Assert.Contains(Tg.Calls, c => c.Method == "sendDocument" && c.Raw.Contains("hisobot"));
    }

    [Fact]
    public async Task Student_question_is_answered_by_the_mentor_and_quiz_is_saved()
    {
        if (!f.Available) return;
        Configure();
        const long admin = 700200, student = 700201;
        await LinkAsync(admin, "+998900000000");
        await LinkAsync(student, "+998902000001");

        await Text(student, "❓ Mentorga savol");
        await Text(student, "3x+5=20 ni qanday yechaman?");
        int qid;
        await using (var db = await DbAsync())
            qid = (await db.MentorQuestions.OrderByDescending(q => q.Id).FirstAsync()).Id;
        Assert.Contains(Tg.Sent(admin), c => (c.Body?["text"]?.GetValue<string>() ?? "").Contains("3x+5=20"));

        await Press(admin, $"q:{qid}");
        Assert.Contains(Tg.Calls, c => c.Method == "copyMessage");
        await Press(admin, $"qa:{qid}");
        await Text(admin, "5 ni ayiring, keyin 3 ga bo'ling: x=5");
        await using (var db = await DbAsync()) Assert.NotNull((await db.MentorQuestions.FindAsync(qid))!.AnsweredAt);
        Assert.Contains("x=5", Tg.LastText(student));

        // mini test: answer every question with the first button
        await Press(student, "qs:x");
        for (var i = 0; i < 5; i++)
        {
            var btn = Tg.LastButtons(student).FirstOrDefault(b => b.StartsWith("qz:"));
            if (btn is null) break;
            await Press(student, btn);
        }
        await using (var db = await DbAsync())
        {
            var sid = await db.Students.Where(s => s.User.Phone == "+998902000001").Select(s => s.Id).FirstAsync();
            var plain = await db.Questions.CountAsync(q => q.Status == QuestionStatus.Active && q.Type == QuestionType.MultipleChoice && !q.Text.Contains("\\("));
            if (plain > 0) Assert.True(await db.MentorQuizResults.AnyAsync(r => r.StudentId == sid));
        }

        // results / payment / schedule screens render
        await Text(student, "/natija"); Assert.Contains("natijalar", Tg.LastText(student));
        await Text(student, "/tolov"); Assert.Contains("To'lov holati", Tg.LastText(student));
        await Text(student, "/jadval"); Assert.Contains("SAT Intensive A", Tg.LastText(student));
    }

    [Fact]
    public async Task Configured_admin_phone_becomes_admin_on_contact()
    {
        if (!f.Available) return;
        Configure();
        const long chat = 700401;
        await LinkAsync(chat, "+998977009999");
        await using var db = await DbAsync();
        var user = await db.Users.FirstAsync(u => u.Phone == "+998977009999");
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal(user.Id, (await db.MentorBotChats.FindAsync(chat))!.UserId);
        Assert.Contains("Mentor paneli", string.Join("\n", Tg.Sent(chat).Select(c => c.Body?["text"]?.GetValue<string>())));
        // an ordinary unknown phone does not
        await LinkAsync(700402, "+998977008888");
        Assert.False(await db.Users.AnyAsync(u => u.Phone == "+998977008888"));
    }

    [Fact]
    public async Task Bot_role_is_separate_from_site_role_and_bot_only_accounts_cannot_use_the_site()
    {
        if (!f.Available) return;
        Configure();
        var admin = Get<MentorBotAdmin>();
        await using var db = await DbAsync();
        var teacher = await db.Users.FirstAsync(u => u.Phone == "+998901111112");
        Assert.Null(await admin.SetBotRoleAsync(teacher.Id, UserRole.Admin, f.AdminId, true));

        const long chat = 700501;
        await LinkAsync(chat, "+998901111112");
        var all = string.Join("\n", Tg.Sent(chat).Select(c => c.Body?["text"]?.GetValue<string>()));
        Assert.Contains("Qarzdorlar", all);                 // admin-only item in the bot
        await using (var db2 = await DbAsync())
            Assert.Equal(UserRole.Teacher, (await db2.Users.FindAsync(teacher.Id))!.Role); // site role untouched

        // back to the site role
        Assert.Null(await admin.SetBotRoleAsync(teacher.Id, null, f.AdminId, true));
        Tg.Clear();
        await Text(chat, "/start");
        Assert.DoesNotContain("Qarzdorlar", Tg.LastText(chat));

        // a new bot-only admin
        Assert.Null(await admin.AddBotStaffAsync("Bot Admin", "+998977005555", UserRole.Admin, f.AdminId, true));
        var botOnly = await db.Users.FirstAsync(u => u.Phone == "+998977005555");
        Assert.False(botOnly.CanUseSite);
        Assert.Equal(UserRole.Admin, botOnly.BotRole);
        var auth = Get<AuthService>();
        var token = await auth.IssueLoginTokenAsync(botOnly.Id);
        Assert.Null((await auth.ConsumeLoginTokenAsync(token)).User);
        await LinkAsync(700502, "+998977005555");
        Assert.Contains("Mentor paneli", string.Join("\n", Tg.Sent(700502).Select(c => c.Body?["text"]?.GetValue<string>())));

        // site access can be reopened; an admin cannot lock themself out
        Assert.Null(await admin.SetSiteAccessAsync(botOnly.Id, true, f.AdminId, true));
        Assert.NotNull((await auth.ConsumeLoginTokenAsync(await auth.IssueLoginTokenAsync(botOnly.Id))).User);
        Assert.NotNull(await admin.SetSiteAccessAsync(f.AdminId, false, f.AdminId, true));
        // students in a group cannot become bot staff
        var studentUser = await db.Users.FirstAsync(u => u.Phone == "+998902000003");
        Assert.NotNull(await admin.SetBotRoleAsync(studentUser.Id, UserRole.Admin, f.AdminId, true));
    }

    [Fact]
    public async Task Site_admin_panel_answers_questions_confirms_receipts_and_broadcasts_via_the_bot()
    {
        if (!f.Available) return;
        Configure();
        var admin = Get<MentorBotAdmin>();
        const long student = 700601;
        await LinkAsync(student, "+998902000004");

        // question → answered from the site
        await Text(student, "/savol");
        await Text(student, "Logarifm qoidalari?");
        await using var db = await DbAsync();
        var q = await db.MentorQuestions.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Contains((await admin.QuestionsAsync(openOnly: true)), x => x.Id == q.Id);
        Assert.Null(await admin.AnswerAsync(q.Id, "log(ab)=log a+log b", f.AdminId));
        Assert.Contains("log(ab)", Tg.LastText(student));
        Assert.NotNull(await admin.AnswerAsync(q.Id, "yana", f.AdminId)); // already answered

        // receipt → payment from the site
        await Text(student, "/tolov");
        await Press(student, "rcp");
        await File(student, "chek");
        var r = await db.MentorQuestions.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(MentorQuestionKind.Receipt, r.Kind);
        Assert.Equal("F1", r.TelegramFileId);
        var sid = await db.Students.Where(s => s.User.Phone == "+998902000004").Select(s => s.Id).FirstAsync();
        var before = await db.Payments.CountAsync(p => p.StudentId == sid);
        Assert.Null(await admin.AcceptReceiptAsync(r.Id, 450000, f.AdminId));
        Assert.Equal(before + 1, await db.Payments.CountAsync(p => p.StudentId == sid));
        Assert.Contains("qabul qilindi", Tg.LastText(student));
        Assert.NotNull(await admin.QuestionFileAsync(r.Id));

        // broadcast from the site
        var gid = await db.Enrollments.Where(e => e.StudentId == sid && e.Status == EnrollmentStatus.Active).Select(e => e.GroupId).FirstAsync();
        var (sent, err) = await admin.BroadcastAsync(BroadcastTarget.Group, gid, "Saytdan e lon", null, f.AdminId);
        Assert.Null(err);
        Assert.True(sent >= 1);
        Assert.Contains("Saytdan e lon", Tg.LastText(student));
        Assert.NotNull((await admin.BroadcastAsync(BroadcastTarget.Group, null, "x", null, f.AdminId)).Error);
    }

    [Fact]
    public async Task Teacher_cannot_touch_other_groups_and_students_cannot_use_admin_actions()
    {
        if (!f.Available) return;
        Configure();
        const long teacher2 = 700301, student = 700302;
        await LinkAsync(teacher2, "+998901111112");
        await LinkAsync(student, "+998902000002");
        await using var db = await DbAsync();
        var gA = await db.Groups.Where(g => g.Name == "SAT Intensive A").Select(g => g.Id).FirstAsync();
        var before = await db.Attendances.CountAsync();

        await Press(teacher2, $"att:{gA}");      // not their group → nothing
        await Press(teacher2, $"asv:{gA}");
        await Press(student, $"att:{gA}");       // student → refused
        await Press(student, $"asv:{gA}");
        await Text(student, "/admin");
        Assert.Contains("faqat xodimlar", Tg.LastText(student));
        Assert.Equal(before, await db.Attendances.CountAsync());
        Assert.DoesNotContain($"att:{gA}", Tg.Sent(teacher2).SelectMany(c => c.Body?["reply_markup"]?["inline_keyboard"]?.AsArray().SelectMany(r => r!.AsArray()).Select(b => b!["callback_data"]!.GetValue<string>()) ?? []));
    }
}
