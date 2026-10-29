using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record BalanceRow(int StudentId, string Name, string Phone, StudentStatus Status, string Groups, decimal Balance, DateOnly? NextPaymentDate);

/// <summary>Leads, student balances/payments and staff tasks.</summary>
public class CrmService(IDbContextFactory<AppDbContext> factory, AuditService audit)
{
    // ---------------------------------------------------------------- leads

    public async Task<List<Lead>> LeadsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Leads.AsNoTracking().Include(l => l.ModeratorUser).OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    public async Task<string?> SaveLeadAsync(Lead l, int userId)
    {
        if (string.IsNullOrWhiteSpace(l.FullName)) return "Ism kiritilishi shart";
        var phone = PhoneFormat.Normalize(l.Phone);
        if (!PhoneFormat.IsValid(phone)) return "Telefon raqami noto'g'ri";
        await using var db = await factory.CreateDbContextAsync();
        if (l.Id == 0)
        {
            l.Phone = phone; l.FullName = l.FullName.Trim(); l.ModeratorUserId ??= userId;
            db.Leads.Add(l);
        }
        else
        {
            var e = await db.Leads.FindAsync(l.Id); if (e is null) return "Lid topilmadi";
            e.FullName = l.FullName.Trim(); e.Phone = phone; e.Source = l.Source; e.Course = l.Course; e.Status = l.Status; e.ModeratorUserId = l.ModeratorUserId; e.Notes = l.Notes;
        }
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "LeadSaved", "Lead", l.Id, l.FullName);
        return null;
    }

    public async Task SetLeadStatusAsync(int id, LeadStatus status, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Leads.Where(l => l.Id == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.Status, status));
        await audit.LogAsync(userId, "LeadStatus", "Lead", id, status.ToString());
    }

    public async Task DeleteLeadAsync(int id, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Leads.Where(l => l.Id == id).ExecuteDeleteAsync();
        await audit.LogAsync(userId, "LeadDeleted", "Lead", id, null);
    }

    // ---------------------------------------------------------------- balances / payments

    public async Task<List<BalanceRow>> BalancesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var sums = await db.Payments.GroupBy(p => p.StudentId).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        var students = await db.Students.AsNoTracking().Include(s => s.User)
            .Include(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)).ThenInclude(e => e.Group).ToListAsync();
        return students.Select(s => new BalanceRow(s.Id, s.User.FullName, s.User.Phone, s.Status,
            string.Join(", ", s.Enrollments.Select(e => e.Group.Name)), sums.GetValueOrDefault(s.Id), s.NextPaymentDate)).ToList();
    }

    public async Task<List<Payment>> PaymentsAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Payments.AsNoTracking().Where(p => p.StudentId == studentId).OrderByDescending(p => p.Date).ThenByDescending(p => p.Id).Take(50).ToListAsync();
    }

    public async Task<string?> AddPaymentAsync(int studentId, decimal amount, PaymentMethod method, DateOnly date, string? note, DateOnly? nextPayment, int userId)
    {
        if (amount == 0) return "Summa 0 bo'lishi mumkin emas";
        await using var db = await factory.CreateDbContextAsync();
        var s = await db.Students.FindAsync(studentId); if (s is null) return "O'quvchi topilmadi";
        // a charge is stored as a negative amount
        var signed = method == PaymentMethod.Charge ? -Math.Abs(amount) : Math.Abs(amount);
        db.Payments.Add(new Payment { StudentId = studentId, Amount = signed, Method = method, Date = date, Note = note, CreatedByUserId = userId });
        if (nextPayment is not null) s.NextPaymentDate = nextPayment;
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "PaymentAdded", "Student", studentId, $"{signed} {method}");
        return null;
    }

    // ---------------------------------------------------------------- staff tasks

    public async Task<List<StaffTask>> TasksAsync(int userId, bool all)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.StaffTasks.AsNoTracking().Include(t => t.AssigneeUser).AsQueryable();
        if (!all) q = q.Where(t => t.AssigneeUserId == userId || t.CreatedByUserId == userId);
        return await q.OrderBy(t => t.Status).ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenByDescending(t => t.Id).ToListAsync();
    }

    public async Task<string?> SaveTaskAsync(StaffTask t, int userId)
    {
        if (string.IsNullOrWhiteSpace(t.Title)) return "Sarlavha kiritilishi shart";
        await using var db = await factory.CreateDbContextAsync();
        if (t.Id == 0) { t.CreatedByUserId = userId; t.Title = t.Title.Trim(); db.StaffTasks.Add(t); }
        else
        {
            var e = await db.StaffTasks.FindAsync(t.Id); if (e is null) return "Topshiriq topilmadi";
            e.Title = t.Title.Trim(); e.Description = t.Description; e.AssigneeUserId = t.AssigneeUserId; e.DueDate = t.DueDate; e.Status = t.Status;
        }
        await db.SaveChangesAsync();
        return null;
    }

    public async Task SetTaskDoneAsync(int id, bool done)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.StaffTasks.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, done ? StaffTaskStatus.Done : StaffTaskStatus.Open));
    }

    public async Task DeleteTaskAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.StaffTasks.Where(t => t.Id == id).ExecuteDeleteAsync();
    }
}
