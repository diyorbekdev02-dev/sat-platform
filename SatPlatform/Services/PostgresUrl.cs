namespace SatPlatform.Services;

public static class PostgresUrl
{
    /// <summary>Turns postgres://user:pass@host:port/db?sslmode=require (Neon / Render / Railway) into an Npgsql connection string.</summary>
    public static string ToConnectionString(string url)
    {
        if (!url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return url;

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var db = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var sslmode = query["sslmode"] ?? "require";
        var ssl = sslmode.Equals("disable", StringComparison.OrdinalIgnoreCase) ? "Disable" : "Require";

        return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode={ssl};Trust Server Certificate=true;Pooling=true;Maximum Pool Size=20";
    }
}
