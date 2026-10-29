using System.Text;

namespace SatPlatform.Services;

public static class CsvExport
{
    /// <summary>Semicolon-separated with a BOM so Excel opens Uzbek text correctly.</summary>
    public static string Build(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        static string Cell(object? v) { var s = v?.ToString() ?? ""; return s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s; }
        var sb = new StringBuilder("﻿");
        sb.AppendLine(string.Join(';', headers.Select(Cell)));
        foreach (var r in rows) sb.AppendLine(string.Join(';', r.Select(Cell)));
        return sb.ToString();
    }
}
