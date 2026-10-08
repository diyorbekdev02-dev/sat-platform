using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SatPlatform.Components;
using SatPlatform.Data;
using SatPlatform.Services;
using SatPlatform.Services.Telegram;

var builder = WebApplication.CreateBuilder(args);

// Secrets (Telegram bot token, admin password) live in this optional file; environment variables still win over it.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

if (Environment.GetEnvironmentVariable("PORT") is { Length: > 0 } port)
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") is { Length: > 0 } databaseUrl
    ? PostgresUrl.ToConnectionString(databaseUrl)
    : builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings:Postgres yoki DATABASE_URL kerak.");

builder.Services.AddRazorComponents().AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddDbContextFactory<AppDbContext>(o => o.UseNpgsql(connectionString, n => n.EnableRetryOnFailure(3)));
builder.Services.AddDataProtection().SetApplicationName("SatPlatform").PersistKeysToDbContext<AppDbContext>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "sat.auth";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromDays(30);
    o.SlidingExpiration = true;
    o.LoginPath = "/login";
    o.AccessDeniedPath = "/forbidden";
});
builder.Services.AddAuthorization();

// app services
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<AuditService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ScoringService>();
builder.Services.AddSingleton<ContentService>();
builder.Services.AddSingleton<MediaService>();
builder.Services.AddSingleton<StatisticsService>();
builder.Services.AddSingleton<TestBuilderService>();
builder.Services.AddSingleton<TestEngineService>();
builder.Services.AddSingleton<PracticeService>();
builder.Services.AddSingleton<AnalyticsService>();
builder.Services.AddSingleton<AcademyService>();
builder.Services.AddSingleton<HomeworkService>();
builder.Services.AddSingleton<MaterialService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<SeedService>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SatPlatform.Components.Layout.PageTitleState>();

builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient<TelegramBotClient>(c => c.Timeout = TimeSpan.FromSeconds(60));
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<AttemptWatchdog>();
builder.Services.AddHostedService<PracticeReminderJob>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<KeepAliveService>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using (var db = await factory.CreateDbContextAsync()) await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SeedService>().RunAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// ---- one-time login token (issued by the interactive login page or the Telegram bot) → auth cookie
app.MapGet("/auth/token", async (string? t, HttpContext http, AuthService auth) =>
{
    if (string.IsNullOrWhiteSpace(t)) return Results.Redirect("/login?error=token");
    var (user, returnUrl) = await auth.ConsumeLoginTokenAsync(t.Trim());
    if (user is null) return Results.Redirect("/login?error=token");
    await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, AuthService.BuildPrincipal(user, CookieAuthenticationDefaults.AuthenticationScheme),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });
    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") ? returnUrl : AuthService.HomeFor(user.Role);
    return Results.Redirect(target);
});

app.MapGet("/auth/signout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

// ---- uploaded files (stored in PostgreSQL)
app.MapGet(MediaService.RequestPath + "/{name}", async (string name, MediaService media, HttpContext http) =>
{
    var file = await media.GetAsync(name);
    if (file is null) return Results.NotFound();
    http.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
    return Results.File(file.Data, file.ContentType, enableRangeProcessing: true);
});
app.MapGet("/files/{id:int}", async (int id, MediaService media, HttpContext http, IDbContextFactory<AppDbContext> factory) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    await using var db = await factory.CreateDbContextAsync();
    var file = await db.MediaFiles.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
    return file is null ? Results.NotFound() : Results.File(file.Data, file.ContentType, file.OriginalName);
});

// ---- image upload from the rich editor (staff only)
app.MapPost("/api/upload-image", async (HttpContext http, MediaService media) =>
{
    if (http.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    var role = http.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
    var canManage = http.User.HasClaim("can_manage_questions", "true");
    if (role is not ("Admin" or "SuperAdmin" or "Teacher" or "Assistant") && !canManage) return Results.Forbid();
    var form = await http.Request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null) return Results.BadRequest("file");
    var userId = int.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    await using var stream = file.OpenReadStream();
    var (url, _, error) = await media.UploadAsync(stream, file.FileName, file.ContentType, file.Length, userId, imagesOnly: true);
    return error is null ? Results.Json(new { url }) : Results.BadRequest(error);
}).DisableAntiforgery();

app.MapGet("/healthz", () => Results.Text("ok"));

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
