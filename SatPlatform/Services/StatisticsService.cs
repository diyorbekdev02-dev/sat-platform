using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

/// <summary>Updates per-skill statistics, the mistake bank and question analytics after a test or practice.</summary>
public class StatisticsService
{
    /// <summary>Records answered questions. Unanswered questions (in tests) count as wrong attempts.</summary>
    public async Task RecordAsync(AppDbContext db, int studentId, IEnumerable<(int QuestionId, int SkillId, bool Correct, int Seconds, string? Answer)> answers, string source)
    {
        var list = answers.ToList();
        if (list.Count == 0) return;
        var now = DateTime.UtcNow;

        var skillIds = list.Select(a => a.SkillId).Distinct().ToList();
        var stats = await db.StudentSkillStatistics.Where(s => s.StudentId == studentId && skillIds.Contains(s.SkillId)).ToListAsync();
        foreach (var g in list.GroupBy(a => a.SkillId))
        {
            var s = stats.FirstOrDefault(x => x.SkillId == g.Key);
            if (s is null) { s = new StudentSkillStatistic { StudentId = studentId, SkillId = g.Key }; db.StudentSkillStatistics.Add(s); stats.Add(s); }
            s.AttemptedCount += g.Count();
            s.CorrectCount += g.Count(a => a.Correct);
            s.TotalTimeSeconds += g.Sum(a => (long)a.Seconds);
            s.UpdatedAt = now;
        }

        var questionIds = list.Select(a => a.QuestionId).Distinct().ToList();
        var mistakes = await db.StudentMistakes.Where(m => m.StudentId == studentId && questionIds.Contains(m.QuestionId)).ToListAsync();
        foreach (var a in list)
        {
            var m = mistakes.FirstOrDefault(x => x.QuestionId == a.QuestionId);
            if (!a.Correct)
            {
                if (m is null) { m = new StudentMistake { StudentId = studentId, QuestionId = a.QuestionId, SkillId = a.SkillId }; db.StudentMistakes.Add(m); mistakes.Add(m); }
                m.MistakeCount++; m.IsResolved = false; m.ResolvedAt = null; m.LastWrongAt = now; m.LastSource = source; m.LastAnswer = a.Answer;
            }
            else if (m is not null && !m.IsResolved)
            {
                m.IsResolved = true; m.ResolvedAt = now;
            }
        }

        foreach (var g in list.GroupBy(a => a.QuestionId))
        {
            var correct = g.Count(a => a.Correct);
            var time = g.Sum(a => (long)a.Seconds);
            await db.Questions.Where(q => q.Id == g.Key).ExecuteUpdateAsync(s => s
                .SetProperty(q => q.AttemptCount, q => q.AttemptCount + g.Count())
                .SetProperty(q => q.CorrectCount, q => q.CorrectCount + correct)
                .SetProperty(q => q.TotalTimeSeconds, q => q.TotalTimeSeconds + time));
        }
    }
}
