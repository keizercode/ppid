using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using PermintaanData.Data;

// WAJIB: harus dipanggil sebelum builder dibuat
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/auth/login";
        opt.AccessDeniedPath = "/auth/akses-ditolak";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

// ── HTTP Clients ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("WilayahApi", c =>
{
    c.BaseAddress = new Uri(builder.Configuration["ExternalApi:WilayahBase"]!);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("NikApi", c =>
{
    c.BaseAddress = new Uri("https://banksampah.jakarta.go.id");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("BidangApi", c =>
{
    c.BaseAddress = new Uri("https://ekinerjapjlp.jakarta.go.id");
    c.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseDeveloperExceptionPage();

// Pastikan folder wwwroot & uploads selalu ada
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwroot);
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads"));

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wwwroot),
    RequestPath = ""
});

app.UseRouting();

// ── Auth middleware (urutan WAJIB: Authentication dulu, baru Authorization) ───
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
