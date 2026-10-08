using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Data;

namespace SatPlatform.Services;

public record OptionSnapshot(string Key, string Text, bool IsCorrect);

/// <summary>Answer checking, raw → scaled conversion (versioned tables) and adaptive routing rules.</summary>
public class ScoringService(IDbContextFactory<AppDbContext> factory, SettingsService settings)
{
    // ---------------------------------------------------------------- answers

    public static List<OptionSnapshot> Options(QuestionVersion v)
    {
        try { return JsonSerializer.Deserialize<List<OptionSnapshot>>(v.OptionsJson) ?? []; }
        catch { return []; }
    }

    public static bool IsCorrect(QuestionVersion v, string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return false;
        if (v.Type == QuestionType.SPR) return IsSprCorrect(v.CorrectAnswer, answer);
        var correct = Options(v).FirstOrDefault(o => o.IsCorrect)?.Key;
        return correct is not null && string.Equals(correct, answer.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Student-produced response rules: integer, negative, decimal or fraction. A decimal that is a rounding or
    /// truncation (to at least 3 decimals) of the exact value is accepted, like on the real digital SAT.
    /// Accepted values are listed by the author separated by '|'.
    /// </summary>
    public static bool IsSprCorrect(string? spec, string? answer)
    {
        if (string.IsNullOrWhiteSpace(spec) || string.IsNullOrWhiteSpace(answer)) return false;
        var given = NormalizeSpr(answer);
        if (given is null) return false;
        var studentValue = ParseNumber(given);
        foreach (var raw in spec.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var accepted = NormalizeSpr(raw);
            if (accepted is null) continue;
            if (accepted == given) return true;
            var acceptedValue = ParseNumber(accepted);
            if (studentValue is null || acceptedValue is null) continue;
            if (studentValue == acceptedValue) return true;
            // rounding / truncation to 3+ decimals (e.g. 2/3 → .666 or .667)
            var diff = Math.Abs(studentValue.Value - acceptedValue.Value);
            if (diff < 0.001001m && DecimalPlaces(given) >= 3) return true;
        }
        return false;
    }

    public static string? NormalizeSpr(string s)
    {
        s = s.Trim().Replace(" ", "").Replace(",", ".");
        if (s.Length == 0 || s.Length > 12) return null;
        if (s.StartsWith('+')) s = s[1..];
        return s;
    }

    public static decimal? ParseNumber(string s)
    {
        if (s.Contains('/'))
        {
            var p = s.Split('/');
            if (p.Length != 2) return null;
            if (!decimal.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a)) return null;
            if (!decimal.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b) || b == 0) return null;
            return a / b;
        }
        return decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int DecimalPlaces(string s)
    {
        var i = s.IndexOf('.');
        return i < 0 ? 0 : s.Length - i - 1;
    }

    public static string? ValidateSprInput(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;
        var n = NormalizeSpr(answer);
        if (n is null) return "Javob juda uzun.";
        if (ParseNumber(n) is null) return "Javob son, kasr (3/4) yoki o'nlik (0.75) bo'lishi kerak.";
        var limit = n.StartsWith('-') ? 6 : 5;
        if (n.Length > limit) return $"Ko'pi bilan {limit} ta belgi kiriting (masalan 0.667 yoki 2/3).";
        return null;
    }

    // ---------------------------------------------------------------- scaled scores

    private Dictionary<(string Version, Section Section, ModuleRoute Route), Dictionary<int, int>>? _tables;
    private DateTime _tablesLoadedAt;

    public void Invalidate() { _tables = null; _rules = null; }

    private async Task<Dictionary<(string, Section, ModuleRoute), Dictionary<int, int>>> TablesAsync()
    {
        if (_tables is not null && DateTime.UtcNow - _tablesLoadedAt < TimeSpan.FromMinutes(1)) return _tables;
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.ScoringEntries.AsNoTracking().ToListAsync();
        var map = new Dictionary<(string, Section, ModuleRoute), Dictionary<int, int>>();
        foreach (var r in rows)
        {
            var key = (r.ScoringVersion, r.Section, r.Route);
            if (!map.TryGetValue(key, out var d)) map[key] = d = new Dictionary<int, int>();
            d[r.RawScore] = r.ScaledScore;
        }
        _tables = map; _tablesLoadedAt = DateTime.UtcNow;
        return map;
    }

    /// <summary>Scaled section score (200–800). Falls back to the Standard route table, then to a linear curve.</summary>
    public async Task<int> ScaledAsync(string version, Section section, ModuleRoute route, int raw, int maxRaw)
    {
        var tables = await TablesAsync();
        foreach (var r in new[] { route, ModuleRoute.Standard, ModuleRoute.Hard })
        {
            if (tables.TryGetValue((version, section, r), out var table) && table.Count > 0)
            {
                if (table.TryGetValue(raw, out var s)) return s;
                var below = table.Keys.Where(k => k < raw).DefaultIfEmpty(-1).Max();
                var above = table.Keys.Where(k => k > raw).DefaultIfEmpty(-1).Min();
                if (below >= 0 && above >= 0) return (int)Math.Round(table[below] + (table[above] - table[below]) * (double)(raw - below) / (above - below));
                if (below >= 0) return table[below];
                if (above >= 0) return table[above];
            }
        }
        return Linear(route, raw, maxRaw);
    }

    public static int Linear(ModuleRoute route, int raw, int maxRaw)
    {
        if (maxRaw <= 0) return 200;
        var top = route == ModuleRoute.Easy ? 640 : 800;
        var v = 200 + (top - 200) * (double)Math.Clamp(raw, 0, maxRaw) / maxRaw;
        return (int)Math.Round(v / 10) * 10;
    }

    public async Task<List<string>> VersionsAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.ScoringEntries.Select(e => e.ScoringVersion).Distinct().OrderBy(v => v).ToListAsync();
        if (list.Count == 0) list.Add(await settings.GetAsync(SettingKeys.DefaultScoringVersion));
        return list;
    }

    public async Task<List<ScoringEntry>> EntriesAsync(string version)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.ScoringEntries.AsNoTracking().Where(e => e.ScoringVersion == version).OrderBy(e => e.Section).ThenBy(e => e.Route).ThenBy(e => e.RawScore).ToListAsync();
    }

    public async Task SaveEntriesAsync(string version, Section section, ModuleRoute route, IEnumerable<(int Raw, int Scaled)> entries)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.ScoringEntries.Where(e => e.ScoringVersion == version && e.Section == section && e.Route == route).ExecuteDeleteAsync();
        foreach (var (raw, scaled) in entries)
            db.ScoringEntries.Add(new ScoringEntry { ScoringVersion = version, Section = section, Route = route, RawScore = raw, ScaledScore = Math.Clamp(scaled, 200, 800) });
        await db.SaveChangesAsync();
        Invalidate();
    }

    /// <summary>Parses "raw,scaled" lines (or "raw scaled", "raw;scaled").</summary>
    public static List<(int Raw, int Scaled)> ParseTable(string text)
    {
        var list = new List<(int, int)>();
        foreach (var line in text.Split('\n'))
        {
            var p = line.Split([',', ';', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (p.Length >= 2 && int.TryParse(p[0], out var r) && int.TryParse(p[1], out var s)) list.Add((r, s));
        }
        return list;
    }

    // ---------------------------------------------------------------- adaptive rules

    private Dictionary<Section, AdaptiveRule>? _rules;

    public async Task<AdaptiveRule> RuleAsync(Section section)
    {
        if (_rules is null)
        {
            await using var db = await factory.CreateDbContextAsync();
            _rules = await db.AdaptiveRules.AsNoTracking().ToDictionaryAsync(r => r.Section);
        }
        if (_rules.TryGetValue(section, out var rule)) return rule;
        return section == Section.RW
            ? new AdaptiveRule { Section = Section.RW, QuestionCount = 27, Cutoff = 15, DefaultModuleSeconds = 1920 }
            : new AdaptiveRule { Section = Section.Math, QuestionCount = 22, Cutoff = 12, DefaultModuleSeconds = 2100 };
    }

    public async Task<ModuleRoute> RouteForAsync(Section section, int module1Raw, int module1Count)
    {
        var rule = await RuleAsync(section);
        // the cutoff is defined for the configured question count; scale it when a module has a different size
        var cutoff = rule.QuestionCount > 0 && module1Count > 0 && module1Count != rule.QuestionCount
            ? (int)Math.Ceiling(rule.Cutoff * (double)module1Count / rule.QuestionCount)
            : rule.Cutoff;
        return module1Raw >= cutoff ? rule.HighRoute : rule.LowRoute;
    }

    public async Task SaveRuleAsync(AdaptiveRule rule)
    {
        await using var db = await factory.CreateDbContextAsync();
        var existing = await db.AdaptiveRules.FirstOrDefaultAsync(r => r.Section == rule.Section);
        if (existing is null) db.AdaptiveRules.Add(rule);
        else { existing.QuestionCount = rule.QuestionCount; existing.Cutoff = rule.Cutoff; existing.LowRoute = rule.LowRoute; existing.HighRoute = rule.HighRoute; existing.DefaultModuleSeconds = rule.DefaultModuleSeconds; }
        await db.SaveChangesAsync();
        Invalidate();
    }

    public async Task<List<AdaptiveRule>> RulesAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var list = await db.AdaptiveRules.AsNoTracking().OrderBy(r => r.Section).ToListAsync();
        foreach (var s in new[] { Section.RW, Section.Math }) if (list.All(r => r.Section != s)) list.Add(await RuleAsync(s));
        return list.OrderBy(r => r.Section).ToList();
    }

    /// <summary>Seeds a default SAT_2026_V1 table shaped like the published Bluebook practice-test curves.</summary>
    public static IEnumerable<ScoringEntry> DefaultTable(string version)
    {
        // R&W: 54 questions. Math: 44 questions. Hard route reaches 800; easy route is capped.
        foreach (var section in new[] { Section.RW, Section.Math })
        {
            var max = section == Section.RW ? 54 : 44;
            foreach (var route in new[] { ModuleRoute.Hard, ModuleRoute.Easy, ModuleRoute.Standard })
            {
                var top = route == ModuleRoute.Easy ? 640 : 800;
                for (var raw = 0; raw <= max; raw++)
                {
                    var x = (double)raw / max;
                    // gentle S-curve: slow at the bottom, steep in the middle
                    var curve = route == ModuleRoute.Easy ? Math.Pow(x, 0.9) : 0.5 + 0.5 * Math.Tanh((x - 0.5) * 2.6) / Math.Tanh(1.3);
                    var scaled = 200 + (top - 200) * curve;
                    yield return new ScoringEntry { ScoringVersion = version, Section = section, Route = route, RawScore = raw, ScaledScore = (int)Math.Round(Math.Clamp(scaled, 200, 800) / 10) * 10 };
                }
            }
        }
    }
}
