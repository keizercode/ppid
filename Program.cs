using PermintaanData.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

// WAJIB: harus dipanggil sebelum builder dibuat
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("WilayahApi", c => {
    c.BaseAddress = new Uri(builder.Configuration["ExternalApi:WilayahBase"]!);
    c.DefaultRequestHeaders.Add("Accept", "application/json");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("NikApi", c => {
    c.BaseAddress = new Uri("https://banksampah.jakarta.go.id");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("BidangApi", c => {
    c.BaseAddress = new Uri("https://ekinerjapjlp.jakarta.go.id");
    c.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseDeveloperExceptionPage();

// Pastikan folder wwwroot & uploads selalu ada
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(wwwroot);
Directory.CreateDirectory(Path.Combine(wwwroot, "uploads"));

// Serve static files dari wwwroot secara eksplisit
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wwwroot),
    RequestPath  = ""
});

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
