using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record UserRow(int Id, string FullName, string Phone, UserRole Role, bool IsActive, bool Telegram, DateTime CreatedAt, DateTime? LastSeenAt, int GroupCount, bool CanManageQuestions, string? Email);

/// <summary>Students, teachers, groups, enrollments, attendance and the "who may see whom" rules.</summary>
public class AcademyService(IDbContextFactory<AppDbContext> factory, AuditService audit, NotificationService notifications)
{
    // ---------------------------------------------------------------- scope / authorization

    /// <summary>Group ids a staff user may work with (admins: all; teachers/assistants: their own).</summary>
    public async Task<List<int>> GroupScopeAsync(int userId, UserRole role)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Groups.Where(g => g.IsActive);
        if (role is UserRole.Teacher or UserRole.Assistant) q = q.Where(g => g.TeacherUserId == userId || g.AssistantUserId == userId);
        return await q.Select(g => g.Id).ToListAsync();
    }

    public async Task<List<int>> StudentScopeAsync(int userId, UserRole role)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (role is UserRole.Admin or UserRole.SuperAdmin) return await db.Students.Select(s => s.Id).ToListAsync();
        if (role == UserRole.Parent) return await db.ParentStudents.Where(p => p.ParentUserId == userId).Select(p => p.StudentId).ToListAsync();
        if (role == UserRole.Student) return await db.Students.Where(s => s.UserId == userId).Select(s => s.Id).ToListAsync();
        return await db.Enrollments.Where(e => e.Status == EnrollmentStatus.Active && (e.Group.TeacherUserId == userId || e.Group.AssistantUserId == userId)).Select(e => e.StudentId).Distinct().ToListAsync();
    }

    public async Task<bool> CanSeeStudentAsync(int userId, UserRole role, int studentId) => (await StudentScopeAsync(userId, role)).Contains(studentId);
    public async Task<bool> CanSeeGroupAsync(int userId, UserRole role, int groupId) => (await GroupScopeAsync(userId, role)).Contains(groupId);

    // ---------------------------------------------------------------- users

    public async Task<List<UserRow>> UsersAsync(UserRole? role = null, string? search = null, bool includeInactive = true)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Users.AsNoTracking().AsQueryable();
        if (role is not null) q = q.Where(u => u.Role == role);
        if (!includeInactive) q = q.Where(u => u.IsActive);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(u => EF.Functions.ILike(u.FullName, $"%{s}%") || u.Phone.Contains(s)); }
        return await q.OrderBy(u => u.Role).ThenBy(u => u.FullName)
            .Select(u => new UserRow(u.Id, u.FullName, u.Phone, u.Role, u.IsActive, u.TelegramChatId != null, u.CreatedAt, u.LastSeenAt,
                db.Groups.Count(g => g.IsActive && (g.TeacherUserId == u.Id || g.AssistantUserId == u.Id)), u.CanManageQuestions, u.Email)).ToListAsync();
    }

    public async Task<User?> UserAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().Include(u => u.Student).FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>Admin creates any user (staff, student, parent). Returns the new user id.</summary>
    public async Task<(int Id, string? Error)> CreateUserAsync(string fullName, string phone, UserRole role, string? password, string? email, int? targetScore, int adminUserId, bool canManageQuestions = false)
    {
        if (AuthService.ValidateName(fullName) is string nerr) return (0, nerr);
        var normalized = PhoneFormat.Normalize(phone);
        if (!PhoneFormat.IsValid(normalized)) return (0, PhoneFormat.InvalidMessage);
        if (!string.IsNullOrEmpty(password) && AuthService.ValidatePassword(password) is string perr) return (0, perr);
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Phone == normalized)) return (0, "Bu raqam allaqachon ro'yxatdan o'tgan.");
        var link = await db.TelegramLinks.FindAsync(normalized);
        var user = new User { FullName = fullName.Trim(), Phone = normalized, Role = role, Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(), AvatarColor = AuthService.ColorFor(normalized), TelegramChatId = link?.ChatId, CanManageQuestions = canManageQuestions && role == UserRole.Teacher };
        if (!string.IsNullOrEmpty(password)) user.PasswordHash = AuthService.HashPassword(user, password);
        db.Users.Add(user);
        if (role == UserRole.Student) user.Student = new Student { TargetScore = targetScore, ParentCode = await AuthService.UniqueParentCodeAsync(db) };
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, role == UserRole.Student ? "StudentCreated" : "UserCreated", "User", user.Id, $"{role} {normalized}");
        return (user.Id, null);
    }

    public async Task<string?> UpdateUserAsync(int id, string fullName, string phone, string? email, UserRole role, bool isActive, bool canManageQuestions, string? newPassword, int adminUserId, int? targetScore = null, StudentStatus? studentStatus = null, string? notes = null)
    {
        if (AuthService.ValidateName(fullName) is string nerr) return nerr;
        var normalized = PhoneFormat.Normalize(phone);
        if (!PhoneFormat.IsValid(normalized)) return PhoneFormat.InvalidMessage;
        if (!string.IsNullOrEmpty(newPassword) && AuthService.ValidatePassword(newPassword) is string perr) return perr;
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.Include(u => u.Student).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return "Foydalanuvchi topilmadi.";
        if (await db.Users.AnyAsync(u => u.Phone == normalized && u.Id != id)) return "Bu raqam boshqa foydalanuvchida.";
        if (user.Role == UserRole.SuperAdmin && (role != UserRole.SuperAdmin || !isActive) && await db.Users.CountAsync(u => u.Role == UserRole.SuperAdmin && u.IsActive) <= 1)
            return "Oxirgi super adminni o'zgartirib bo'lmaydi.";
        if (user.Role == UserRole.Student && role != UserRole.Student) return "O'quvchi rolini o'zgartirib bo'lmaydi (natijalar unga bog'langan).";
        if (role == UserRole.Student && user.Role != UserRole.Student) return "Mavjud foydalanuvchini o'quvchiga aylantirib bo'lmaydi; yangi o'quvchi yarating.";
        user.FullName = fullName.Trim(); user.Phone = normalized; user.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(); user.Role = role; user.IsActive = isActive;
        user.CanManageQuestions = canManageQuestions && role == UserRole.Teacher; user.UpdatedAt = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(newPassword)) user.PasswordHash = AuthService.HashPassword(user, newPassword);
        if (user.Student is not null)
        {
            if (targetScore is not null) user.Student.TargetScore = targetScore;
            if (studentStatus is not null) user.Student.Status = studentStatus.Value;
            if (notes is not null) user.Student.Notes = notes;
            if (studentStatus is StudentStatus.Dropped or StudentStatus.Frozen or StudentStatus.Graduated)
            {
                foreach (var e in await db.Enrollments.Where(e => e.StudentId == user.Student.Id && e.Status == EnrollmentStatus.Active).ToListAsync())
                    if (studentStatus is StudentStatus.Dropped or StudentStatus.Graduated) { e.Status = EnrollmentStatus.Ended; e.EndDate = Tz.Today; }
            }
        }
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, "UserUpdated", "User", id, $"{role} active={isActive}" + (studentStatus is null ? "" : $" status={studentStatus}"));
        return null;
    }

    // ---------------------------------------------------------------- students

    public async Task<Student?> StudentAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Students.AsNoTracking().Include(s => s.User).Include(s => s.Enrollments).ThenInclude(e => e.Group).ThenInclude(g => g.TeacherUser)
            .Include(s => s.Parents).ThenInclude(p => p.ParentUser).FirstOrDefaultAsync(s => s.Id == studentId);
    }

    public async Task<List<Student>> StudentsAsync(StudentStatus? status = null, string? search = null, int? groupId = null, IEnumerable<int>? ids = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Students.AsNoTracking().Include(s => s.User).Include(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)).ThenInclude(e => e.Group).AsQueryable();
        if (status is not null) q = q.Where(s => s.Status == status);
        if (groupId is not null) q = q.Where(s => s.Enrollments.Any(e => e.GroupId == groupId && e.Status == EnrollmentStatus.Active));
        if (ids is not null) { var l = ids.ToList(); q = q.Where(s => l.Contains(s.Id)); }
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => EF.Functions.ILike(x.User.FullName, $"%{s}%") || x.User.Phone.Contains(s)); }
        return await q.OrderBy(s => s.User.FullName).ToListAsync();
    }

    public async Task<int?> StudentIdOfUserAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Students.Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync();
    }

    // ---------------------------------------------------------------- groups

    public async Task<List<Group>> GroupsAsync(IEnumerable<int>? ids = null, bool includeInactive = false)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.Groups.AsNoTracking().Include(g => g.TeacherUser).Include(g => g.AssistantUser).Include(g => g.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)).AsQueryable();
        if (!includeInactive) q = q.Where(g => g.IsActive);
        if (ids is not null) { var l = ids.ToList(); q = q.Where(g => l.Contains(g.Id)); }
        return await q.OrderBy(g => g.Name).ToListAsync();
    }

    public async Task<Group?> GroupAsync(int id)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Groups.AsNoTracking().Include(g => g.TeacherUser).Include(g => g.AssistantUser)
            .Include(g => g.Enrollments).ThenInclude(e => e.Student).ThenInclude(s => s.User).FirstOrDefaultAsync(g => g.Id == id);
    }

    public async Task<(int Id, string? Error)> SaveGroupAsync(Group g, int adminUserId)
    {
        if (string.IsNullOrWhiteSpace(g.Name)) return (0, "Guruh nomini kiriting.");
        if (g.StartTime is not null && g.EndTime is not null && g.EndTime <= g.StartTime) return (0, "Tugash vaqti boshlanishdan keyin bo'lishi kerak.");
        await using var db = await factory.CreateDbContextAsync();
        // schedule collisions: same teacher or same room at an overlapping time on a shared day
        if (g.StartTime is not null && g.EndTime is not null && g.ScheduleDays.Length > 0)
        {
            var days = g.ScheduleDays.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            var others = await db.Groups.AsNoTracking().Where(x => x.IsActive && x.Id != g.Id && x.StartTime != null && x.EndTime != null && x.ScheduleDays != "").ToListAsync();
            foreach (var o in others)
            {
                if (!o.ScheduleDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Any(days.Contains)) continue;
                var overlap = g.StartTime < o.EndTime && o.StartTime < g.EndTime;
                if (!overlap) continue;
                if (g.TeacherUserId is not null && g.TeacherUserId == o.TeacherUserId) return (0, $"O'qituvchining bu vaqtda darsi bor: {o.Name}.");
                if (!string.IsNullOrWhiteSpace(g.Room) && string.Equals(g.Room.Trim(), o.Room?.Trim(), StringComparison.OrdinalIgnoreCase)) return (0, $"Xona bu vaqtda band: {o.Name}.");
            }
        }
        if (g.Id == 0) { db.Groups.Add(g); }
        else
        {
            var e = await db.Groups.FindAsync(g.Id);
            if (e is null) return (0, "Guruh topilmadi.");
            e.Name = g.Name; e.CourseType = g.CourseType; e.TeacherUserId = g.TeacherUserId; e.AssistantUserId = g.AssistantUserId; e.Room = g.Room; e.ScheduleDays = g.ScheduleDays; e.StartTime = g.StartTime; e.EndTime = g.EndTime; e.IsActive = g.IsActive;
        }
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, g.Id == 0 ? "GroupCreated" : "GroupUpdated", "Group", g.Id, g.Name);
        return (g.Id, null);
    }

    public async Task<string?> EnrollAsync(int studentId, int groupId, int adminUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Enrollments.AnyAsync(e => e.StudentId == studentId && e.GroupId == groupId && e.Status == EnrollmentStatus.Active)) return "O'quvchi allaqachon shu guruhda.";
        db.Enrollments.Add(new Enrollment { StudentId = studentId, GroupId = groupId, StartDate = Tz.Today });
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, "StudentGroupChanged", "Student", studentId, $"enrolled in group {groupId}");
        return null;
    }

    public async Task UnenrollAsync(int studentId, int groupId, int adminUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        foreach (var e in await db.Enrollments.Where(e => e.StudentId == studentId && e.GroupId == groupId && e.Status == EnrollmentStatus.Active).ToListAsync())
        { e.Status = EnrollmentStatus.Ended; e.EndDate = Tz.Today; }
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, "StudentGroupChanged", "Student", studentId, $"removed from group {groupId}");
    }

    // ---------------------------------------------------------------- attendance

    public async Task<List<Attendance>> AttendanceAsync(int groupId, DateOnly date)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Attendances.AsNoTracking().Where(a => a.GroupId == groupId && a.Date == date).ToListAsync();
    }

    public async Task<List<Attendance>> AttendanceRangeAsync(int groupId, DateOnly from, DateOnly to)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Attendances.AsNoTracking().Where(a => a.GroupId == groupId && a.Date >= from && a.Date <= to).ToListAsync();
    }

    public async Task SaveAttendanceAsync(int groupId, DateOnly date, IEnumerable<(int StudentId, AttendanceStatus Status, int LateMinutes)> rows, int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.Attendances.Where(a => a.GroupId == groupId && a.Date == date).ToListAsync();
        var changed = new List<(int, AttendanceStatus, int)>();
        foreach (var (sid, status, late) in rows)
        {
            var a = existing.FirstOrDefault(x => x.StudentId == sid);
            var minutes = status == AttendanceStatus.Late ? Math.Max(0, late) : 0;
            if (a is null)
            {
                db.Attendances.Add(new Attendance { GroupId = groupId, StudentId = sid, Date = date, Status = status, LateMinutes = minutes, CreatedByUserId = userId });
                changed.Add((sid, status, minutes));
            }
            else if (a.Status != status || a.LateMinutes != minutes)
            {
                a.Status = status; a.LateMinutes = minutes; a.UpdatedAt = DateTime.UtcNow;
                changed.Add((sid, status, minutes));
            }
        }
        await db.SaveChangesAsync();
        await audit.LogAsync(userId, "AttendanceChanged", "Group", groupId, $"{date} {changed.Count} changes");
        foreach (var (sid, status, minutes) in changed) _ = notifications.AttendanceMarkedAsync(sid, groupId, date, status, minutes);
    }

    public record AttendanceSummary(int Present, int Absent, int Late, int Excused)
    {
        public int Total => Present + Absent + Late + Excused;
        public double? Rate => Total == 0 ? null : Math.Round((Present + Late) * 100.0 / Total);
    }

    public async Task<AttendanceSummary> AttendanceSummaryAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var g = await db.Attendances.Where(a => a.StudentId == studentId).GroupBy(_ => 1)
            .Select(x => new { P = x.Count(a => a.Status == AttendanceStatus.Present), A = x.Count(a => a.Status == AttendanceStatus.Absent), L = x.Count(a => a.Status == AttendanceStatus.Late), E = x.Count(a => a.Status == AttendanceStatus.Excused) })
            .FirstOrDefaultAsync();
        return g is null ? new(0, 0, 0, 0) : new(g.P, g.A, g.L, g.E);
    }

    public async Task<List<Attendance>> StudentAttendanceAsync(int studentId, int take = 60)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Attendances.AsNoTracking().Include(a => a.Group).Where(a => a.StudentId == studentId).OrderByDescending(a => a.Date).Take(take).ToListAsync();
    }

    public async Task<List<Attendance>> GroupAttendanceHistoryAsync(int groupId, int days = 30)
    {
        await using var db = await factory.CreateDbContextAsync();
        var from = Tz.Today.AddDays(-days);
        return await db.Attendances.AsNoTracking().Where(a => a.GroupId == groupId && a.Date >= from).ToListAsync();
    }

    /// <summary>Groups that have a class today (by schedule) for the dashboard.</summary>
    public static bool HasClassOn(Group g, DateOnly date) => g.ScheduleDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Contains(((int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek).ToString());

    public async Task<List<User>> ParentsOfAsync(int studentId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ids = await db.ParentStudents.Where(p => p.StudentId == studentId).Select(p => p.ParentUserId).ToListAsync();
        return await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync();
    }

    public async Task<List<Student>> ChildrenOfAsync(int parentUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ids = await db.ParentStudents.Where(p => p.ParentUserId == parentUserId).Select(p => p.StudentId).ToListAsync();
        return await db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).Include(s => s.User)
            .Include(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)).ThenInclude(e => e.Group).ThenInclude(g => g.TeacherUser).OrderBy(s => s.User.FullName).ToListAsync();
    }

    public async Task UnlinkParentAsync(int parentUserId, int studentId, int adminUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.ParentStudents.Where(p => p.ParentUserId == parentUserId && p.StudentId == studentId).ExecuteDeleteAsync();
        await audit.LogAsync(adminUserId, "ParentUnlinked", "Student", studentId, $"parent {parentUserId}");
    }

    public async Task<string?> LinkParentAsync(int parentUserId, int studentId, int adminUserId)
    {
        await using var db = await factory.CreateDbContextAsync();
        if (!await db.Users.AnyAsync(u => u.Id == parentUserId && u.Role == UserRole.Parent)) return "Ota-ona topilmadi.";
        if (await db.ParentStudents.AnyAsync(p => p.ParentUserId == parentUserId && p.StudentId == studentId)) return "Allaqachon bog'langan.";
        db.ParentStudents.Add(new ParentStudent { ParentUserId = parentUserId, StudentId = studentId });
        await db.SaveChangesAsync();
        await audit.LogAsync(adminUserId, "ParentLinked", "Student", studentId, $"parent {parentUserId}");
        return null;
    }

    public async Task<List<AuditLog>> AuditAsync(int take = 200, string? action = null)
    {
        await using var db = await factory.CreateDbContextAsync();
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) q = q.Where(a => a.Action == action);
        return await q.OrderByDescending(a => a.CreatedAt).Take(take).ToListAsync();
    }

    public async Task<Dictionary<int, string>> UserNamesAsync(IEnumerable<int> ids)
    {
        await using var db = await factory.CreateDbContextAsync();
        var l = ids.Distinct().ToList();
        return await db.Users.Where(u => l.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
    }
}
