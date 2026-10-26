namespace SatPlatform.Services;

/// <summary>
/// Pings the public URL every 10 minutes so a free-tier host does not put the server to sleep (App:KeepAlive=true).
/// App:KeepAliveHours ("07-23", Asia/Tashkent) limits the pings to a daily window; outside it the host may sleep,
/// which saves free-tier instance hours. Omitted or "0-24" = all day.
/// </summary>
public class KeepAliveService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<KeepAliveService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!string.Equals(config["App:KeepAlive"], "true", StringComparison.OrdinalIgnoreCase)) return;
        var url = (config["App:PublicUrl"] ?? "").TrimEnd('/');
        if (url.Length == 0) return;
        var schedule = KeepAliveSchedule.Parse(config["App:KeepAliveHours"]);
        logger.LogInformation("keep-alive: {Url} within {Schedule} (Asia/Tashkent)", url, schedule);
        await Task.Delay(TimeSpan.FromMinutes(1), ct);
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            if (!schedule.IsActive(now))
            {
                var wait = schedule.UntilNextStart(now);
                logger.LogInformation("keep-alive: outside the window, sleeping {Wait}", wait);
                try { await Task.Delay(wait, ct); } catch (OperationCanceledException) { }
                continue;
            }
            try
            {
                using var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                await client.GetAsync($"{url}/healthz", ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogDebug(ex, "keep-alive ping failed"); }
            try { await Task.Delay(TimeSpan.FromMinutes(10), ct); } catch (OperationCanceledException) { }
        }
    }
}

/// <summary>Daily window in Asia/Tashkent local hours, e.g. "07-23" (07:00 ≤ t &lt; 23:00); "22-06" wraps past midnight.</summary>
public sealed record KeepAliveSchedule(int StartHour, int EndHour)
{
    public static readonly KeepAliveSchedule AllDay = new(0, 24);

    public static KeepAliveSchedule Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return AllDay;
        var parts = text.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end)
            || start is < 0 or > 24 || end is < 0 or > 24 || start == end)
            return AllDay;
        return new KeepAliveSchedule(start, end);
    }

    public bool IsAllDay => (StartHour, EndHour) is (0, 24);

    public bool IsActive(DateTime utc)
    {
        if (IsAllDay) return true;
        var h = Tz.ToLocal(utc).TimeOfDay.TotalHours;
        return StartHour < EndHour ? h >= StartHour && h < EndHour : h >= StartHour || h < EndHour;
    }

    /// <summary>Time until the window next opens (at least 1 minute).</summary>
    public TimeSpan UntilNextStart(DateTime utc)
    {
        var local = Tz.ToLocal(utc);
        var next = local.Date.AddHours(StartHour);
        if (next <= local) next = next.AddDays(1);
        var wait = next - local;
        return wait < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : wait;
    }

    public override string ToString() => IsAllDay ? "all day" : $"{StartHour:00}:00-{EndHour:00}:00";
}
