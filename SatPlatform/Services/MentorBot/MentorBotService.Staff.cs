using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

public partial class MentorBotService
{
    private Task<List<int>> GroupScopeAsync(Ctx c) => academy.GroupScopeAsync(c.User!.Id, c.User.Role);
    private Task<List<int>> StudentScopeAsync(Ctx c) => academy.StudentScopeAsync(c.User!.Id, c.User.Role);
    private async Task<bool> CanGroupAsync(Ctx c, int gid) => (await GroupScopeAsync(c)).Contains(gid);
    private async Task<bool> CanStudentAsync(Ctx c, int sid) => (await StudentScopeAsync(c)).Contains(sid);

    private async Task StaffCallbackAsync(Ctx c, string[] p)
    {
        int I(int i) => p.Length > i && int.TryParse(p[i], out var v) ? v : 0;
        var admin = IsAdmin(c.User);
        switch (p[0])
        {
            case "g": await GroupCardAsync(c, I(1)); return;
            case "gnew" when admin: await c.StepAsync("s_gn_name", true); await Say(c, "➕ Yangi guruh nomini yozing (masalan: <i>SAT Math 14:00</i>):"); return;
            case "gc" when c.Chat.Step == "s_gn_course": c.Set("course", Courses.ElementAtOrDefault(I(1)) ?? "SAT"); await GroupDaysAskAsync(c); return;
            case "gd" when c.Chat.Step == "s_gn_days": c.Set("days", p.ElementAtOrDefault(1) ?? ""); await c.StepAsync("s_gn_time"); await Show(c, "⏰ Dars vaqtini yozing: <i>14:00-15:30</i>"); return;
            case "att": await AttendanceStartAsync(c, I(1)); return;
            case "ac": await AttendanceToggleAsync(c, I(1), I(2)); return;
            case "aa": await AttendanceAllAsync(c, I(1)); return;
            case "asv": await AttendanceSaveAsync(c, I(1)); return;
            case "hwn": await HomeworkNewAsync(c, I(1)); return;
            case "hd" when c.Chat.Step == "s_hw_deadline": await HomeworkCreateAsync(c, Tz.ToUtc(Tz.Today.AddDays(I(1)).ToDateTime(new TimeOnly(23, 59)))); return;
            case "sub": await SubmissionDetailAsync(c, I(1)); return;
            case "rv": await ReviewStartAsync(c, I(1)); return;
            case "bt": await BroadcastTargetAsync(c, (BroadcastTarget)I(1), null); return;
            case "btg": await BroadcastTargetAsync(c, BroadcastTarget.Group, I(1)); return;
            case "btp": await BroadcastTargetAsync(c, BroadcastTarget.Parents, I(1)); return;
            case "bs": await BroadcastSendAsync(c, p.ElementAtOrDefault(1) == "later"); return;
            case "st": await StudentCardAsync(c, I(1)); return;
            case "dm": await DirectStartAsync(c, I(1)); return;
            case "pay" when admin: await PayStartAsync(c, I(1), null); return;
            case "en" when admin: await PickGroupAsync(c, I(1), "en2", "➕ Qaysi guruhga qo'shamiz?"); return;
            case "mv" when admin: await PickGroupAsync(c, I(1), "mv2", "🔀 Qaysi guruhga ko'chiramiz? (joriy guruhlardan chiqariladi)"); return;
            case "ue" when admin: await PickCurrentGroupAsync(c, I(1)); return;
            case "en2" when admin: await EnrollAsync(c, I(1), I(2), move: false); return;
            case "mv2" when admin: await EnrollAsync(c, I(1), I(2), move: true); return;
            case "ue2" when admin: await academy.UnenrollAsync(I(1), I(2), c.User!.Id); await Show(c, "➖ Guruhdan chiqarildi."); return;
            case "q": await QuestionDetailAsync(c, I(1)); return;
            case "qa": await AnswerStartAsync(c, I(1)); return;
            case "qc": await CloseQuestionAsync(c, I(1), "Yopildi", null); return;
            case "rcok" when admin: await ReceiptAcceptAsync(c, I(1)); return;
            case "rcno" when admin: await CloseQuestionAsync(c, I(1), "Rad etildi", "❌ Yuborgan chekingiz tasdiqlanmadi. Iltimos, mentor bilan bog'laning."); return;
            case "debt" when admin: await DebtReminderAsync(c, p.ElementAtOrDefault(1) == "all" ? null : I(1)); return;
            case "rep": await ReportAsync(c, I(1)); return;
            case "gst": await GroupStudentsAsync(c, I(1)); return;
        }
        await Say(c, "⛔ Bu amalga ruxsat yo'q.");
    }

    private async Task<bool> StaffStepAsync(Ctx c)
    {
        var t = c.Text;
        switch (c.Chat.Step)
        {
            case "s_gn_name":
                if (t.Length is < 2 or > 120) { await Say(c, "Nom 2–120 belgi bo'lsin."); return true; }
                c.Set("name", t);
                await c.StepAsync("s_gn_course");
                await Say(c, "📘 Kurs:", Column(Courses.Select((x, i) => new B(x, $"gc:{i}"))));
                return true;
            case "s_gn_days":
                var days = string.Join(',', t.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries).Where(d => d.Length == 1 && d[0] is >= '1' and <= '7').Distinct());
                if (days.Length == 0) { await Say(c, "Kun raqamlarini yozing: 1=Du … 7=Ya, masalan <i>1,3,5</i>"); return true; }
                c.Set("days", days);
                await c.StepAsync("s_gn_time");
                await Say(c, "⏰ Dars vaqtini yozing: <i>14:00-15:30</i>");
                return true;
            case "s_gn_time": await GroupCreateAsync(c); return true;
            case "s_hw_title":
                if (t.Length is < 2 or > 160) { await Say(c, "Sarlavha 2–160 belgi bo'lsin."); return true; }
                c.Set("title", t);
                await c.StepAsync("s_hw_desc");
                await Say(c, "📄 Vazifa matnini yozing yoki <b>fayl/rasm</b> yuboring (izoh bilan bo'lishi mumkin). O'tkazib yuborish: <code>-</code>");
                return true;
            case "s_hw_desc":
                if (c.U.FileId is not null)
                {
                    var (fid, err) = await SaveTelegramFileAsync(c);
                    if (err is not null) { await Say(c, "❌ " + H(err)); return true; }
                    c.Set("file", fid?.ToString());
                }
                if (t.Length > 0 && t != "-") c.Set("desc", t.Length > 2000 ? t[..2000] : t);
                await c.StepAsync("s_hw_max");
                await Say(c, "🔢 Maksimal ball? (masalan <i>10</i>; kerak bo'lmasa <code>-</code>)");
                return true;
            case "s_hw_max":
                if (t != "-" && !(int.TryParse(t, out var mx) && mx is > 0 and <= 1000)) { await Say(c, "Son yozing (1–1000) yoki -"); return true; }
                c.Set("max", t == "-" ? null : t);
                await c.StepAsync("s_hw_deadline");
                await Say(c, "⏰ Muddat? Tanlang yoki yozing: <i>25.11 18:00</i>",
                    Inline([new B("Ertaga", "hd:1"), new B("3 kun", "hd:3"), new B("1 hafta", "hd:7")]));
                return true;
            case "s_hw_deadline":
                if (ParseLocal(t) is not DateTime dl) { await Say(c, "Format: <i>25.11 18:00</i> yoki <i>25.11.2026</i>"); return true; }
                await HomeworkCreateAsync(c, dl);
                return true;
            case "s_rv_score":
                int? score = null, max = null;
                if (t != "-")
                {
                    var parts = t.Split('/', StringSplitOptions.TrimEntries);
                    if (!int.TryParse(parts[0], out var sc) || sc < 0 || (parts.Length > 1 && !(int.TryParse(parts[1], out var m2) && m2 > 0 && (max = m2) is not null)))
                    { await Say(c, "Format: <i>8</i> yoki <i>8/10</i> (bahosiz: -)"); return true; }
                    score = sc;
                    max ??= c.GetInt("max") is > 0 and var dm ? dm : null;
                }
                c.Set("score", score?.ToString()); c.Set("smax", max?.ToString());
                await c.StepAsync("s_rv_comment");
                await Say(c, "💬 Izoh yozing (kerak bo'lmasa <code>-</code>):");
                return true;
            case "s_rv_comment":
                var subId = c.GetInt("sub");
                var e = await homework.ReviewAsync(subId, int.TryParse(c.Get("score"), out var s1) ? s1 : null, int.TryParse(c.Get("smax"), out var s2) ? s2 : null, t == "-" ? null : t, c.User!.Id);
                await c.ResetAsync();
                await Say(c, e is null ? "✅ Baholandi. O'quvchiga xabar yuborildi." : "❌ " + H(e), MenuFor(c.User));
                if (e is null) await SubmissionsAsync(c);
                return true;
            case "s_bc_text":
                if (t.Length == 0) { await Say(c, "Faqat matnli xabar yuboriladi. Matn yozing:"); return true; }
                c.Set("text", t.Length > 3500 ? t[..3500] : t);
                await c.StepAsync("s_bc_confirm");
                await Say(c, $"👀 <b>Ko'rinishi</b> ({H(c.Get("label"))}):\n\n📣 {H(c.Get("text"))}",
                    Inline([new B("📤 Hozir yuborish", "bs:now"), new B("⏰ Rejalashtirish", "bs:later")], [new B("❌ Bekor", "cancel")]));
                return true;
            case "s_bc_time":
                if (ParseLocal(t) is not DateTime at || at <= DateTime.UtcNow) { await Say(c, "Kelajakdagi vaqtni yozing: <i>25.11 09:00</i>"); return true; }
                await BroadcastSaveAsync(c, at);
                return true;
            case "s_dm_text":
                if (t.Length == 0) return false;
                c.Set("text", t);
                await BroadcastSaveAsync(c, null);
                return true;
            case "s_qa_text":
                if (t.Length == 0) return false;
                await AnswerSendAsync(c, t);
                return true;
            case "s_pay_amount":
                var raw = t.Replace(" ", "").Replace(",", "").Replace("so'm", "");
                if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0) { await Say(c, "Summani son bilan yozing, masalan <i>500000</i>"); return true; }
                await PaySaveAsync(c, amount);
                return true;
        }
        return false;
    }

    private static DateTime? ParseLocal(string s)
    {
        string[] formats = ["dd.MM HH:mm", "d.MM HH:mm", "dd.MM.yyyy HH:mm", "d.M.yyyy HH:mm", "dd.MM.yyyy", "dd.MM", "d.MM", "d.M"];
        if (!DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return null;
        if (!s.Contains(':')) d = d.Date.AddHours(23).AddMinutes(59);
        if (!s.Contains(d.Year.ToString()) && d < Tz.ToLocal(DateTime.UtcNow).AddDays(-1)) d = d.AddYears(1);
        return Tz.ToUtc(d);
    }

    // ---------------------------------------------------------------- groups

    private async Task StaffGroupsAsync(Ctx c, string action)
    {
        var ids = await GroupScopeAsync(c);
        var groups = await academy.GroupsAsync(ids);
        var title = action switch { "att" => "✅ Davomat — guruhni tanlang", "hwn" => "📝 Qaysi guruhga vazifa?", "rep" => "📈 Hisobot — guruhni tanlang", _ => "👥 <b>Guruhlar</b>" };
        var buttons = groups.OrderBy(g => g.Name).Select(g => new B($"{g.Name} · {Time(g)}", $"{action}:{g.Id}")).ToList();
        if (action == "g" && IsAdmin(c.User)) buttons.Add(new B("➕ Yangi guruh", "gnew"));
        if (buttons.Count == 0) { await Say(c, "Sizga biriktirilgan guruh yo'q."); return; }
        await Say(c, title, Column(buttons));
    }

    private async Task GroupCardAsync(Ctx c, int gid)
    {
        if (!await CanGroupAsync(c, gid)) return;
        var g = await c.Db.Groups.AsNoTracking().Include(x => x.TeacherUser).Include(x => x.AssistantUser).FirstAsync(x => x.Id == gid, c.Ct);
        var count = await c.Db.Enrollments.CountAsync(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active, c.Ct);
        var text = $"👥 <b>{H(g.Name)}</b> ({H(g.CourseType)})\n🗓 {Labels.Days(g.ScheduleDays)} ⏰ {Time(g)}" +
                   (string.IsNullOrWhiteSpace(g.Room) ? "" : $"\n🚪 {H(g.Room)}") +
                   $"\n👨‍🏫 {H(g.TeacherUser?.FullName ?? "—")}" + (g.AssistantUser is null ? "" : $"\n🧑‍💼 {H(g.AssistantUser.FullName)}") +
                   $"\n🎓 O'quvchilar: <b>{count}</b>";
        await Show(c, text, Inline(
            [new B("🎓 O'quvchilar", $"gst:{gid}"), new B("✅ Davomat", $"att:{gid}")],
            [new B("📝 Vazifa", $"hwn:{gid}"), new B("📈 Hisobot", $"rep:{gid}")],
            [new B("📣 Guruhga xabar", $"btg:{gid}"), new B("👨‍👩‍👧 Ota-onalarga", $"btp:{gid}")]));
    }

    private async Task GroupStudentsAsync(Ctx c, int gid)
    {
        if (!await CanGroupAsync(c, gid)) return;
        var list = await c.Db.Enrollments.AsNoTracking().Where(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.StudentId, e.Student.User.FullName }).OrderBy(x => x.FullName).ToListAsync(c.Ct);
        if (list.Count == 0) { await Say(c, "Guruhda o'quvchi yo'q." + (IsAdmin(c.User) ? " «🆕 Yangi o'quvchilar» orqali qo'shing." : "")); return; }
        await Say(c, $"🎓 O'quvchilar ({list.Count}):", Column(list.Select(x => new B(x.FullName, $"st:{x.StudentId}"))));
    }

    private async Task GroupDaysAskAsync(Ctx c)
    {
        await c.StepAsync("s_gn_days");
        await Show(c, "🗓 Dars kunlari? Tanlang yoki yozing (1=Du … 7=Ya), masalan <i>1,3,5</i>",
            Inline([new B("Du-Cho-Ju", "gd:1,3,5"), new B("Se-Pa-Sha", "gd:2,4,6")], [new B("Har kuni (Du–Ju)", "gd:1,2,3,4,5")]));
    }

    private async Task GroupCreateAsync(Ctx c)
    {
        var parts = c.Text.Replace('–', '-').Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TimeOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out var st) || !TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out var en))
        { await Say(c, "Format: <i>14:00-15:30</i>"); return; }
        var g = new Group { Name = c.Get("name")!, CourseType = c.Get("course") ?? "SAT", ScheduleDays = c.Get("days") ?? "", StartTime = st, EndTime = en,
            TeacherUserId = c.User!.Role == UserRole.Teacher ? c.User.Id : null };
        var (id, err) = await academy.SaveGroupAsync(g, c.User.Id);
        await c.ResetAsync();
        if (err is not null) { await Say(c, "❌ " + H(err)); return; }
        await Say(c, "✅ Guruh yaratildi.");
        await GroupCardAsync(c, id);
    }

    // ---------------------------------------------------------------- attendance

    private Dictionary<int, AttendanceStatus> Marks(Ctx c) => (c.Get("am") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Split('=')).Where(x => x.Length == 2).ToDictionary(x => int.Parse(x[0]), x => (AttendanceStatus)int.Parse(x[1]));
    private static string MarksString(Dictionary<int, AttendanceStatus> m) => string.Join(';', m.Select(kv => $"{kv.Key}={(int)kv.Value}"));

    private async Task AttendanceStartAsync(Ctx c, int gid)
    {
        if (!await CanGroupAsync(c, gid)) return;
        var existing = await academy.AttendanceAsync(gid, Tz.Today);
        c.Set("am", MarksString(existing.ToDictionary(a => a.StudentId, a => a.Status)));
        c.Set("gid", gid.ToString());
        await c.StepAsync("s_att");
        await AttendanceRenderAsync(c, gid, edit: false);
    }

    private async Task AttendanceRenderAsync(Ctx c, int gid, bool edit)
    {
        var g = await c.Db.Groups.AsNoTracking().FirstAsync(x => x.Id == gid, c.Ct);
        var list = await c.Db.Enrollments.AsNoTracking().Where(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.StudentId, e.Student.User.FullName }).OrderBy(x => x.FullName).ToListAsync(c.Ct);
        if (list.Count == 0) { await Say(c, "Guruhda o'quvchi yo'q."); return; }
        var marks = Marks(c);
        var warn = AcademyService.HasClassOn(g, Tz.Today) ? "" : "\n⚠️ Bugun bu guruhning dars kuni emas.";
        var text = $"✅ <b>Davomat</b> — {H(g.Name)}\n📅 {Tz.Today:dd.MM.yyyy}{warn}\n\nBosib almashtiring: ✅ keldi → ❌ kelmadi → 🟡 sababli → ⏱ kechikdi.\n⬜ belgilanmaganlar «keldi» deb saqlanadi.";
        var rows = list.Select(x => new[] { new B($"{(marks.TryGetValue(x.StudentId, out var s) ? AttIcon(s) : "⬜")} {x.FullName}", $"ac:{gid}:{x.StudentId}") }).ToList();
        rows.Add([new B("✅ Hammasi keldi", $"aa:{gid}"), new B("💾 Saqlash", $"asv:{gid}")]);
        if (edit) await Show(c, text, Inline(rows)); else await Say(c, text, Inline(rows));
    }

    private async Task AttendanceToggleAsync(Ctx c, int gid, int sid)
    {
        if (c.Chat.Step != "s_att" || c.GetInt("gid") != gid) { await AttendanceStartAsync(c, gid); return; }
        var m = Marks(c);
        m[sid] = !m.TryGetValue(sid, out var cur) ? AttendanceStatus.Present : cur switch
        {
            AttendanceStatus.Present => AttendanceStatus.Absent,
            AttendanceStatus.Absent => AttendanceStatus.Excused,
            AttendanceStatus.Excused => AttendanceStatus.Late,
            _ => AttendanceStatus.Present
        };
        c.Set("am", MarksString(m));
        await c.SaveAsync();
        await AttendanceRenderAsync(c, gid, edit: true);
    }

    private async Task AttendanceAllAsync(Ctx c, int gid)
    {
        if (c.Chat.Step != "s_att" || c.GetInt("gid") != gid) { await AttendanceStartAsync(c, gid); return; }
        var ids = await c.Db.Enrollments.Where(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).ToListAsync(c.Ct);
        c.Set("am", MarksString(ids.ToDictionary(i => i, _ => AttendanceStatus.Present)));
        await c.SaveAsync();
        await AttendanceRenderAsync(c, gid, edit: true);
    }

    private async Task AttendanceSaveAsync(Ctx c, int gid)
    {
        if (!await CanGroupAsync(c, gid)) return;
        if (c.Chat.Step != "s_att" || c.GetInt("gid") != gid) { await AttendanceStartAsync(c, gid); return; }
        var m = Marks(c);
        var ids = await c.Db.Enrollments.Where(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active).Select(e => e.StudentId).ToListAsync(c.Ct);
        var rows = ids.Select(i => (i, m.GetValueOrDefault(i, AttendanceStatus.Present), m.GetValueOrDefault(i) == AttendanceStatus.Late ? 10 : 0)).ToList();
        await academy.SaveAttendanceAsync(gid, Tz.Today, rows, c.User!.Id);
        await c.ResetAsync();
        var p = rows.Count(r => r.Item2 == AttendanceStatus.Present); var a = rows.Count(r => r.Item2 == AttendanceStatus.Absent);
        var e = rows.Count(r => r.Item2 == AttendanceStatus.Excused); var l = rows.Count(r => r.Item2 == AttendanceStatus.Late);
        await Show(c, $"💾 <b>Davomat saqlandi</b> ({Tz.Today:dd.MM})\n✅ {p}  ❌ {a}  🟡 {e}  ⏱ {l}\n\nKelmaganlarning ota-onalariga xabar yuborildi.");
    }

    // ---------------------------------------------------------------- homework

    private async Task HomeworkNewAsync(Ctx c, int gid)
    {
        if (!await CanGroupAsync(c, gid)) return;
        c.Set("gid", gid.ToString());
        await c.StepAsync("s_hw_title", true);
        c.Set("gid", gid.ToString()); await c.SaveAsync();
        await Say(c, "📝 Vazifa sarlavhasini yozing (masalan: <i>Kvadrat tenglamalar — 1-15</i>):");
    }

    private async Task HomeworkCreateAsync(Ctx c, DateTime deadlineUtc)
    {
        var desc = c.Get("desc");
        if (c.Get("max") is string mx) desc = (desc is null ? "" : desc + "\n\n") + $"Maksimal ball: {mx}";
        var h = new Homework { Title = c.Get("title") ?? "Vazifa", Description = desc, Kind = HomeworkKind.File, GroupId = c.GetInt("gid"),
            FileId = int.TryParse(c.Get("file"), out var f) ? f : null, Deadline = deadlineUtc };
        var (id, err) = await homework.CreateAsync(h, c.User!.Id);
        await c.ResetAsync();
        if (err is not null) { await Say(c, "❌ " + H(err), MenuFor(c.User)); return; }
        await Show(c, $"✅ Vazifa berildi: <b>{H(h.Title)}</b>\n⏰ {Tz.Fmt(deadlineUtc)}\nO'quvchilarga xabar ketdi; muddatdan 1 kun oldin eslatma yuboriladi.");
    }

    private async Task SubmissionsAsync(Ctx c)
    {
        var scope = await StudentScopeAsync(c);
        var list = await c.Db.HomeworkSubmissions.AsNoTracking().Where(s => s.Status == SubmissionStatus.Submitted && scope.Contains(s.StudentId))
            .OrderBy(s => s.SubmittedAt).Take(25).Select(s => new { s.Id, s.Homework.Title, s.Student.User.FullName }).ToListAsync(c.Ct);
        if (list.Count == 0) { await Say(c, "📥 Tekshirilmagan topshiriqlar yo'q. 🎉"); return; }
        await Say(c, $"📥 <b>Tekshirish kutilmoqda</b> ({list.Count})", Column(list.Select(x => new B($"{Cut(x.FullName, 22)} · {Cut(x.Title, 22)}", $"sub:{x.Id}"))));
    }

    private async Task SubmissionDetailAsync(Ctx c, int id)
    {
        var s = await c.Db.HomeworkSubmissions.AsNoTracking().Include(x => x.Homework).Include(x => x.Student).ThenInclude(x => x.User).FirstOrDefaultAsync(x => x.Id == id, c.Ct);
        if (s is null || !await CanStudentAsync(c, s.StudentId)) return;
        var text = $"📥 <b>{H(s.Homework.Title)}</b>\n👤 {H(s.Student.User.FullName)}\n🕒 {Tz.Fmt(s.SubmittedAt)}" + (s.SubmittedAt > s.Homework.Deadline ? " ⏰ kechikib" : "") +
                   $"\nHolat: {Labels.Submission(s.Status, s.Homework.Deadline)}" +
                   (s.TextAnswer is null ? "" : $"\n\n✍️ {H(s.TextAnswer)}") + (s.Status == SubmissionStatus.Reviewed ? $"\n\nBaho: {Score(s)}  💬 {H(s.TeacherComment)}" : "");
        await Say(c, text, Inline([new B("✍️ Baholash", $"rv:{id}")]));
        if (s.FileId is int fid) await SendMediaFileAsync(c, fid, $"📎 {H(s.Student.User.FullName)} javobi");
    }

    private async Task ReviewStartAsync(Ctx c, int id)
    {
        var s = await c.Db.HomeworkSubmissions.AsNoTracking().Include(x => x.Homework).FirstOrDefaultAsync(x => x.Id == id, c.Ct);
        if (s is null || !await CanStudentAsync(c, s.StudentId)) return;
        var max = System.Text.RegularExpressions.Regex.Match(s.Homework.Description ?? "", @"Maksimal ball: (\d+)");
        await c.StepAsync("s_rv_score", true);
        c.Set("sub", id.ToString()); if (max.Success) c.Set("max", max.Groups[1].Value); await c.SaveAsync();
        await Say(c, "🔢 Baho yozing: <i>8</i> yoki <i>8/10</i>" + (max.Success ? $" (maksimal {max.Groups[1].Value})" : "") + ". Bahosiz: <code>-</code>");
    }

    // ---------------------------------------------------------------- broadcast / direct message

    private async Task BroadcastStartAsync(Ctx c)
    {
        if (IsAdmin(c.User))
            await Say(c, "📣 Kimga yuboramiz?", Inline(
                [new B("🎓 Barcha o'quvchilar", "bt:0"), new B("👨‍👩‍👧 Barcha ota-onalar", "bt:1")],
                [new B("👥 Bitta guruh", "bt:2"), new B("🌐 Hammaga", "bt:3")]));
        else await StaffGroupsAsync(c, "btg");
    }

    private async Task BroadcastTargetAsync(Ctx c, BroadcastTarget target, int? gid)
    {
        if (target == BroadcastTarget.Group && gid is null or 0) { await StaffGroupsAsync(c, "btg"); return; }
        if (gid is int g && g > 0 && !await CanGroupAsync(c, g)) return;
        if (gid is null or 0 && !IsAdmin(c.User)) return;
        var gname = gid is int g2 && g2 > 0 ? await c.Db.Groups.Where(x => x.Id == g2).Select(x => x.Name).FirstAsync(c.Ct) : null;
        var label = target switch
        {
            BroadcastTarget.AllStudents => "barcha o'quvchilar",
            BroadcastTarget.Parents => gname is null ? "barcha ota-onalar" : $"{gname} — ota-onalar",
            BroadcastTarget.Group => $"{gname} guruhi",
            _ => "hamma foydalanuvchilar"
        };
        await c.StepAsync("s_bc_text", true);
        c.Set("target", ((int)target).ToString()); c.Set("gid", gid?.ToString()); c.Set("label", label); await c.SaveAsync();
        await Show(c, $"📣 Qabul qiluvchilar: <b>{H(label)}</b>\n\nXabar matnini yozing (dars e'loni, Zoom/Meet havolasi, dars bekor qilinishi va h.k.):");
    }

    private async Task BroadcastSendAsync(Ctx c, bool later)
    {
        if (c.Chat.Step != "s_bc_confirm") return;
        if (later) { await c.StepAsync("s_bc_time"); await Show(c, "⏰ Qachon yuborilsin? <i>25.11 09:00</i>"); return; }
        await BroadcastSaveAsync(c, null);
    }

    private async Task BroadcastSaveAsync(Ctx c, DateTime? at)
    {
        var target = (BroadcastTarget)c.GetInt("target");
        var b = new MentorBroadcast { Target = target, GroupId = c.GetInt("gid") is > 0 and var g ? g : null, StudentId = c.GetInt("sid") is > 0 and var s ? s : null,
            Text = c.Get("text") ?? "", ScheduledAt = at, CreatedByUserId = c.User!.Id };
        c.Db.MentorBroadcasts.Add(b);
        await c.ResetAsync();
        await audit.LogAsync(c.User.Id, "MentorBroadcast", "MentorBroadcast", b.Id, $"{target} {b.GroupId}");
        if (at is not null) { await Say(c, $"⏰ Rejalashtirildi: {Tz.Fmt(at)}", MenuFor(c.User)); return; }
        var n = await sender.BroadcastAsync(b.Id, c.Ct);
        await Say(c, $"✅ Yuborildi: {n} ta foydalanuvchiga.", MenuFor(c.User));
    }

    private async Task DirectStartAsync(Ctx c, int sid)
    {
        if (!await CanStudentAsync(c, sid)) return;
        await c.StepAsync("s_dm_text", true);
        c.Set("target", ((int)BroadcastTarget.Student).ToString()); c.Set("sid", sid.ToString()); await c.SaveAsync();
        await Say(c, "✉️ O'quvchiga xabar matnini yozing (ota-onasiga ham nusxa ketadi):");
    }

    // ---------------------------------------------------------------- students

    private async Task StudentCardAsync(Ctx c, int sid)
    {
        if (!await CanStudentAsync(c, sid) && !(IsAdmin(c.User) && await c.Db.Students.AnyAsync(s => s.Id == sid, c.Ct))) return;
        var st = await c.Db.Students.AsNoTracking().Include(s => s.User).Include(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)).ThenInclude(e => e.Group)
            .FirstAsync(s => s.Id == sid, c.Ct);
        var bal = await c.Db.Payments.Where(p => p.StudentId == sid).SumAsync(p => (decimal?)p.Amount, c.Ct) ?? 0;
        var parents = await c.Db.ParentStudents.Where(p => p.StudentId == sid).Select(p => p.ParentUser.FullName + " " + p.ParentUser.Phone).ToListAsync(c.Ct);
        var inBot = await c.Db.MentorBotChats.AnyAsync(x => x.UserId == st.UserId, c.Ct);
        var head = $"👤 <b>{H(st.User.FullName)}</b>\n📱 {PhoneFormat.Display(st.User.Phone)} {(inBot ? "· 🤖 botda" : "· botda emas")}\nHolat: {Labels.StudentStatus(st.Status)}" +
                   $"\n👥 {(st.Enrollments.Count == 0 ? "guruhsiz" : H(string.Join(", ", st.Enrollments.Select(e => e.Group.Name))))}" +
                   $"\n💳 Balans: <b>{Labels.Money(bal)}</b>" + (st.NextPaymentDate is DateOnly d ? $" · keyingi to'lov {d:dd.MM}" : "") +
                   $"\n👨‍👩‍👧 Ota-ona: {(parents.Count == 0 ? $"yo'q (kod: <code>{st.ParentCode}</code>)" : H(string.Join(", ", parents)))}" +
                   (string.IsNullOrWhiteSpace(st.Notes) ? "" : $"\n📝 {H(st.Notes)}");
        var report = await StudentReportAsync(c.Db, sid, c.Ct);
        var rows = new List<B[]> { new[] { new B("✉️ Xabar", $"dm:{sid}") } };
        if (IsAdmin(c.User))
        {
            rows[0] = [new B("✉️ Xabar", $"dm:{sid}"), new B("💵 To'lov kiritish", $"pay:{sid}")];
            rows.Add([new B("➕ Guruhga qo'shish", $"en:{sid}"), new B("🔀 Ko'chirish", $"mv:{sid}")]);
            rows.Add([new B("➖ Guruhdan chiqarish", $"ue:{sid}"), new B("🔔 To'lov eslatmasi", $"debt:{sid}")]);
        }
        await Say(c, head + "\n\n" + report.Split('\n', 2).ElementAtOrDefault(1), Inline(rows));
    }

    private async Task NewStudentsAsync(Ctx c)
    {
        if (!IsAdmin(c.User)) { await Say(c, "⛔ Faqat adminlar uchun."); return; }
        var list = await c.Db.Students.AsNoTracking().Where(s => (s.Status == StudentStatus.Applicant || s.Status == StudentStatus.Active) && s.User.IsActive &&
                !s.Enrollments.Any(e => e.Status == EnrollmentStatus.Active))
            .OrderByDescending(s => s.CreatedAt).Take(25).Select(s => new { s.Id, s.User.FullName, s.CreatedAt }).ToListAsync(c.Ct);
        if (list.Count == 0) { await Say(c, "🆕 Guruhsiz o'quvchilar yo'q."); return; }
        await Say(c, $"🆕 <b>Guruhga qo'shilmagan o'quvchilar</b> ({list.Count})\nBosing → «Guruhga qo'shish».",
            Column(list.Select(x => new B($"{x.FullName} · {Tz.Fmt(x.CreatedAt, "dd.MM")}", $"st:{x.Id}"))));
    }

    private async Task PickGroupAsync(Ctx c, int sid, string action, string title)
    {
        var groups = await academy.GroupsAsync();
        await Show(c, title, Column(groups.OrderBy(g => g.Name).Select(g => new B($"{g.Name} · {Time(g)}", $"{action}:{sid}:{g.Id}"))));
    }

    private async Task PickCurrentGroupAsync(Ctx c, int sid)
    {
        var groups = await c.Db.Enrollments.Where(e => e.StudentId == sid && e.Status == EnrollmentStatus.Active).Select(e => new { e.GroupId, e.Group.Name }).ToListAsync(c.Ct);
        if (groups.Count == 0) { await Say(c, "O'quvchi hech qaysi guruhda emas."); return; }
        await Show(c, "➖ Qaysi guruhdan chiqaramiz?", Column(groups.Select(g => new B(g.Name, $"ue2:{sid}:{g.GroupId}"))));
    }

    private async Task EnrollAsync(Ctx c, int sid, int gid, bool move)
    {
        if (move)
            foreach (var old in await c.Db.Enrollments.Where(e => e.StudentId == sid && e.Status == EnrollmentStatus.Active && e.GroupId != gid).Select(e => e.GroupId).ToListAsync(c.Ct))
                await academy.UnenrollAsync(sid, old, c.User!.Id);
        var err = await academy.EnrollAsync(sid, gid, c.User!.Id);
        if (err is not null && !move) { await Show(c, "❌ " + H(err)); return; }
        var st = await c.Db.Students.Include(s => s.User).FirstAsync(s => s.Id == sid, c.Ct);
        if (st.Status == StudentStatus.Applicant) { st.Status = StudentStatus.Active; await c.Db.SaveChangesAsync(c.Ct); }
        var g = await c.Db.Groups.AsNoTracking().FirstAsync(x => x.Id == gid, c.Ct);
        await Show(c, $"✅ {H(st.User.FullName)} → <b>{H(g.Name)}</b>");
        await sender.SendToUserAsync(st.UserId, $"🎉 Siz <b>{H(g.Name)}</b> guruhiga qo'shildingiz!\n🗓 {Labels.Days(g.ScheduleDays)} ⏰ {Time(g)}" +
            (string.IsNullOrWhiteSpace(g.Room) ? "" : $"\n🚪 {H(g.Room)}"), null, c.Ct);
    }

    // ---------------------------------------------------------------- payments

    private async Task PayStartAsync(Ctx c, int sid, int? questionId)
    {
        await c.StepAsync("s_pay_amount", true);
        c.Set("sid", sid.ToString()); c.Set("q", questionId?.ToString()); await c.SaveAsync();
        var name = await c.Db.Students.Where(s => s.Id == sid).Select(s => s.User.FullName).FirstAsync(c.Ct);
        await Say(c, $"💵 {H(name)} — qabul qilingan summa (so'm):");
    }

    private async Task PaySaveAsync(Ctx c, decimal amount)
    {
        var sid = c.GetInt("sid"); var qid = c.GetInt("q");
        var err = await crm.AddPaymentAsync(sid, amount, qid > 0 ? PaymentMethod.Transfer : PaymentMethod.Cash, Tz.Today, qid > 0 ? "Chek (JavohirsMath bot)" : "JavohirsMath bot", null, c.User!.Id);
        await c.ResetAsync();
        if (err is not null) { await Say(c, "❌ " + H(err), MenuFor(c.User)); return; }
        if (qid > 0) await CloseQuestionAsync(c, qid, $"Tasdiqlandi: {Labels.Money(amount)}", null, silent: true);
        var st = await c.Db.Students.AsNoTracking().Include(s => s.User).FirstAsync(s => s.Id == sid, c.Ct);
        var bal = await c.Db.Payments.Where(p => p.StudentId == sid).SumAsync(p => (decimal?)p.Amount, c.Ct) ?? 0;
        await Say(c, $"✅ To'lov kiritildi: {Labels.Money(amount)} so'm. Yangi balans: {Labels.Money(bal)}", MenuFor(c.User));
        var msg = $"✅ To'lovingiz qabul qilindi: <b>{Labels.Money(amount)}</b> so'm.\nBalans: {Labels.Money(bal)} so'm. Rahmat!";
        await sender.SendToUserAsync(st.UserId, msg, null, c.Ct);
        foreach (var p in await academy.ParentsOfAsync(sid)) await sender.SendToUserAsync(p.Id, $"👶 {H(st.User.FullName)}: " + msg, null, c.Ct);
    }

    private async Task DebtorsAsync(Ctx c)
    {
        if (!IsAdmin(c.User)) { await Say(c, "⛔ Faqat adminlar uchun."); return; }
        var list = (await crm.BalancesAsync()).Where(b => b.Balance < 0 && b.Status != StudentStatus.Dropped).OrderBy(b => b.Balance).ToList();
        if (list.Count == 0) { await Say(c, "💰 Qarzdorlar yo'q. 🎉"); return; }
        var sb = new StringBuilder($"💰 <b>Qarzdorlar</b> ({list.Count}) · jami {Labels.Money(list.Sum(b => b.Balance))}\n");
        foreach (var b in list.Take(40)) sb.Append($"\n• {H(b.Name)} — <b>{Labels.Money(b.Balance)}</b>" + (b.NextPaymentDate is DateOnly d ? $" ({d:dd.MM})" : ""));
        var buttons = list.Take(20).Select(b => new B($"{Cut(b.Name, 25)} · {Labels.Money(b.Balance)}", $"st:{b.StudentId}")).ToList();
        buttons.Insert(0, new B("🔔 Hammasiga eslatma yuborish", "debt:all"));
        await Say(c, sb.ToString(), Column(buttons));
    }

    private async Task DebtReminderAsync(Ctx c, int? sid)
    {
        var list = (await crm.BalancesAsync()).Where(b => b.Balance < 0 && (sid is null || b.StudentId == sid)).ToList();
        if (list.Count == 0) { await Show(c, "Qarzdorlik yo'q."); return; }
        var sent = 0;
        foreach (var b in list)
        {
            var st = await c.Db.Students.AsNoTracking().FirstAsync(s => s.Id == b.StudentId, c.Ct);
            var msg = $"💳 <b>To'lov eslatmasi</b>\n{H(b.Name)} bo'yicha qarzdorlik: <b>{Labels.Money(-b.Balance)}</b> so'm." +
                      (b.NextPaymentDate is DateOnly d ? $"\nTo'lov sanasi: {d:dd.MM.yyyy}" : "") + "\n\nTo'lovdan so'ng chekni shu botga yuboring.";
            var kb = Inline([new B("🧾 Chek yuborish", "rcp")]);
            if (await sender.SendToUserAsync(st.UserId, msg, kb, c.Ct)) sent++;
            foreach (var p in await academy.ParentsOfAsync(b.StudentId)) if (await sender.SendToUserAsync(p.Id, msg, kb, c.Ct)) sent++;
        }
        await audit.LogAsync(c.User!.Id, "MentorDebtReminder", "Student", sid, $"{list.Count} students");
        await Say(c, $"🔔 Eslatma yuborildi: {sent} ta xabar ({list.Count} o'quvchi). Botda yo'qlarga yuborilmaydi.");
    }

    // ---------------------------------------------------------------- questions inbox

    private async Task<List<int>> InboxUserScopeAsync(Ctx c)
    {
        var students = await StudentScopeAsync(c);
        var users = await c.Db.Students.Where(s => students.Contains(s.Id)).Select(s => s.UserId).ToListAsync(c.Ct);
        users.AddRange(await c.Db.ParentStudents.Where(p => students.Contains(p.StudentId)).Select(p => p.ParentUserId).ToListAsync(c.Ct));
        return users;
    }

    private async Task InboxAsync(Ctx c)
    {
        var q = c.Db.MentorQuestions.AsNoTracking().Where(x => x.AnsweredAt == null);
        if (!IsAdmin(c.User)) { var users = await InboxUserScopeAsync(c); q = q.Where(x => x.Kind == MentorQuestionKind.Question && x.UserId != null && users.Contains(x.UserId.Value)); }
        var list = await q.OrderBy(x => x.CreatedAt).Take(25).Select(x => new { x.Id, x.Kind, x.Text, Name = x.User != null ? x.User.FullName : "?" }).ToListAsync(c.Ct);
        if (list.Count == 0) { await Say(c, "❓ Javob kutayotgan savollar yo'q. 🎉"); return; }
        await Say(c, $"❓ <b>Savollar inbox</b> ({list.Count})",
            Column(list.Select(x => new B($"{(x.Kind == MentorQuestionKind.Receipt ? "🧾" : "❓")} {Cut(x.Name, 18)}: {Cut(x.Text, 28)}", $"q:{x.Id}"))));
    }

    private async Task<MentorQuestion?> LoadQuestionAsync(Ctx c, int id)
    {
        var q = await c.Db.MentorQuestions.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id, c.Ct);
        if (q is null) return null;
        if (IsAdmin(c.User)) return q;
        if (q.Kind != MentorQuestionKind.Question || q.UserId is null || !(await InboxUserScopeAsync(c)).Contains(q.UserId.Value)) return null;
        return q;
    }

    private async Task QuestionDetailAsync(Ctx c, int id)
    {
        var q = await LoadQuestionAsync(c, id);
        if (q is null) { await Say(c, "Topilmadi."); return; }
        await client.CopyAsync(c.ChatId, q.ChatId, q.MessageId, c.Ct);
        var head = $"{(q.Kind == MentorQuestionKind.Receipt ? "🧾 <b>Chek</b>" : "❓ <b>Savol</b>")} · {H(q.User?.FullName)} · {Tz.Fmt(q.CreatedAt)}" +
                   (q.AnsweredAt is null ? "" : $"\n✔️ {H(q.Answer)}");
        object kb = q.Kind == MentorQuestionKind.Receipt
            ? Inline([new B("✅ Tasdiqlash (summa)", $"rcok:{id}"), new B("❌ Rad etish", $"rcno:{id}")])
            : Inline([new B("✍️ Javob berish", $"qa:{id}"), new B("✔️ Yopish", $"qc:{id}")]);
        await Say(c, head, q.AnsweredAt is null ? kb : null);
    }

    private async Task AnswerStartAsync(Ctx c, int id)
    {
        if (await LoadQuestionAsync(c, id) is null) return;
        await c.StepAsync("s_qa_text", true);
        c.Set("q", id.ToString()); await c.SaveAsync();
        await Say(c, "✍️ Javobingizni yozing:");
    }

    private async Task AnswerSendAsync(Ctx c, string answer)
    {
        var q = await LoadQuestionAsync(c, c.GetInt("q"));
        await c.ResetAsync();
        if (q is null) return;
        q.Answer = answer.Length > 2000 ? answer[..2000] : answer; q.AnsweredAt = DateTime.UtcNow; q.AnsweredByUserId = c.User!.Id;
        await c.Db.SaveChangesAsync(c.Ct);
        await sender.SendAsync(q.ChatId, $"💬 <b>Mentor javobi</b> ({H(c.User.FullName)})\n\n<i>Savolingiz: {H(Cut(q.Text, 200))}</i>\n\n{H(answer)}", null, c.Ct);
        await Say(c, "✅ Javob yuborildi.", MenuFor(c.User));
        await InboxAsync(c);
    }

    private async Task CloseQuestionAsync(Ctx c, int id, string answer, string? notify, bool silent = false)
    {
        var q = await LoadQuestionAsync(c, id);
        if (q is null) return;
        q.Answer = answer; q.AnsweredAt = DateTime.UtcNow; q.AnsweredByUserId = c.User!.Id;
        await c.Db.SaveChangesAsync(c.Ct);
        if (notify is not null) await sender.SendAsync(q.ChatId, notify, null, c.Ct);
        if (!silent) await Show(c, $"✔️ {H(answer)}.");
    }

    private async Task ReceiptAcceptAsync(Ctx c, int id)
    {
        var q = await LoadQuestionAsync(c, id);
        if (q?.UserId is not int uid) return;
        var sid = await c.Db.Students.Where(s => s.UserId == uid).Select(s => (int?)s.Id).FirstOrDefaultAsync(c.Ct)
                  ?? await c.Db.ParentStudents.Where(p => p.ParentUserId == uid).Select(p => (int?)p.StudentId).FirstOrDefaultAsync(c.Ct);
        if (sid is null) { await Say(c, "Bu foydalanuvchiga bog'langan o'quvchi topilmadi."); return; }
        await PayStartAsync(c, sid.Value, id);
    }

    // ---------------------------------------------------------------- risk / reports

    public record RiskRow(int StudentId, string Name, string Reason);

    /// <summary>Students who missed the last 2 lessons in a row or have 2+ overdue homeworks.</summary>
    public static async Task<List<RiskRow>> RiskAsync(AppDbContext db, IReadOnlyCollection<int> studentIds, CancellationToken ct)
    {
        var since = Tz.Today.AddDays(-60);
        var att = await db.Attendances.AsNoTracking().Where(a => studentIds.Contains(a.StudentId) && a.Date >= since)
            .Select(a => new { a.StudentId, a.Date, a.Status }).ToListAsync(ct);
        var monthAgo = DateTime.UtcNow.AddDays(-30);
        var overdue = await db.HomeworkSubmissions.AsNoTracking().Where(s => studentIds.Contains(s.StudentId) && (s.Status == SubmissionStatus.NotStarted || s.Status == SubmissionStatus.InProgress)
                && s.Homework.Deadline < DateTime.UtcNow && s.Homework.Deadline > monthAgo)
            .GroupBy(s => s.StudentId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var names = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id) && s.Status == StudentStatus.Active)
            .Select(s => new { s.Id, s.User.FullName }).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var result = new List<RiskRow>();
        foreach (var (sid, name) in names)
        {
            var reasons = new List<string>();
            var last = att.Where(a => a.StudentId == sid).OrderByDescending(a => a.Date).Take(2).ToList();
            if (last.Count == 2 && last.All(a => a.Status == AttendanceStatus.Absent)) reasons.Add($"ketma-ket 2 dars qoldirdi ({last[1].Date:dd.MM}, {last[0].Date:dd.MM})");
            if (overdue.GetValueOrDefault(sid) >= 2) reasons.Add($"{overdue[sid]} ta vazifa topshirilmagan");
            if (reasons.Count > 0) result.Add(new RiskRow(sid, name, string.Join("; ", reasons)));
        }
        return result.OrderBy(r => r.Name).ToList();
    }

    private async Task RiskAsync(Ctx c)
    {
        var rows = await RiskAsync(c.Db, await StudentScopeAsync(c), c.Ct);
        if (rows.Count == 0) { await Say(c, "⚠️ Xavf signallari yo'q. 🎉"); return; }
        var sb = new StringBuilder($"⚠️ <b>Xavf signallari</b> ({rows.Count})\n");
        foreach (var r in rows) sb.Append($"\n• <b>{H(r.Name)}</b> — {H(r.Reason)}");
        await Say(c, sb.ToString(), Column(rows.Take(20).Select(r => new B(r.Name, $"st:{r.StudentId}"))));
    }

    private async Task ReportAsync(Ctx c, int gid)
    {
        if (!await CanGroupAsync(c, gid)) return;
        var g = await c.Db.Groups.AsNoTracking().FirstAsync(x => x.Id == gid, c.Ct);
        var students = await c.Db.Enrollments.AsNoTracking().Where(e => e.GroupId == gid && e.Status == EnrollmentStatus.Active)
            .Select(e => new { e.StudentId, e.Student.User.FullName, e.Student.User.Phone }).OrderBy(x => x.FullName).ToListAsync(c.Ct);
        var from = Tz.Today.AddDays(-30);
        var att = await academy.AttendanceRangeAsync(gid, from, Tz.Today);
        var ids = students.Select(s => s.StudentId).ToList();
        var subs = await c.Db.HomeworkSubmissions.AsNoTracking().Where(s => s.Homework.GroupId == gid && ids.Contains(s.StudentId)).ToListAsync(c.Ct);
        var bal = await c.Db.Payments.Where(p => ids.Contains(p.StudentId)).GroupBy(p => p.StudentId).Select(x => new { x.Key, S = x.Sum(p => p.Amount) }).ToDictionaryAsync(x => x.Key, x => x.S, c.Ct);
        var risk = (await RiskAsync(c.Db, ids, c.Ct)).ToDictionary(r => r.StudentId, r => r.Reason);

        var rows = students.Select(s =>
        {
            var a = att.Where(x => x.StudentId == s.StudentId).ToList();
            var pct = a.Count == 0 ? (int?)null : a.Count(x => x.Status is AttendanceStatus.Present or AttendanceStatus.Late) * 100 / a.Count;
            var mine = subs.Where(x => x.StudentId == s.StudentId).ToList();
            var graded = mine.Where(x => x.Score is not null && x.MaxScore is > 0).ToList();
            var avg = graded.Count == 0 ? (int?)null : (int)graded.Average(x => x.Score!.Value * 100.0 / x.MaxScore!.Value);
            return new { s.FullName, s.Phone, Att = pct, Sub = $"{mine.Count(x => x.Status >= SubmissionStatus.Submitted)}/{mine.Count}", Avg = avg, Bal = bal.GetValueOrDefault(s.StudentId), Risk = risk.GetValueOrDefault(s.StudentId) };
        }).ToList();

        var lessons = att.Select(a => a.Date).Distinct().Count();
        var avgAtt = rows.Where(r => r.Att is not null).Select(r => r.Att!.Value).DefaultIfEmpty().Average();
        var sb = new StringBuilder($"📈 <b>{H(g.Name)}</b> — so'nggi 30 kun\n🎓 {rows.Count} o'quvchi · 📅 {lessons} dars · davomat o'rtacha {avgAtt:0}%\n");
        foreach (var r in rows.Take(40))
            sb.Append($"\n• {H(r.FullName)} — 🗓 {(r.Att is null ? "—" : r.Att + "%")} · 📝 {r.Sub}{(r.Avg is null ? "" : $" ({r.Avg}%)")}{(r.Bal < 0 ? " · 🔴" : "")}{(r.Risk is null ? "" : " ⚠️")}");
        await Say(c, sb.ToString());

        var csv = CsvExport.Build(["O'quvchi", "Telefon", "Davomat %", "Vazifalar", "O'rtacha baho %", "Balans", "Xavf"],
            rows.Select(r => new object?[] { r.FullName, r.Phone, r.Att, r.Sub, r.Avg, r.Bal, r.Risk }));
        await sender.SendDocumentAsync(c.ChatId, Encoding.UTF8.GetBytes(csv), $"hisobot-{g.Name}-{Tz.Today:yyyy-MM-dd}.csv".Replace(' ', '_'), "📎 Excel'da ochiladi (CSV)", c.Ct);
    }
}
