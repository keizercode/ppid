using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("auth")]
public class AuthController(AppDbContext db) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl);

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginPost(LoginVm vm, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View("Login", vm);

        var user = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Username == vm.Username && u.IsActive);

        if (user == null || !user.VerifyPassword(vm.Password))
        {
            ModelState.AddModelError("", "Username atau password salah.");
            return View("Login", vm);
        }

        // Buat claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.AppUserID.ToString()),
            new(ClaimTypes.Name,           user.NamaLengkap),
            new(ClaimTypes.Role,           user.Role),
            new("Username",                user.Username),
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = vm.RememberMe,
                ExpiresUtc = vm.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToLocal(returnUrl) ?? RedirectToRoleHome(user.Role);
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet("akses-ditolak")]
    public IActionResult AksesDitolak() => View();

    // ── Helpers ───────────────────────────────────────────────────────────

    private IActionResult? RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return null;
    }

    private IActionResult RedirectToRoleHome(string role) => role switch
    {
        "Loket" => Redirect("/petugas-loket"),
        "Kepegawaian" => Redirect("/kepegawaian"),
        "KDI" => Redirect("/kdi"),
        "ProdusenData" => Redirect("/produsen-data"),
        _ => Redirect("/petugas-loket")
    };
}
