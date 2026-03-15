using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;

var builder = WebApplication.CreateBuilder(args);

// ── 1. MVC ────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── 2. Database (PostgreSQL) ──────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── 3. HttpClient untuk ApiProxyController ────────────────────────────────────
// Cukup satu baris — ApiProxyController membaca URL dari config sendiri
// dan memanggil httpFactory.CreateClient() tanpa named client.
builder.Services.AddHttpClient();

// ── 4. Cookie Authentication ──────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// ── 5. Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── 6. Exception Handler ──────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// ── 7. Static Files ───────────────────────────────────────────────────────────
app.UseStaticFiles();

// ── 8. Routing — WAJIB sebelum UseAuthentication / UseAuthorization ───────────
app.UseRouting();

// ── 9. Auth — urutan tidak boleh terbalik ────────────────────────────────────
app.UseAuthentication();
app.UseAuthorization();

// ── 10. Route Table ───────────────────────────────────────────────────────────
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
