using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("auth")]
public class AuthController(AppDbContext db, IMemoryCache cache) : Controller
{
    private const int MaxFailedAttempts                  = 5;
    private static readonly TimeSpan LockoutDuration    = TimeSpan.FromMinutes(15);

    // ── Login ─────────────────────────────────────────────────────────────

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToLocal(returnUrl) ?? RedirectByRole(CurrentRole);

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> LoginPost(LoginVm vm, string? returnUrl)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View("Login", vm);

        var clientIp = GetClientIp();
        var cacheKey = $"login_attempts:{clientIp}";

        if (IsLockedOut(cacheKey, out var remaining))
        {
            ModelState.AddModelError(string.Empty,
                $"Terlalu banyak percobaan login. Coba lagi dalam {remaining} menit.");
            return View("Login", vm);
        }

        var user = await db.AppUsers.FirstOrDefaultAsync(
            u => u.Username == vm.Username && u.IsActive);

        if (user == null || !user.VerifyPassword(vm.Password))
        {
            RecordFailedAttempt(cacheKey);
            ModelState.AddModelError(string.Empty, "Username atau password salah.");
            return View("Login", vm);
        }

        ResetFailedAttempts(cacheKey);

        // Upgrade hash lama (SHA256 → BCrypt) secara transparan
        if (user.IsLegacyHash)
        {
            user.PasswordHash = AppUser.HashPassword(vm.Password);
            user.UpdatedAt    = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        await SignInUser(user, vm.RememberMe);
        return RedirectToLocal(returnUrl) ?? RedirectByRole(user.Role);
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("akses-ditolak")]
    public IActionResult AksesDitolak() => View();

    // ── Helpers ───────────────────────────────────────────────────────────

    private string CurrentRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    private async Task SignInUser(AppUser user, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.AppUserID.ToString()),
            new(ClaimTypes.Name,           user.NamaLengkap),
            new(ClaimTypes.Role,           user.Role),
            new("Username",                user.Username),
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc   = rememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });
    }

    /// <summary>Redirect ke landing page sesuai role.</summary>
    private static IActionResult RedirectByRole(string role) => role switch
{
    AppRoles.Loket               => new RedirectResult("/petugas-loket"),
    AppRoles.LoketUmum           => new RedirectResult("/loket-umum"),
    AppRoles.KasubkelKepegawaian => new RedirectResult("/kasubkel-kepegawaian"),
    AppRoles.KasubkelKDI         => new RedirectResult("/kasubkel-kdi"),
    AppRoles.Admin               => new RedirectResult("/petugas-loket"),
    _                            => new RedirectResult("/petugas-loket")
};

    // ── Rate limiting ─────────────────────────────────────────────────────

    private bool IsLockedOut(string key, out int remainingMinutes)
    {
        remainingMinutes = 0;
        if (!cache.TryGetValue(key, out LoginAttemptInfo? info) || info is null) return false;
        if (info.Count < MaxFailedAttempts) return false;
        remainingMinutes = (int)Math.Ceiling((info.LockedUntil - DateTime.UtcNow).TotalMinutes);
        return remainingMinutes > 0;
    }

    private void RecordFailedAttempt(string key)
    {
        var info = cache.GetOrCreate(key, e =>
        {
            e.AbsoluteExpirationRelativeToNow = LockoutDuration;
            return new LoginAttemptInfo();
        })!;

        if (++info.Count >= MaxFailedAttempts)
            info.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);

        cache.Set(key, info, LockoutDuration);
    }

    private void ResetFailedAttempts(string key) => cache.Remove(key);

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private IActionResult? RedirectToLocal(string? url) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? Redirect(url) : null;

    private sealed class LoginAttemptInfo
    {
        public int      Count       { get; set; }
        public DateTime LockedUntil { get; set; }
    }
}
