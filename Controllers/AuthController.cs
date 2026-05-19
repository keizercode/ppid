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
    private static readonly object _attemptLock = new();
    private const int MaxFailedAttempts       = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

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

        // Cek lockout sebelum DB query (baca saja, tidak perlu lock)
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
            // Atomic: increment counter dan cek lockout dalam satu operasi
            if (RecordAndCheckLockout(cacheKey, out var remaining2))
            {
                ModelState.AddModelError(string.Empty,
                    $"Terlalu banyak percobaan login. Akun dikunci {remaining2} menit.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Username atau password salah.");
            }
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

    private static IActionResult RedirectByRole(string role) => role switch
    {
        AppRoles.Loket               => new RedirectResult("/petugas-loket"),
        AppRoles.LoketUmum           => new RedirectResult("/loket-umum"),
        AppRoles.KasubkelKepegawaian => new RedirectResult("/kasubkel-kepegawaian"),
        AppRoles.KasubkelKDI         => new RedirectResult("/kasubkel-kdi"),
        AppRoles.Admin               => new RedirectResult("/petugas-loket"),
        _                            => new RedirectResult("/petugas-loket")
    };

    // ── Rate limiting (P-03 FIX) ──────────────────────────────────────────

    /// <summary>
    /// Cek apakah IP sedang dalam lockout. Thread-safe untuk read.
    /// Dipanggil SEBELUM query DB agar login yang dikunci tidak membebani DB.
    /// </summary>
    private bool IsLockedOut(string key, out int remainingMinutes)
    {
        remainingMinutes = 0;
        if (!cache.TryGetValue(key, out LoginAttemptInfo? info) || info is null)
            return false;
        if (info.Count < MaxFailedAttempts)
            return false;
        var rem = (info.LockedUntil - DateTime.UtcNow).TotalMinutes;
        if (rem <= 0) return false;
        remainingMinutes = (int)Math.Ceiling(rem);
        return true;
    }

    /// <summary>
    /// Increment counter DAN cek lockout dalam satu lock — mencegah TOCTOU.
    /// Menggantikan RecordFailedAttempt yang terpisah dari IsLockedOut.
    /// </summary>
    private bool RecordAndCheckLockout(string key, out int remainingMinutes)
    {
        remainingMinutes = 0;
        lock (_attemptLock)
        {
            var info = cache.GetOrCreate(key, e =>
            {
                e.AbsoluteExpirationRelativeToNow = LockoutDuration;
                return new LoginAttemptInfo();
            })!;

            info.Count++;
            if (info.Count >= MaxFailedAttempts)
                info.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);

            cache.Set(key, info, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = LockoutDuration
            });

            if (info.Count >= MaxFailedAttempts)
            {
                var rem = (info.LockedUntil - DateTime.UtcNow).TotalMinutes;
                remainingMinutes = (int)Math.Ceiling(Math.Max(rem, 1));
                return true;
            }
            return false;
        }
    }

    private void ResetFailedAttempts(string key) => cache.Remove(key);

    private string GetClientIp()
    {
        // Cek X-Forwarded-For terlebih dahulu (set oleh reverse proxy seperti Nginx/Cloudflare).
        // Ambil IP pertama (client asli) — bukan entry terakhir (proxy).
        // PENTING: pastikan hanya proxy terpercaya yang dapat set header ini
        //          (konfigurasi di Nginx: proxy_set_header X-Forwarded-For $remote_addr).
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(firstIp))
                return firstIp;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private IActionResult? RedirectToLocal(string? url) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? Redirect(url) : null;

    private sealed class LoginAttemptInfo
    {
        public int      Count       { get; set; }
        public DateTime LockedUntil { get; set; }
    }
}
