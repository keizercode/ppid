using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;

var builder = WebApplication.CreateBuilder(args);

// ── 1. Logging ────────────────────────────────────────────────────────────────
builder.Logging
    .ClearProviders()
    .AddConsole()
    .AddDebug()
    .SetMinimumLevel(builder.Environment.IsDevelopment()
        ? LogLevel.Debug
        : LogLevel.Warning);

// ── 2. MVC ────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── 3. Database (PostgreSQL) ──────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
            npgsql.CommandTimeout(30);
        });

    if (builder.Environment.IsDevelopment())
        options.EnableDetailedErrors().EnableSensitiveDataLogging();
});

// ── 4. HttpClient untuk ApiProxyController ────────────────────────────────────
builder.Services.AddHttpClient();

// ── 5. Cookie Authentication ──────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/akses-ditolak";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.None
            : CookieSecurePolicy.Always;
    });

// ── 6. Memory Cache (untuk AuthController rate-limit) ─────────────────────────
builder.Services.AddMemoryCache();

// ── 7. Health Checks ──────────────────────────────────────────────────────────
// Membutuhkan: dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
builder.Services.AddHealthChecks();

var password = Environment.GetEnvironmentVariable("CERT_PASSWORD");

// builder.WebHost.ConfigureKestrel(options =>
// {
//     options.ListenAnyIP(5055, listenOptions =>
//     {
//         listenOptions.UseHttps("/var/www/ppid/cert.pfx", password);
//     });
// });
// ── 8. Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── 9. Migrate & Seed Database on Startup ────────────────────────────────────
await RunStartupMigrationsAsync(app);

// ── 10. Exception Handler ─────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    // Development: tampilkan detail teknis lengkap (YSOD)
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exFeature = context.Features.Get<IExceptionHandlerPathFeature>();
            var logger = context.RequestServices
                .GetRequiredService<ILogger<Program>>();

            if (exFeature?.Error is not null)
            {
                logger.LogError(exFeature.Error,
                    "Unhandled exception on path {Path}", exFeature.Path);
            }

            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(BuildErrorHtml());
        });
    });
    app.UseHsts();
}

// ── 11. Status Code Pages (404, 403, dll.) ────────────────────────────────────
app.UseStatusCodePages(async ctx =>
{
    var code = ctx.HttpContext.Response.StatusCode;
    if (code == 404)
    {
        ctx.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync(Build404Html());
    }
});

app.UseHttpsRedirection();
// ── 12. Static Files ──────────────────────────────────────────────────────────
app.UseStaticFiles();

// ── 13. Routing — WAJIB sebelum Auth ──────────────────────────────────────────
app.UseRouting();

// ── 14. Auth ──────────────────────────────────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── 15. Health Check Endpoint ─────────────────────────────────────────────────
app.MapHealthChecks("/health");

// ── 16. Route Table ───────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ══════════════════════════════════════════════════════════════════════════════
// STARTUP MIGRATION HELPER
// ══════════════════════════════════════════════════════════════════════════════

static async Task RunStartupMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var env = services.GetRequiredService<IWebHostEnvironment>();

    try
    {
        var db = services.GetRequiredService<AppDbContext>();

        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect)
        {
            logger.LogCritical(
                "Tidak dapat terhubung ke database. Periksa connection string di appsettings.");
            if (!env.IsDevelopment())
                throw new InvalidOperationException("Database connection failed on startup.");
            return;
        }

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation(
                "Menerapkan {Count} pending migration(s): {Migrations}",
                pending.Count, string.Join(", ", pending));

            await db.Database.MigrateAsync();
            logger.LogInformation("Migrasi database selesai.");
        }
        else
        {
            logger.LogDebug("Tidak ada pending migration.");
        }

        await ValidateCriticalTablesAsync(db, logger);
    }
    catch (Exception ex) when (ex is not InvalidOperationException)
    {
        logger.LogCritical(ex, "Startup database migration gagal.");
        if (!env.IsDevelopment()) throw;
    }
}

static async Task ValidateCriticalTablesAsync(AppDbContext db, ILogger logger)
{
    try
    {
        await db.Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM information_schema.tables " +
            "WHERE table_schema = 'public' " +
            "AND table_name = 'NoPermohonanCounter'");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "Validasi tabel NoPermohonanCounter gagal — pastikan migration sudah dijalankan.");
    }

    var userCount = await db.AppUsers.CountAsync();
    if (userCount == 0)
        logger.LogWarning(
            "Tabel AppUser kosong! Seed user default belum ada. Jalankan migration atau seed manual.");
    else
        logger.LogDebug("AppUser count: {Count}", userCount);
}

// ── HTML builder untuk error pages ───────────────────────────────────────────
// CATATAN: Gunakan {{ dan }} untuk literal brace di dalam $"""..."""
// karena { } di-parse sebagai ekspresi interpolasi C#.

static string BuildErrorHtml()
{
    var refCode = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
    var year = DateTime.Now.Year;

    // Tidak menggunakan $""" agar CSS { } tidak perlu di-escape
    return $$"""
    <!DOCTYPE html>
    <html lang="id">
    <head>
      <meta charset="utf-8"/>
      <meta name="viewport" content="width=device-width,initial-scale=1"/>
      <title>Terjadi Kesalahan — PPID DLH Jakarta</title>
      <script src="https://cdn.tailwindcss.com"></script>
      <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;700;800&display=swap" rel="stylesheet"/>
      <style>* { font-family:'Plus Jakarta Sans',sans-serif; }</style>
    </head>
    <body class="min-h-screen bg-[#F5F7FA] flex items-center justify-center px-4">
      <div class="text-center max-w-md w-full">
        <div class="text-6xl mb-4">⚠️</div>
        <h1 class="text-2xl font-extrabold text-gray-900 mb-2">Terjadi Kesalahan Sistem</h1>
        <p class="text-gray-500 text-sm mb-2 leading-relaxed">
          Terjadi kesalahan yang tidak terduga. Tim teknis telah diberitahu.
          Silakan coba kembali atau hubungi administrator.
        </p>
        <p class="text-gray-400 text-xs mb-6">
          Referensi: {{refCode}} · PPID DLH Jakarta
        </p>
        <div class="flex gap-3 justify-center flex-wrap">
          <a href="javascript:history.back()"
             class="bg-[#002d7a] hover:bg-blue-900 text-white font-bold px-5 py-2.5 rounded-xl text-sm transition-colors">
            ← Kembali
          </a>
          <a href="/"
             class="border border-gray-200 text-gray-600 font-semibold px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50 transition-colors">
            Halaman Utama
          </a>
          <a href="/health"
             class="border border-gray-200 text-gray-600 font-semibold px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50 transition-colors">
            🩺 Cek Status
          </a>
        </div>
        <p class="text-gray-300 text-xs mt-8">© {{year}} PPID Dinas Lingkungan Hidup DKI Jakarta</p>
      </div>
    </body>
    </html>
    """;
}

static string Build404Html() => """
    <!DOCTYPE html>
    <html lang="id">
    <head>
      <meta charset="utf-8"/>
      <meta name="viewport" content="width=device-width,initial-scale=1"/>
      <title>Halaman Tidak Ditemukan — PPID DLH Jakarta</title>
      <script src="https://cdn.tailwindcss.com"></script>
      <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;700;800&display=swap" rel="stylesheet"/>
      <style>* { font-family:'Plus Jakarta Sans',sans-serif; }</style>
    </head>
    <body class="min-h-screen bg-[#F5F7FA] flex items-center justify-center px-4">
      <div class="text-center max-w-sm">
        <div class="text-6xl mb-4">🔍</div>
        <h1 class="text-2xl font-extrabold text-gray-900 mb-2">Halaman Tidak Ditemukan</h1>
        <p class="text-gray-500 text-sm mb-6 leading-relaxed">
          Halaman yang Anda cari tidak tersedia atau sudah dipindahkan.
        </p>
        <div class="flex gap-3 justify-center">
          <a href="javascript:history.back()"
             class="bg-[#002d7a] hover:bg-blue-900 text-white font-bold px-5 py-2.5 rounded-xl text-sm">
            ← Kembali
          </a>
          <a href="/"
             class="border border-gray-200 text-gray-600 font-semibold px-5 py-2.5 rounded-xl text-sm hover:bg-gray-50">
            Beranda
          </a>
        </div>
      </div>
    </body>
    </html>
    """;
