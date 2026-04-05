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
    // ── Rate limiting ─────────────────────────────────────────────────────
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // ── Login ─────────────────────────────────────────────────────────────

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return RedirectToLocal(returnUrl) ?? RedirectByRole(role);
        }

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

        if (IsLockedOut(cacheKey, out var remainingMinutes))
        {
            ModelState.AddModelError(string.Empty,
                $"Terlalu banyak percobaan login gagal. Coba lagi dalam {remainingMinutes} menit.");
            return View("Login", vm);
        }

        var user = await db.AppUsers
            .FirstOrDefaultAsync(u => u.Username == vm.Username && u.IsActive);

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
                IsPersistent = vm.RememberMe,
                ExpiresUtc   = vm.RememberMe
                    ? DateTimeOffset.UtcNow.AddDays(7)
                    : DateTimeOffset.UtcNow.AddHours(8)
            });

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

    // ── Rate limiting helpers ─────────────────────────────────────────────

    private bool IsLockedOut(string cacheKey, out int remainingMinutes)
    {
        remainingMinutes = 0;
        if (!cache.TryGetValue(cacheKey, out LoginAttemptInfo? info) || info == null)
            return false;
        if (info.Count < MaxFailedAttempts) return false;

        remainingMinutes = (int)Math.Ceiling((info.LockedUntil - DateTime.UtcNow).TotalMinutes);
        return remainingMinutes > 0;
    }

    private void RecordFailedAttempt(string cacheKey)
    {
        var info = cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LockoutDuration;
            return new LoginAttemptInfo();
        })!;

        info.Count++;
        if (info.Count >= MaxFailedAttempts)
            info.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);

        cache.Set(cacheKey, info, LockoutDuration);
    }

    private void ResetFailedAttempts(string cacheKey) => cache.Remove(cacheKey);

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // ── Redirect helpers ──────────────────────────────────────────────────

    private IActionResult? RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : null;

    /// <summary>
    /// Redirect ke halaman home sesuai role.
    /// Admin diarahkan ke Loket Kepegawaian sebagai default landing page.
    /// </summary>
    private IActionResult RedirectByRole(string role) => role switch
    {
        AppRoles.Loket               => Redirect("/petugas-loket"),
        AppRoles.LoketUmum           => Redirect("/loket-umum"),
        AppRoles.Kepegawaian         => Redirect("/kepegawaian"),
        AppRoles.KasubkelKepegawaian => Redirect("/kasubkel-kepegawaian"),
        AppRoles.KasubkelUmum        => Redirect("/kasubkel-umum"),
        AppRoles.KDI                 => Redirect("/kdi"),
        AppRoles.KasubkelKDI         => Redirect("/kasubkel-kdi"),
        AppRoles.ProdusenData        => Redirect("/produsen-data"),
        AppRoles.Admin               => Redirect("/petugas-loket"),
        _                            => Redirect("/petugas-loket")
    };

    // ── Inner types ───────────────────────────────────────────────────────

    private sealed class LoginAttemptInfo
    {
        public int      Count       { get; set; }
        public DateTime LockedUntil { get; set; }
    }
}
