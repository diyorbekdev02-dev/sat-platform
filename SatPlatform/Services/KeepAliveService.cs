namespace SatPlatform.Services;

/// <summary>Pings the public URL every 10 minutes so a free-tier host does not put the server to sleep (App:KeepAlive=true).</summary>
public class KeepAliveService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<KeepAliveService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!string.Equals(config["App:KeepAlive"], "true", StringComparison.OrdinalIgnoreCase)) return;
        var url = (config["App:PublicUrl"] ?? "").TrimEnd('/');
        if (url.Length == 0) return;
        await Task.Delay(TimeSpan.FromMinutes(1), ct);
        while (!ct.IsCancellationRequested)
        {
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
