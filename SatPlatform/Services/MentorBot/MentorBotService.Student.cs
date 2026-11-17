using System.Text;
using Regex = System.Text.RegularExpressions.Regex;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

public partial class MentorBotService
{
    // ---------------------------------------------------------------- schedule

    private async Task ScheduleAsync(Ctx c)
    {
        var students = await MyStudentsAsync(c);
        if (students.Count == 0) { await Say(c, "Hali o'quvchi bog'lanmagan."); return; }
        var ids = students.Select(s => s.Id).ToList();
        var rows = await c.Db.Enrollments.AsNoTracking().Include(e => e.Group).ThenInclude(g => g.TeacherUser)
            .Where(e => ids.Contains(e.StudentId) && e.Status == EnrollmentStatus.Active && e.Group.IsActive).ToListAsync(c.Ct);
        if (rows.Count == 0) { await Say(c, "📅 Siz hali guruhga qo'shilmagansiz. Mentor qo'shgach jadval shu yerda chiqadi."); return; }
        var sb = new StringBuilder("📅 <b>Dars jadvali</b>\n");
        foreach (var e in rows)
        {
            var who = students.Count > 1 ? $" — {H(students.First(s => s.Id == e.StudentId).User.FullName)}" : "";
            sb.Append($"\n<b>{H(e.Group.Name)}</b>{who}\n🗓 {Labels.Days(e.Group.ScheduleDays)}  ⏰ {Time(e.Group)}");
            if (!string.IsNullOrWhiteSpace(e.Group.Room)) sb.Append($"\n🚪 Xona: {H(e.Group.Room)}");
            if (e.Group.TeacherUser is not null) sb.Append($"\n👨‍🏫 {H(e.Group.TeacherUser.FullName)}");
            var next = NextLesson(e.Group);
            if (next is not null) sb.Append($"\n➡️ Keyingi dars: {next}");
            sb.Append('\n');
        }
        sb.Append("\n🔔 Dars oldidan 1 soat va 10 daqiqa qolganda eslatma keladi.");
        await Say(c, sb.ToString());
    }

    private static string? NextLesson(Group g)
    {
        if (g.ScheduleDays.Length == 0) return null;
        var now = Tz.ToLocal(DateTime.UtcNow);
        for (var i = 0; i < 8; i++)
        {
            var d = DateOnly.FromDateTime(now).AddDays(i);
            if (!AcademyService.HasClassOn(g, d)) continue;
            if (i == 0 && g.StartTime is TimeOnly st && st < TimeOnly.FromDateTime(now)) continue;
            return (i == 0 ? "bugun" : i == 1 ? "ertaga" : d.ToString("dd.MM")) + (g.StartTime is TimeOnly s ? $" {s:HH\\:mm}" : "");
        }
        return null;
    }

    // ---------------------------------------------------------------- homework

    private async Task StudentHomeworkAsync(Ctx c)
    {
        var sid = await StudentIdAsync(c);
        if (sid is null) { await Say(c, "Faqat o'quvchilar uchun."); return; }
        var list = await homework.ForStudentAsync(sid.Value);
        var open = list.Where(x => x.Submission?.Status is not SubmissionStatus.Reviewed && x.Homework.Deadline > DateTime.UtcNow.AddDays(-7)).Take(15).ToList();
        var done = list.Where(x => x.Submission?.Status == SubmissionStatus.Reviewed).OrderByDescending(x => x.Submission!.ReviewedAt).Take(5).ToList();
        if (open.Count == 0 && done.Count == 0) { await Say(c, "📝 Hozircha uy vazifasi yo'q. 🎉"); return; }
        var sb = new StringBuilder("📝 <b>Uy vazifalari</b>\n");
        if (open.Count > 0) sb.Append("\nJoriy (bosib oching):");
        if (done.Count > 0)
        {
            sb.Append("\n\n✅ <b>Tekshirilganlar</b>");
            foreach (var d in done) sb.Append($"\n• {H(d.Homework.Title)} — <b>{Score(d.Submission!)}</b>");
        }
        var buttons = open.Select(x => new B($"{StatusIcon(x)} {Cut(x.Homework.Title, 30)} · {Tz.Fmt(x.Homework.Deadline, "dd.MM HH:mm")}", $"sh:{x.Homework.Id}"));
        await Say(c, sb.ToString(), open.Count > 0 ? Column(buttons) : null);
    }

    private static string StatusIcon(StudentHomework x) => x.Submission?.Status switch
    {
        SubmissionStatus.Submitted => "📤",
        _ when x.Homework.Deadline < DateTime.UtcNow => "⏰",
        _ => "🟡"
    };

    private static string Score(HomeworkSubmission s) => s.Score is null ? "baholanmagan" : s.MaxScore is null ? $"{s.Score}" : $"{s.Score}/{s.MaxScore}";
    private static string Cut(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    private async Task StudentHomeworkDetailAsync(Ctx c, int hwId)
    {
        var sid = await StudentIdAsync(c);
        if (sid is null) return;
        var sub = await c.Db.HomeworkSubmissions.AsNoTracking().Include(s => s.Homework).FirstOrDefaultAsync(s => s.HomeworkId == hwId && s.StudentId == sid, c.Ct);
        if (sub is null) { await Say(c, "Vazifa topilmadi."); return; }
        var h = sub.Homework;
        var sb = new StringBuilder($"📝 <b>{H(h.Title)}</b>\n⏰ Muddat: {Tz.Fmt(h.Deadline)}\n");
        if (!string.IsNullOrWhiteSpace(h.Description)) sb.Append($"\n{H(h.Description)}\n");
        sb.Append($"\nHolat: <b>{Labels.Submission(sub.Status, h.Deadline)}</b>");
        if (sub.TextAnswer is not null) sb.Append($"\nJavobingiz: <i>{H(Cut(sub.TextAnswer, 300))}</i>");
        if (sub.Status == SubmissionStatus.Reviewed) sb.Append($"\nBaho: <b>{Score(sub)}</b>");
        if (!string.IsNullOrWhiteSpace(sub.TeacherComment)) sb.Append($"\n💬 Izoh: {H(sub.TeacherComment)}");
        object? kb = null;
        if (h.Kind == HomeworkKind.File && sub.Status != SubmissionStatus.Reviewed)
            kb = Inline([new B(sub.Status == SubmissionStatus.Submitted ? "🔁 Javobni qayta yuborish" : "📤 Javob yuborish", $"sb:{h.Id}")]);
        else if (h.Kind != HomeworkKind.File)
            sb.Append("\n\nℹ️ Bu onlayn mashq/test turidagi vazifa — uni mentor ko'rsatmasi bo'yicha bajaring.");
        await Say(c, sb.ToString(), kb);
        if (h.FileId is int fid) await SendMediaFileAsync(c, fid, "📎 Vazifa fayli");
    }

    private async Task SendMediaFileAsync(Ctx c, int fileId, string caption)
    {
        var f = await c.Db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == fileId, c.Ct);
        if (f is not null) await sender.SendDocumentAsync(c.ChatId, f.Data, f.OriginalName, caption, c.Ct);
    }

    private async Task SubmitStartAsync(Ctx c, int hwId)
    {
        var sid = await StudentIdAsync(c);
        if (sid is null || !await c.Db.HomeworkSubmissions.AnyAsync(s => s.HomeworkId == hwId && s.StudentId == sid && s.Status != SubmissionStatus.Reviewed, c.Ct))
        { await Say(c, "Bu vazifaga javob yuborib bo'lmaydi."); return; }
        c.Set("hw", hwId.ToString());
        await c.StepAsync("sub");
        await Say(c, "📤 Javobingizni yuboring: <b>rasm</b>, <b>PDF/fayl</b> yoki <b>matn</b>.\nRasmga izoh (caption) ham yozishingiz mumkin. Bekor qilish: /cancel");
    }

    private async Task<bool> SubmitReceiveAsync(Ctx c)
    {
        var sid = await StudentIdAsync(c);
        var hwId = c.GetInt("hw");
        if (sid is null || hwId == 0) return false;
        int? fileId = null;
        if (c.U.FileId is not null)
        {
            var (id, err) = await SaveTelegramFileAsync(c);
            if (err is not null) { await Say(c, "❌ " + H(err)); return true; }
            fileId = id;
        }
        else if (c.U.MediaKind is "voice" or "video") { await Say(c, "Ovozli xabar yoki video qabul qilinmaydi — rasm, fayl yoki matn yuboring."); return true; }
        var text = string.IsNullOrWhiteSpace(c.U.Text) ? null : c.U.Text.Trim();
        var error = await homework.SubmitFileAsync(hwId, sid.Value, fileId, text);
        if (error is not null) { await Say(c, "❌ " + H(error)); return true; }
        await c.ResetAsync();
        await Say(c, "✅ Javobingiz qabul qilindi! Mentor tekshirgach, baho va izoh shu yerga keladi.", MenuFor(c.User!));

        var sub = await c.Db.HomeworkSubmissions.AsNoTracking().Include(s => s.Homework).FirstAsync(s => s.HomeworkId == hwId && s.StudentId == sid, c.Ct);
        var staff = (await TeachersOfStudentAsync(c.Db, sid.Value, c.Ct)).Append(sub.Homework.CreatedByUserId).Distinct();
        foreach (var u in staff)
            await sender.SendToUserAsync(u, $"📥 <b>Yangi topshiriq</b>\n👤 {H(c.User!.FullName)}\n📝 {H(sub.Homework.Title)}", Inline([new B("Ko'rish", $"sub:{sub.Id}")]), c.Ct);
        return true;
    }

    /// <summary>Downloads the photo/document of the current message and stores it as a MediaFile.</summary>
    private async Task<(int? Id, string? Error)> SaveTelegramFileAsync(Ctx c)
    {
        if (c.U.FileSize > 20 * 1024 * 1024) return (null, "Fayl 20 MB dan katta.");
        var data = await client.DownloadAsync(c.U.FileId!, c.Ct);
        if (data is null) return (null, "Faylni yuklab bo'lmadi, qaytadan yuboring.");
        var mime = c.U.MimeType ?? "application/octet-stream";
        using var ms = new MemoryStream(data);
        var (_, id, err) = await media.UploadAsync(ms, c.U.FileName ?? "fayl", mime, data.Length, c.User!.Id, imagesOnly: false);
        return (id, err);
    }

    // ---------------------------------------------------------------- results

    private async Task ResultsAsync(Ctx c)
    {
        var sid = await StudentIdAsync(c);
        if (sid is null) return;
        await Say(c, await StudentReportAsync(c.Db, sid.Value, c.Ct));
    }

    /// <summary>Text summary of one student (used by students, parents and staff).</summary>
    private async Task<string> StudentReportAsync(AppDbContext db, int sid, CancellationToken ct)
    {
        var st = await db.Students.AsNoTracking().Include(s => s.User).FirstAsync(s => s.Id == sid, ct);
        var att = await academy.AttendanceSummaryAsync(sid);
        var total = att.Present + att.Absent + att.Late + att.Excused;
        var subs = await db.HomeworkSubmissions.AsNoTracking().Include(s => s.Homework).Where(s => s.StudentId == sid).ToListAsync(ct);
        var reviewed = subs.Where(s => s.Score is not null && s.MaxScore is > 0).ToList();
        var quizzes = await db.MentorQuizResults.AsNoTracking().Where(q => q.StudentId == sid).OrderByDescending(q => q.CreatedAt).Take(5).ToListAsync(ct);
        var scores = await db.ScoreHistories.AsNoTracking().Where(s => s.StudentId == sid).OrderByDescending(s => s.CreatedAt).Take(5).ToListAsync(ct);
        var weak = await db.StudentSkillStatistics.AsNoTracking().Include(s => s.Skill).Where(s => s.StudentId == sid && s.AttemptedCount >= 3)
            .OrderBy(s => (double)s.CorrectCount / s.AttemptedCount).Take(3).ToListAsync(ct);

        var sb = new StringBuilder($"📊 <b>{H(st.User.FullName)} — natijalar</b>\n");
        sb.Append($"\n🗓 <b>Davomat</b>: {(total == 0 ? "ma'lumot yo'q" : $"{(att.Present + att.Late) * 100 / total}% ({att.Present} keldi, {att.Late} kechikdi, {att.Absent} kelmadi, {att.Excused} sababli)")}");
        var overdue = subs.Count(s => s.Status is SubmissionStatus.NotStarted or SubmissionStatus.InProgress && s.Homework.Deadline < DateTime.UtcNow);
        sb.Append($"\n📝 <b>Vazifalar</b>: {subs.Count(s => s.Status >= SubmissionStatus.Submitted)}/{subs.Count} topshirilgan" + (overdue > 0 ? $", ⏰ {overdue} ta muddati o'tgan" : ""));
        if (reviewed.Count > 0) sb.Append($"\n   O'rtacha baho: <b>{reviewed.Average(s => s.Score!.Value * 100.0 / s.MaxScore!.Value):0}%</b>");
        if (quizzes.Count > 0) sb.Append($"\n🧠 <b>Mini testlar</b>: " + string.Join(", ", quizzes.Select(q => $"{q.Correct}/{q.Total}")));
        if (scores.Count > 0)
        {
            sb.Append("\n🎯 <b>SAT mock natijalari</b>:");
            foreach (var s in scores) sb.Append($"\n   {Tz.FmtDate(s.CreatedAt)} — <b>{s.TotalScore}</b> (R&W {s.RwScore}, Math {s.MathScore})");
            if (scores.Count > 1) sb.Append($"\n   Dinamika: {(scores[0].TotalScore - scores[^1].TotalScore >= 0 ? "📈 +" : "📉 ")}{scores[0].TotalScore - scores[^1].TotalScore}");
        }
        if (weak.Count > 0) sb.Append("\n⚠️ <b>Zaif mavzular</b>: " + string.Join(", ", weak.Select(w => $"{H(w.Skill.Name)} ({w.Accuracy:0}%)")));
        return sb.ToString();
    }

    // ---------------------------------------------------------------- parent

    private async Task ChildrenAsync(Ctx c)
    {
        var kids = await MyStudentsAsync(c);
        if (kids.Count == 0) { await Say(c, "Farzand bog'lanmagan. Mentordan ota-ona kodini so'rang."); return; }
        foreach (var k in kids)
        {
            var sb = new StringBuilder(await StudentReportAsync(c.Db, k.Id, c.Ct));
            var last = await academy.StudentAttendanceAsync(k.Id, 8);
            if (last.Count > 0) sb.Append("\n\n🗓 So'nggi darslar: " + string.Join(" ", last.OrderBy(a => a.Date).Select(a => $"{a.Date:dd.MM}{AttIcon(a.Status)}")));
            var bal = await c.Db.Payments.Where(p => p.StudentId == k.Id).SumAsync(p => (decimal?)p.Amount, c.Ct) ?? 0;
            sb.Append($"\n💳 Balans: <b>{Labels.Money(bal)}</b> so'm");
            await Say(c, sb.ToString());
        }
    }

    private static string AttIcon(AttendanceStatus s) => s switch { AttendanceStatus.Present => "✅", AttendanceStatus.Absent => "❌", AttendanceStatus.Late => "⏱", _ => "🟡" };

    // ---------------------------------------------------------------- payment

    private async Task PaymentAsync(Ctx c)
    {
        var students = await MyStudentsAsync(c);
        if (students.Count == 0) { await Say(c, "Ma'lumot yo'q."); return; }
        var sb = new StringBuilder("💳 <b>To'lov holati</b>\n");
        foreach (var s in students)
        {
            var pays = await crm.PaymentsAsync(s.Id);
            var bal = await c.Db.Payments.Where(p => p.StudentId == s.Id).SumAsync(p => (decimal?)p.Amount, c.Ct) ?? 0;
            if (students.Count > 1) sb.Append($"\n<b>{H(s.User.FullName)}</b>");
            sb.Append($"\nBalans: <b>{Labels.Money(bal)}</b> so'm {(bal < 0 ? "🔴 qarzdorlik" : "🟢")}");
            if (s.NextPaymentDate is DateOnly d) sb.Append($"\nKeyingi to'lov: <b>{d:dd.MM.yyyy}</b>" + (d < Tz.Today ? " ⏰ muddati o'tgan" : ""));
            foreach (var p in pays.Take(5)) sb.Append($"\n  {p.Date:dd.MM} · {Labels.PaymentMethod(p.Method)} · {Labels.Money(p.Amount)}");
            sb.Append('\n');
        }
        sb.Append("\nTo'lov qilgan bo'lsangiz, chekni yuboring — mentor tasdiqlaydi.");
        await Say(c, sb.ToString(), Inline([new B("🧾 Chek yuborish", "rcp")]));
    }

    // ---------------------------------------------------------------- ask mentor / receipt

    private async Task AskStartAsync(Ctx c)
    {
        await c.StepAsync("ask", true);
        await Say(c, "❓ Savolingizni yozing — matn, rasm (masala surati) yoki ovozli xabar bo'lishi mumkin. Javob kelganda shu yerga xabar beraman.\nBekor qilish: /cancel");
    }

    private async Task<bool> AskReceiveAsync(Ctx c)
    {
        var receipt = c.Chat.Step == "receipt";
        if (receipt && c.U.FileId is null) { await Say(c, "Chekning rasmini yoki PDF faylini yuboring."); return true; }
        var text = c.U.Text?.Trim() ?? "";
        var label = c.U.MediaKind switch { "photo" => "[rasm] ", "document" => "[fayl] ", "voice" => "[ovozli xabar] ", "video" => "[video] ", _ => "" } + text;
        if (label.Trim().Length == 0) return false;
        var q = new MentorQuestion { Kind = receipt ? MentorQuestionKind.Receipt : MentorQuestionKind.Question, ChatId = c.ChatId, UserId = c.User!.Id, MessageId = c.U.MessageId, TelegramFileId = c.U.FileId, Text = Cut(label, 2000) };
        c.Db.MentorQuestions.Add(q);
        await c.ResetAsync();
        await Say(c, receipt ? "🧾 Chek yuborildi. Tasdiqlangach xabar beraman." : "✅ Savolingiz mentorga yuborildi. Javob kelganda shu yerga keladi.", MenuFor(c.User));

        var who = $"👤 {H(c.User.FullName)} ({Labels.Role(c.User.Role)})";
        var html = receipt ? $"🧾 <b>To'lov cheki</b>\n{who}" : $"❓ <b>Yangi savol</b>\n{who}\n\n{H(Cut(label, 500))}";
        var kb = Inline([new B("Ko'rish / javob", $"q:{q.Id}")]);
        var targets = new HashSet<int>(await AdminUserIdsAsync(c.Ct));
        if (!receipt)
        {
            foreach (var s in await MyStudentsAsync(c))
                foreach (var t in await TeachersOfStudentAsync(c.Db, s.Id, c.Ct)) targets.Add(t);
        }
        foreach (var t in targets) await sender.SendToUserAsync(t, html, kb, c.Ct);
        return true;
    }

    // ---------------------------------------------------------------- materials

    private async Task MaterialsAsync(Ctx c, string? search)
    {
        var students = await MyStudentsAsync(c);
        var ids = students.Select(s => s.Id).ToList();
        var groupIds = await c.Db.Enrollments.Where(e => ids.Contains(e.StudentId) && e.Status == EnrollmentStatus.Active).Select(e => e.GroupId).ToListAsync(c.Ct);
        var list = await materials.ListAsync(null, groupIds);
        if (!string.IsNullOrWhiteSpace(search))
            list = list.Where(m => m.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || (m.Description ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (list.Count == 0) { await Say(c, search is null ? "📚 Kutubxona hozircha bo'sh." : "🔎 Hech narsa topilmadi.", Inline([new B("🔎 Qidirish", "msr")])); return; }
        var buttons = list.Take(30).Select(m => new B($"{(m.FileId is not null ? "📎" : "🔗")} {Cut(m.Title, 40)} · {Labels.Material(m.Category)}", $"m:{m.Id}")).ToList();
        buttons.Add(new B("🔎 Qidirish", "msr"));
        await Say(c, search is null ? $"📚 <b>Materiallar</b> ({list.Count})" : $"🔎 «{H(search)}» bo'yicha: {list.Count}", Column(buttons));
    }

    private async Task<bool> MaterialsSearchAsync(Ctx c)
    {
        if (c.Text.Length == 0) return false;
        await c.ResetAsync();
        await MaterialsAsync(c, c.Text);
        return true;
    }

    private async Task MaterialSendAsync(Ctx c, int id)
    {
        var m = await c.Db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, c.Ct);
        if (m is null) return;
        if (m.GroupId is int g && !IsStaff(c.User))
        {
            var ids = (await MyStudentsAsync(c)).Select(s => s.Id).ToList();
            if (!await c.Db.Enrollments.AnyAsync(e => ids.Contains(e.StudentId) && e.GroupId == g && e.Status == EnrollmentStatus.Active, c.Ct)) return;
        }
        var caption = $"📚 <b>{H(m.Title)}</b>" + (string.IsNullOrWhiteSpace(m.Description) ? "" : $"\n{H(m.Description)}");
        if (m.FileId is int fid) await SendMediaFileAsync(c, fid, caption);
        if (m.ExternalUrl is not null) await Say(c, caption + $"\n🔗 {H(m.ExternalUrl)}");
    }

    // ---------------------------------------------------------------- mini test

    private const int QuizSize = 5;

    private async Task QuizStartAsync(Ctx c)
    {
        if (await StudentIdAsync(c) is null) return;
        await Say(c, $"🧠 <b>Mini test</b> — {QuizSize} ta savol, darhol tekshiriladi.\nBo'limni tanlang:",
            Inline([new B("➗ Math", "qs:1"), new B("📖 Reading & Writing", "qs:0")], [new B("🔀 Aralash", "qs:x")]));
    }

    private static readonly string[] Unrenderable = ["<img", "\\(", "\\[", "$$", "<table", "<svg", "<math", "katex"];

    private async Task QuizBeginAsync(Ctx c, string section)
    {
        if (await StudentIdAsync(c) is null) return;
        var q = c.Db.Questions.AsNoTracking().Where(x => x.Status == QuestionStatus.Active && x.Type == QuestionType.MultipleChoice);
        if (section is "0" or "1") { var s = (Section)int.Parse(section); q = q.Where(x => x.Section == s); }
        // Telegram can't render formulas / images, so only plain-text questions go into the bot quiz
        foreach (var bad in Unrenderable)
            q = q.Where(x => !x.Text.Contains(bad) && (x.Passage == null || !x.Passage.Contains(bad)) && !x.Options.Any(o => o.Text.Contains(bad)));
        var ids = await q.Where(x => x.Passage == null || x.Passage.Length < 2500).OrderBy(_ => EF.Functions.Random()).Select(x => x.Id).Take(QuizSize).ToListAsync(c.Ct);
        if (ids.Count == 0) { await Show(c, "Hozircha botda ko'rsatsa bo'ladigan savollar yo'q (formulali savollar Telegramda ko'rinmaydi)."); return; }
        c.Set("qz", string.Join(',', ids)); c.Set("qi", "0"); c.Set("qc", "0"); c.Set("qw", "");
        await c.StepAsync("quiz");
        await Show(c, $"🧠 Boshladik! {ids.Count} ta savol.");
        await QuizAskAsync(c);
    }

    private async Task QuizAskAsync(Ctx c)
    {
        var ids = (c.Get("qz") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var i = c.GetInt("qi");
        if (i >= ids.Count) { await QuizFinishAsync(c, ids.Count); return; }
        var q = await c.Db.Questions.AsNoTracking().Include(x => x.Options).FirstAsync(x => x.Id == ids[i], c.Ct);
        var sb = new StringBuilder($"<b>{i + 1}/{ids.Count}</b> · {Labels.SectionShort(q.Section)} · {Labels.Difficulty(q.Difficulty)}\n\n");
        if (!string.IsNullOrWhiteSpace(q.Passage)) sb.Append($"<i>{H(PlainText(q.Passage))}</i>\n\n");
        sb.Append(H(PlainText(q.Text))).Append('\n');
        var opts = q.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Key).ToList();
        foreach (var o in opts) sb.Append($"\n<b>{H(o.Key)})</b> {H(PlainText(o.Text))}");
        await Say(c, sb.ToString(), Inline(opts.Select(o => new B(o.Key, $"qz:{q.Id}:{o.Key}")).ToArray()));
    }

    private async Task QuizAnswerAsync(Ctx c, int qid, string key)
    {
        if (c.Chat.Step != "quiz") return;
        var ids = (c.Get("qz") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
        var i = c.GetInt("qi");
        if (i >= ids.Count || ids[i] != qid) return; // stale button
        var q = await c.Db.Questions.AsNoTracking().Include(x => x.Options).Include(x => x.Skill).FirstAsync(x => x.Id == qid, c.Ct);
        var correct = q.Options.FirstOrDefault(o => o.IsCorrect);
        var ok = correct?.Key == key;
        if (ok) c.Set("qc", (c.GetInt("qc") + 1).ToString());
        else c.Set("qw", string.Join('|', (c.Get("qw") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).Append(q.Skill.Name).Distinct()));
        c.Set("qi", (i + 1).ToString());
        await c.SaveAsync();
        var fb = ok ? $"✅ To'g'ri! ({H(key)})" : $"❌ Noto'g'ri. Siz: {H(key)}, to'g'ri javob: <b>{H(correct?.Key)}</b>";
        if (!string.IsNullOrWhiteSpace(q.Explanation) && !Unrenderable.Any(b => q.Explanation.Contains(b)))
            fb += $"\n\n💡 {H(Cut(PlainText(q.Explanation), 700))}";
        fb += $"\n🏷 {H(q.Skill.Name)}";
        await Say(c, fb);
        await QuizAskAsync(c);
    }

    private async Task QuizFinishAsync(Ctx c, int total)
    {
        var sid = await StudentIdAsync(c);
        var correct = c.GetInt("qc");
        var weak = (c.Get("qw") ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries);
        if (sid is int s) c.Db.MentorQuizResults.Add(new MentorQuizResult { StudentId = s, Correct = correct, Total = total, Topics = Cut(string.Join(", ", weak), 300) });
        await c.ResetAsync();
        var emoji = correct == total ? "🏆" : correct * 2 >= total ? "👍" : "💪";
        await Say(c, $"{emoji} <b>Natija: {correct}/{total}</b>" + (weak.Length > 0 ? $"\n\nTakrorlash kerak: {H(string.Join(", ", weak))}" : "\n\nBarakalla!"),
            Inline([new B("🔁 Yana", "qs:x")]));
    }

    private static string PlainText(string html)
    {
        var s = Regex.Replace(html, @"<(br|/p|/div|/li)\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, "<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);
        return Regex.Replace(s, @"\n{3,}", "\n\n").Trim();
    }
}
