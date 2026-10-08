using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public class QuestionFilter
{
    public Section? Section { get; set; }
    public int? DomainId { get; set; }
    public int? SkillId { get; set; }
    public Difficulty? Difficulty { get; set; }
    public QuestionStatus? Status { get; set; }
    public QuestionType? Type { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int? ExcludeModuleId { get; set; }
}

public class QuestionEdit
{
    public int Id { get; set; }
    public Section Section { get; set; }
    public int DomainId { get; set; }
    public int SkillId { get; set; }
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public QuestionType Type { get; set; }
    public QuestionStatus Status { get; set; } = QuestionStatus.Draft;
    public string? Passage { get; set; }
    public string Text { get; set; } = "";
    public string? Explanation { get; set; }
    public string? VideoUrl { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? Tag { get; set; }
    public List<OptionEdit> Options { get; set; } = [new("A"), new("B"), new("C"), new("D")];

    public static QuestionEdit From(Question q) => new()
    {
        Id = q.Id, Section = q.Section, DomainId = q.DomainId, SkillId = q.SkillId, Difficulty = q.Difficulty, Type = q.Type, Status = q.Status,
        Passage = q.Passage, Text = q.Text, Explanation = q.Explanation, VideoUrl = q.VideoUrl, CorrectAnswer = q.CorrectAnswer, Tag = q.Tag,
        Options = q.Options.OrderBy(o => o.SortOrder).Select(o => new OptionEdit(o.Key) { Text = o.Text, IsCorrect = o.IsCorrect }).ToList()
    };
}

public class OptionEdit(string key)
{
    public string Key { get; set; } = key;
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
}

public record QuestionRow(int Id, Section Section, string Domain, string Skill, int SkillId, Difficulty Difficulty, QuestionType Type, QuestionStatus Status, string Preview, int AttemptCount, int CorrectCount, DateTime UpdatedAt, string? Tag);

public class ContentService(IDbContextFactory<AppDbContext> factory, AuditService audit)
{
    // ---------------------------------------------------------------- taxonomy

    public async Task<List<Domain>> DomainsAsync(Section? section = null, bool includeInactive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Domains.AsNoTracking().Include(d => d.Skills.OrderBy(s => s.SortOrder)).AsQueryable();
        if (section is not null) q = q.Where(d => d.Section == section);
        if (!includeInactive) q = q.Where(d => d.IsActive);
        var list = await q.OrderBy(d => d.Section).ThenBy(d => d.SortOrder).ToListAsync();
        if (!includeInactive) foreach (var d in list) d.Skills = d.Skills.Where(s => s.IsActive).ToList();
        return list;
    }

    public async Task<List<Skill>> SkillsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Skills.AsNoTracking().Include(s => s.Domain).Where(s => s.IsActive).OrderBy(s => s.Domain.Section).ThenBy(s => s.Domain.SortOrder).ThenBy(s => s.SortOrder).ToListAsync();
    }

    public async Task<string?> SaveDomainAsync(Domain d)
    {
        if (string.IsNullOrWhiteSpace(d.Name)) return "Nom kiriting.";
        await using var db = await factory.CreateDbContextAsync();
        d.Code = string.IsNullOrWhiteSpace(d.Code) ? Slug(d.Name) : Slug(d.Code);
        if (await db.Domains.AnyAsync(x => x.Code == d.Code && x.Id != d.Id)) return "Bu kod band.";
        if (d.Id == 0) db.Domains.Add(d);
        else
        {
            var e = await db.Domains.FindAsync(d.Id);
            if (e is null) return "Topilmadi.";
            e.Name = d.Name; e.Code = d.Code; e.Section = d.Section; e.SortOrder = d.SortOrder; e.IsActive = d.IsActive;
        }
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> SaveSkillAsync(Skill s)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) return "Nom kiriting.";
        await using var db = await factory.CreateDbContextAsync();
        s.Code = string.IsNullOrWhiteSpace(s.Code) ? Slug(s.Name) : Slug(s.Code);
        if (await db.Skills.AnyAsync(x => x.Code == s.Code && x.Id != s.Id)) return "Bu kod band.";
        if (s.Id == 0) db.Skills.Add(s);
        else
        {
            var e = await db.Skills.FindAsync(s.Id);
            if (e is null) return "Topilmadi.";
            e.Name = s.Name; e.Code = s.Code; e.DomainId = s.DomainId; e.SortOrder = s.SortOrder; e.IsActive = s.IsActive;
        }
        await db.SaveChangesAsync();
        return null;
    }

    public static string Slug(string s) => Regex.Replace(s.Trim().ToUpperInvariant().Replace('\'', ' '), "[^A-Z0-9]+", "_").Trim('_');

    // ---------------------------------------------------------------- questions

    public async Task<(List<QuestionRow> Rows, int Total)> SearchAsync(QuestionFilter f)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Questions.AsNoTracking().AsQueryable();
        if (f.Section is not null) q = q.Where(x => x.Section == f.Section);
        if (f.DomainId is not null) q = q.Where(x => x.DomainId == f.DomainId);
        if (f.SkillId is not null) q = q.Where(x => x.SkillId == f.SkillId);
        if (f.Difficulty is not null) q = q.Where(x => x.Difficulty == f.Difficulty);
        if (f.Status is not null) q = q.Where(x => x.Status == f.Status);
        if (f.Type is not null) q = q.Where(x => x.Type == f.Type);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            if (int.TryParse(s, out var id)) q = q.Where(x => x.Id == id);
            else q = q.Where(x => EF.Functions.ILike(x.Text, $"%{s}%") || (x.Passage != null && EF.Functions.ILike(x.Passage, $"%{s}%")) || (x.Tag != null && EF.Functions.ILike(x.Tag, $"%{s}%")));
        }
        if (f.ExcludeModuleId is not null) q = q.Where(x => !db.TestQuestions.Any(t => t.TestModuleId == f.ExcludeModuleId && t.QuestionId == x.Id));

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.UpdatedAt).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)
            .Select(x => new QuestionRow(x.Id, x.Section, x.Domain.Name, x.Skill.Name, x.SkillId, x.Difficulty, x.Type, x.Status, x.Text, x.AttemptCount, x.CorrectCount, x.UpdatedAt, x.Tag))
            .ToListAsync();
        return (rows.Select(r => r with { Preview = Preview(r.Preview) }).ToList(), total);
    }

    public static string StripHtml(string? html) => string.IsNullOrEmpty(html) ? "" : Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();
    public static string Preview(string? html, int max = 110) { var t = StripHtml(html); return t.Length > max ? t[..max] + "…" : t; }

    public async Task<Question?> GetAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Questions.AsNoTracking().Include(q => q.Options.OrderBy(o => o.SortOrder)).Include(q => q.Domain).Include(q => q.Skill).FirstOrDefaultAsync(q => q.Id == id);
    }

    public async Task<QuestionVersion?> GetVersionAsync(int versionId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.QuestionVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId);
    }

    public static List<string> Validate(QuestionEdit e)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(StripHtml(e.Text)) && !(e.Text?.Contains("<img") ?? false)) errors.Add("Savol matni bo'sh.");
        if (e.DomainId == 0) errors.Add("Domain tanlanmagan.");
        if (e.SkillId == 0) errors.Add("Skill tanlanmagan.");
        if (e.Type == QuestionType.MultipleChoice)
        {
            var filled = e.Options.Where(o => !string.IsNullOrWhiteSpace(StripHtml(o.Text)) || (o.Text?.Contains("<img") ?? false)).ToList();
            if (filled.Count < 2) errors.Add("Kamida 2 ta variant kiriting.");
            if (e.Options.Count(o => o.IsCorrect) != 1) errors.Add("Aynan bitta to'g'ri variant belgilang.");
            if (e.Options.Any(o => o.IsCorrect && string.IsNullOrWhiteSpace(StripHtml(o.Text)) && !(o.Text?.Contains("<img") ?? false))) errors.Add("To'g'ri variant matni bo'sh.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(e.CorrectAnswer)) errors.Add("SPR uchun to'g'ri javob kiriting.");
            else foreach (var a in e.CorrectAnswer.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (ScoringService.NormalizeSpr(a) is null || ScoringService.ParseNumber(ScoringService.NormalizeSpr(a)!) is null) errors.Add($"To'g'ri javob '{a}' son emas.");
        }
        if (e.Status == QuestionStatus.Active && string.IsNullOrWhiteSpace(StripHtml(e.Explanation))) errors.Add("Faol savolda izoh (explanation) bo'lishi kerak.");
        return errors;
    }

    public async Task<(int Id, List<string> Errors)> SaveAsync(QuestionEdit e, int userId, bool enforceStatusRules = true)
    {
        var errors = Validate(e);
        if (enforceStatusRules && e.Status == QuestionStatus.Active && errors.Count > 0) return (e.Id, errors);
        if (errors.Any(x => x.Contains("Domain") || x.Contains("Skill"))) return (e.Id, errors);

        await using var db = await factory.CreateDbContextAsync();
        var skill = await db.Skills.Include(s => s.Domain).FirstOrDefaultAsync(s => s.Id == e.SkillId);
        if (skill is null) return (e.Id, ["Skill topilmadi."]);
        e.DomainId = skill.DomainId; e.Section = skill.Domain.Section;

        Question q;
        var isNew = e.Id == 0;
        if (isNew) { q = new Question { CreatedByUserId = userId }; db.Questions.Add(q); }
        else
        {
            q = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == e.Id) ?? throw new AppException("Savol topilmadi.");
        }

        var before = isNew ? null : Fingerprint(q);
        q.Section = e.Section; q.DomainId = e.DomainId; q.SkillId = e.SkillId; q.Difficulty = e.Difficulty; q.Type = e.Type; q.Status = e.Status;
        q.Passage = Clean(e.Passage); q.Text = Clean(e.Text) ?? ""; q.Explanation = Clean(e.Explanation); q.VideoUrl = e.VideoUrl?.Trim();
        q.CorrectAnswer = e.Type == QuestionType.SPR ? string.Join("|", (e.CorrectAnswer ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) : null;
        q.Tag = string.IsNullOrWhiteSpace(e.Tag) ? null : e.Tag.Trim();
        q.UpdatedAt = DateTime.UtcNow;

        if (e.Type == QuestionType.MultipleChoice)
        {
            var wanted = e.Options.Select((o, i) => new { o, i }).ToList();
            foreach (var old in q.Options.Where(o => wanted.All(w => w.o.Key != o.Key)).ToList()) db.QuestionOptions.Remove(old);
            foreach (var w in wanted)
            {
                var opt = q.Options.FirstOrDefault(o => o.Key == w.o.Key);
                if (opt is null) { opt = new QuestionOption { Key = w.o.Key }; q.Options.Add(opt); }
                opt.Text = Clean(w.o.Text) ?? ""; opt.IsCorrect = w.o.IsCorrect; opt.SortOrder = w.i;
            }
        }
        else foreach (var old in q.Options.ToList()) db.QuestionOptions.Remove(old);

        await db.SaveChangesAsync();

        if (isNew || before != Fingerprint(q)) await EnsureVersionAsync(db, q, isNew);
        await audit.LogAsync(userId, isNew ? "QuestionCreated" : "QuestionUpdated", "Question", q.Id, $"v{q.CurrentVersion} {q.Status}");
        return (q.Id, errors);
    }

    private static string? Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var t = html.Trim();
        if (t is "<p><br></p>" or "<p></p>" or "<br>") return null;
        // strip script/event handlers (content is authored by staff, but keep the student page safe)
        t = Regex.Replace(t, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", "", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"javascript\s*:", "", RegexOptions.IgnoreCase);
        return t;
    }

    private static string Fingerprint(Question q) =>
        JsonSerializer.Serialize(new { q.Type, q.Passage, q.Text, q.Explanation, q.VideoUrl, q.CorrectAnswer, Options = q.Options.OrderBy(o => o.SortOrder).Select(o => new { o.Key, o.Text, o.IsCorrect }) });

    /// <summary>Creates a new frozen version when the content changed (attempts keep pointing at the old one).</summary>
    private static async Task<QuestionVersion> EnsureVersionAsync(AppDbContext db, Question q, bool isNew)
    {
        var latest = await db.QuestionVersions.Where(v => v.QuestionId == q.Id).OrderByDescending(v => v.Version).FirstOrDefaultAsync();
        var optionsJson = JsonSerializer.Serialize(q.Options.OrderBy(o => o.SortOrder).Select(o => new OptionSnapshot(o.Key, o.Text, o.IsCorrect)));
        if (latest is not null && latest.Type == q.Type && latest.Passage == q.Passage && latest.Text == q.Text && latest.Explanation == q.Explanation && latest.VideoUrl == q.VideoUrl && latest.CorrectAnswer == q.CorrectAnswer && latest.OptionsJson == optionsJson)
            return latest;
        var referenced = latest is not null && (await db.AttemptQuestions.AnyAsync(a => a.QuestionVersionId == latest.Id) || await db.PracticeQuestions.AnyAsync(p => p.QuestionVersionId == latest.Id));
        QuestionVersion v;
        if (latest is not null && !referenced)
        {
            // nobody used the previous snapshot: just refresh it
            v = latest;
        }
        else
        {
            v = new QuestionVersion { QuestionId = q.Id, Version = (latest?.Version ?? 0) + 1 };
            db.QuestionVersions.Add(v);
            q.CurrentVersion = v.Version;
        }
        v.Type = q.Type; v.Passage = q.Passage; v.Text = q.Text; v.Explanation = q.Explanation; v.VideoUrl = q.VideoUrl; v.CorrectAnswer = q.CorrectAnswer; v.OptionsJson = optionsJson; v.CreatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return v;
    }

    /// <summary>Current version id for the question (creating the snapshot if the question has none yet).</summary>
    public static async Task<int> CurrentVersionIdAsync(AppDbContext db, int questionId)
    {
        var id = await db.QuestionVersions.Where(v => v.QuestionId == questionId).OrderByDescending(v => v.Version).Select(v => (int?)v.Id).FirstOrDefaultAsync();
        if (id is not null) return id.Value;
        var q = await db.Questions.Include(x => x.Options).FirstAsync(x => x.Id == questionId);
        return (await EnsureVersionAsync(db, q, true)).Id;
    }

    public async Task<string?> SetStatusAsync(int id, QuestionStatus status, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = await db.Questions.Include(x => x.Options).FirstOrDefaultAsync(x => x.Id == id);
        if (q is null) return "Savol topilmadi.";
        if (status == QuestionStatus.Active)
        {
            var errors = Validate(QuestionEdit.From(q));
            if (errors.Count > 0) return string.Join(" ", errors);
        }
        q.Status = status; q.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, status == QuestionStatus.Archived ? "QuestionArchived" : "QuestionStatusChanged", "Question", id, status.ToString());
        return null;
    }

    public async Task<string?> DeleteAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.AttemptQuestions.AnyAsync(a => a.QuestionId == id) || await db.PracticeQuestions.AnyAsync(p => p.QuestionId == id) || await db.TestQuestions.AnyAsync(t => t.QuestionId == id))
            return "Bu savol testlarda yoki urinishlarda ishlatilgan — o'chirib bo'lmaydi, arxivlang.";
        var q = await db.Questions.FindAsync(id);
        if (q is null) return "Savol topilmadi.";
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "QuestionDeleted", "Question", id);
        return null;
    }

    public async Task<List<QuestionRow>> FindDuplicatesAsync(string text, int excludeId)
    {
        var plain = StripHtml(text);
        if (plain.Length < 20) return [];
        var probe = plain.Length > 60 ? plain[..60] : plain;
        await using var db = await factory.CreateDbContextAsync();
        return await db.Questions.AsNoTracking().Where(q => q.Id != excludeId && EF.Functions.ILike(q.Text, $"%{probe}%"))
            .Take(5).Select(x => new QuestionRow(x.Id, x.Section, x.Domain.Name, x.Skill.Name, x.SkillId, x.Difficulty, x.Type, x.Status, x.Text, x.AttemptCount, x.CorrectCount, x.UpdatedAt, x.Tag)).ToListAsync();
    }

    public async Task<Dictionary<string, int>> CountsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var byStatus = await db.Questions.GroupBy(q => q.Status).Select(g => new { g.Key, C = g.Count() }).ToListAsync();
        var d = byStatus.ToDictionary(x => x.Key.ToString(), x => x.C);
        d["Total"] = byStatus.Sum(x => x.C);
        return d;
    }

    /// <summary>Active question counts per skill — used by the auto generator and the admin dashboard.</summary>
    public async Task<List<(int SkillId, string Skill, string Domain, Section Section, int Easy, int Medium, int Hard)>> CoverageAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Questions.Where(q => q.Status == QuestionStatus.Active).GroupBy(q => new { q.SkillId, q.Difficulty }).Select(g => new { g.Key.SkillId, g.Key.Difficulty, C = g.Count() }).ToListAsync();
        var skills = await db.Skills.Include(s => s.Domain).OrderBy(s => s.Domain.Section).ThenBy(s => s.Domain.SortOrder).ThenBy(s => s.SortOrder).ToListAsync();
        return skills.Select(s => (s.Id, s.Name, s.Domain.Name, s.Domain.Section,
            rows.Where(r => r.SkillId == s.Id && r.Difficulty == Difficulty.Easy).Sum(r => r.C),
            rows.Where(r => r.SkillId == s.Id && r.Difficulty == Difficulty.Medium).Sum(r => r.C),
            rows.Where(r => r.SkillId == s.Id && r.Difficulty == Difficulty.Hard).Sum(r => r.C))).ToList();
    }

    // ---------------------------------------------------------------- bulk import

    public record ImportItem(string? Section, string? Domain, string? Skill, string? Difficulty, string? Type, string? Passage, string Text, string? Explanation,
        string? VideoUrl, string? CorrectAnswer, string? Tag, List<ImportOption>? Options, string? Status);
    public record ImportOption(string Key, string Text, bool IsCorrect);

    /// <summary>Imports a JSON array of questions. Skills are matched by code or name; missing ones are reported.</summary>
    public async Task<(int Imported, List<string> Errors)> ImportJsonAsync(string json, int userId)
    {
        List<ImportItem>? items;
        try { items = JsonSerializer.Deserialize<List<ImportItem>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (Exception ex) { return (0, [$"JSON o'qilmadi: {ex.Message}"]); }
        if (items is null || items.Count == 0) return (0, ["Fayl bo'sh."]);

        var skills = await SkillsAsync();
        var errors = new List<string>();
        var imported = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var skill = skills.FirstOrDefault(s => s.Code.Equals(it.Skill, StringComparison.OrdinalIgnoreCase) || s.Name.Equals(it.Skill, StringComparison.OrdinalIgnoreCase));
            if (skill is null) { errors.Add($"#{i + 1}: skill topilmadi: {it.Skill}"); continue; }
            var edit = new QuestionEdit
            {
                SkillId = skill.Id, DomainId = skill.DomainId, Section = skill.Domain.Section,
                Difficulty = Enum.TryParse<Difficulty>(it.Difficulty, true, out var d) ? d : Difficulty.Medium,
                Type = (it.Type ?? "").Contains("spr", StringComparison.OrdinalIgnoreCase) || (it.Options is null or { Count: 0 }) ? QuestionType.SPR : QuestionType.MultipleChoice,
                Passage = it.Passage, Text = it.Text, Explanation = it.Explanation, VideoUrl = it.VideoUrl, CorrectAnswer = it.CorrectAnswer, Tag = it.Tag,
                Status = Enum.TryParse<QuestionStatus>(it.Status, true, out var st) ? st : QuestionStatus.Active,
                Options = it.Options?.Select(o => new OptionEdit(o.Key.ToUpperInvariant()) { Text = o.Text, IsCorrect = o.IsCorrect }).ToList() ?? []
            };
            var (_, errs) = await SaveAsync(edit, userId);
            if (errs.Count > 0 && edit.Status == QuestionStatus.Active) { errors.Add($"#{i + 1}: {string.Join(" ", errs)}"); continue; }
            imported++;
        }
        return (imported, errors);
    }

    public async Task<string> ExportJsonAsync(QuestionFilter f)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Questions.AsNoTracking().Include(x => x.Options).Include(x => x.Skill).AsQueryable();
        if (f.Section is not null) q = q.Where(x => x.Section == f.Section);
        if (f.SkillId is not null) q = q.Where(x => x.SkillId == f.SkillId);
        if (f.Status is not null) q = q.Where(x => x.Status == f.Status);
        var list = await q.OrderBy(x => x.Id).ToListAsync();
        var items = list.Select(x => new ImportItem(x.Section.ToString(), null, x.Skill.Code, x.Difficulty.ToString(), x.Type.ToString(), x.Passage, x.Text, x.Explanation, x.VideoUrl, x.CorrectAnswer, x.Tag,
            x.Options.OrderBy(o => o.SortOrder).Select(o => new ImportOption(o.Key, o.Text, o.IsCorrect)).ToList(), x.Status.ToString()));
        return JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }
}

/// <summary>Uploaded files (question images, materials, homework files) stored in PostgreSQL — the free host has no persistent disk.</summary>
public class MediaService(IDbContextFactory<AppDbContext> factory)
{
    public const string RequestPath = "/media";
    public const long MaxSize = 25 * 1024 * 1024;
    private static readonly HashSet<string> ImageTypes = ["image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml"];
    private static readonly HashSet<string> DocTypes = ["application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "text/plain", "application/zip", "video/mp4", "audio/mpeg"];

    public static bool IsAllowed(string contentType, bool imagesOnly) => imagesOnly ? ImageTypes.Contains(contentType) : ImageTypes.Contains(contentType) || DocTypes.Contains(contentType);

    public async Task<(string? Url, int? Id, string? Error)> UploadAsync(Stream stream, string originalName, string contentType, long size, int userId, bool imagesOnly)
    {
        if (size <= 0) return (null, null, "Fayl bo'sh.");
        if (size > MaxSize) return (null, null, "Fayl 25 MB dan katta.");
        contentType = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (!IsAllowed(contentType, imagesOnly)) return (null, null, imagesOnly ? "Faqat rasm (PNG, JPG, GIF, WebP, SVG) yuklash mumkin." : "Bu fayl turi qo'llab-quvvatlanmaydi.");
        var ext = Path.GetExtension(originalName);
        if (ext.Length > 8) ext = "";
        var name = Ids.Token(12) + ext.ToLowerInvariant();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        await using var db = await factory.CreateDbContextAsync();
        var file = new MediaFile { Name = name, OriginalName = Path.GetFileName(originalName), ContentType = contentType, Data = ms.ToArray(), Size = ms.Length, UploadedByUserId = userId };
        db.MediaFiles.Add(file);
        await db.SaveChangesAsync();
        return ($"{RequestPath}/{name}", file.Id, null);
    }

    public async Task<MediaFile?> GetAsync(string name)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Name == name);
    }

    public async Task<MediaFile?> GetByIdAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.MediaFiles.AsNoTracking().Select(f => new MediaFile { Id = f.Id, Name = f.Name, OriginalName = f.OriginalName, ContentType = f.ContentType, Size = f.Size }).FirstOrDefaultAsync(f => f.Id == id);
    }
}
