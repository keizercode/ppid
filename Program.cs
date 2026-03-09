using PermintaanData.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// HttpClient untuk proxy ke API eksternal
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
