using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services.MentorBot;

/// <summary>
/// PDF library inside the bot. Everyone sees it (materials without a group); only super admins and users a super admin
/// granted <see cref="User.CanManageBotPdfs"/> may add, rename, replace, re-categorise or delete PDFs.
/// </summary>
public partial class MentorBotService
{
    private static readonly MaterialCategory[] PdfCategories = Enum.GetValues<MaterialCategory>();

    /// <summary>Checked against the database (not the bot view), so a bot role never grants or removes it.</summary>
    private async Task<bool> CanManagePdfAsync(Ctx c) =>
        c.User is not null && await c.Db.Users.AnyAsync(u => u.Id == c.User.Id && u.IsActive && (u.Role == UserRole.SuperAdmin || u.CanManageBotPdfs), c.Ct);

    private static object PdfEditKeyboard(int id) => Inline(
        [new B("✏️ Nomi", $"pdfr:{id}"), new B("📎 Faylni almashtirish", $"pdff:{id}")],
        [new B("🏷 Kategoriya", $"pdfc:{id}"), new B("🗑 O'chirish", $"pdfd:{id}")],
        [new B("📣 Hammaga e'lon qilish", $"pdfann:{id}")]);

    private static object CategoryKeyboard(string prefix) => Column(PdfCategories.Select(cat => new B(Labels.Material(cat), $"{prefix}:{(int)cat}")));

    private static bool IsPdf(BotUpdate u) => u.MediaKind == "document" &&
        (string.Equals(u.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase) || (u.FileName ?? "").EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

    private async Task PdfCallbackAsync(Ctx c, string[] p)
    {
        if (!await CanManagePdfAsync(c)) { await Say(c, "⛔ PDF qo'shish va o'zgartirish faqat super admin va u ruxsat bergan foydalanuvchilar uchun."); return; }
        var id = p.Length > 1 && int.TryParse(p[1], out var v) ? v : 0;
        var m = id > 0 && p[0] is not "pdfadd" and not "pdfcat" ? await c.Db.Materials.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, c.Ct) : null;
        if (p[0] is not ("pdfadd" or "pdfcat") && m is null) { await Show(c, "Material topilmadi."); return; }
        switch (p[0])
        {
            case "pdfadd":
                await c.StepAsync("pdf_file", true);
                await Say(c, "📄 <b>PDF faylni yuboring</b> (20 MB gacha).\nBekor qilish: /cancel");
                return;
            case "pdfcat" when c.Chat.Step == "pdf_cat":
                await PdfCreateAsync(c, (MaterialCategory)id);
                return;
            case "pdfc":
                await c.StepAsync("pdf_editcat", true);
                c.Set("id", id.ToString()); await c.SaveAsync();
                await Show(c, $"🏷 <b>{H(m!.Title)}</b> — yangi kategoriya:", CategoryKeyboard("pdfcat"));
                return;
            case "pdfcat" when c.Chat.Step == "pdf_editcat":
                var mid = c.GetInt("id");
                var e = await c.Db.Materials.FirstOrDefaultAsync(x => x.Id == mid, c.Ct);
                await c.ResetAsync();
                if (e is null) return;
                e.Category = (MaterialCategory)id;
                await c.Db.SaveChangesAsync(c.Ct);
                await audit.LogAsync(c.User!.Id, "MaterialSaved", "Material", e.Id, $"kategoriya: {e.Category} (bot)");
                await Show(c, $"✅ Kategoriya: <b>{Labels.Material(e.Category)}</b>", PdfEditKeyboard(e.Id));
                return;
            case "pdfr":
                await c.StepAsync("pdf_rename", true);
                c.Set("id", id.ToString()); await c.SaveAsync();
                await Say(c, $"✏️ <b>{H(m!.Title)}</b> — yangi nomini yozing:");
                return;
            case "pdff":
                await c.StepAsync("pdf_replace", true);
                c.Set("id", id.ToString()); await c.SaveAsync();
                await Say(c, $"📎 <b>{H(m!.Title)}</b> — yangi PDF faylni yuboring:");
                return;
            case "pdfd":
                await Show(c, $"🗑 <b>{H(m!.Title)}</b> o'chirilsinmi? Bu qaytarib bo'lmaydi.", Inline([new B("Ha, o'chirish", $"pdfdd:{id}"), new B("Yo'q", "cancel")]));
                return;
            case "pdfdd":
                await materials.DeleteAsync(id, c.User!.Id);
                await Show(c, $"🗑 O'chirildi: <b>{H(m!.Title)}</b>");
                return;
            case "pdfann":
                var b = new MentorBroadcast { Target = BroadcastTarget.Everyone, Text = $"📚 Kutubxonaga yangi material qo'shildi: «{m!.Title}»\n«📚 Materiallar» bo'limidan oching.", CreatedByUserId = c.User!.Id };
                c.Db.MentorBroadcasts.Add(b);
                await c.Db.SaveChangesAsync(c.Ct);
                var n = await sender.BroadcastAsync(b.Id, c.Ct);
                await Say(c, $"📣 E'lon yuborildi: {n} ta foydalanuvchiga.");
                return;
        }
    }

    private async Task<bool> PdfStepAsync(Ctx c)
    {
        if (!await CanManagePdfAsync(c)) { await c.ResetAsync(); return false; }
        switch (c.Chat.Step)
        {
            case "pdf_file":
            case "pdf_replace":
                if (!IsPdf(c.U)) { await Say(c, "Iltimos, <b>PDF</b> fayl yuboring (hujjat sifatida). Bekor qilish: /cancel"); return true; }
                var (fid, err) = await SaveTelegramFileAsync(c, "application/pdf");
                if (err is not null) { await Say(c, "❌ " + H(err)); return true; }
                if (c.Chat.Step == "pdf_replace")
                {
                    var id = c.GetInt("id");
                    var m = await c.Db.Materials.FirstOrDefaultAsync(x => x.Id == id, c.Ct);
                    await c.ResetAsync();
                    if (m is null) return true;
                    var old = m.FileId;
                    m.FileId = fid;
                    await c.Db.SaveChangesAsync(c.Ct);
                    if (old is int o && o != fid) await c.Db.MediaFiles.Where(f => f.Id == o).ExecuteDeleteAsync(c.Ct);
                    await audit.LogAsync(c.User!.Id, "MaterialSaved", "Material", m.Id, "fayl almashtirildi (bot)");
                    await Say(c, $"✅ Fayl almashtirildi: <b>{H(m.Title)}</b>", PdfEditKeyboard(m.Id));
                    return true;
                }
                c.Set("file", fid?.ToString());
                c.Set("fname", Path.GetFileNameWithoutExtension(c.U.FileName ?? "Material"));
                if (!string.IsNullOrWhiteSpace(c.U.Text)) c.Set("desc", c.U.Text.Trim());
                await c.StepAsync("pdf_title");
                await Say(c, $"✍️ Material nomini yozing.\nFayl nomi qolsin desangiz: <code>-</code> (<i>{H(c.Get("fname"))}</i>)");
                return true;
            case "pdf_title":
                var title = c.Text == "-" ? c.Get("fname") ?? "Material" : c.Text;
                if (title.Length is < 2 or > 160) { await Say(c, "Nom 2–160 belgi bo'lsin."); return true; }
                c.Set("title", title);
                await c.StepAsync("pdf_cat");
                await Say(c, "🏷 Kategoriyani tanlang:", CategoryKeyboard("pdfcat"));
                return true;
            case "pdf_rename":
                if (c.Text.Length is < 2 or > 160) { await Say(c, "Nom 2–160 belgi bo'lsin."); return true; }
                var rid = c.GetInt("id");
                var r = await c.Db.Materials.FirstOrDefaultAsync(x => x.Id == rid, c.Ct);
                await c.ResetAsync();
                if (r is null) return true;
                r.Title = c.Text;
                await c.Db.SaveChangesAsync(c.Ct);
                await audit.LogAsync(c.User!.Id, "MaterialSaved", "Material", r.Id, $"nom: {r.Title} (bot)");
                await Say(c, $"✅ Yangi nom: <b>{H(r.Title)}</b>", PdfEditKeyboard(r.Id));
                return true;
        }
        return false;
    }

    private async Task PdfCreateAsync(Ctx c, MaterialCategory category)
    {
        // no group → the PDF is visible to everyone in the bot (and on the site's materials pages)
        var m = new Material { Title = c.Get("title") ?? "Material", Description = c.Get("desc"), Category = category, FileId = int.TryParse(c.Get("file"), out var f) ? f : null };
        var (id, err) = await materials.SaveAsync(m, c.User!.Id);
        await c.ResetAsync();
        if (err is not null) { await Show(c, "❌ " + H(err)); return; }
        await Show(c, $"✅ Kutubxonaga qo'shildi: <b>{H(m.Title)}</b> ({Labels.Material(category)})\nHamma «📚 Materiallar» bo'limida ko'radi.", PdfEditKeyboard(id));
    }
}
