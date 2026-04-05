using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("kepegawaian")]
[Authorize(Roles = $"{AppRoles.Kepegawaian},{AppRoles.Admin}")]
public class KepegawaianController(AppDbContext db, IWebHostEnvironment env)
    : LoketBaseController(db, env)
{
    private string CurrentUser => User.Identity?.Name ?? AppRoles.Kepegawaian;

    // ── Dashboard ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID == StatusId.MenungguSuratIzin)
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();

        return View(list);
    }

    // ── Surat Izin ────────────────────────────────────────────────────────

    [HttpGet("surat-izin/{id:guid}")]
    public async Task<IActionResult> SuratIzin(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        return View(new SuratIzinVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            Kategori         = p.KategoriPemohon ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
            IsObservasi      = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara      = p.IsWawancara,
        });
    }

    [HttpPost("surat-izin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuratIzinPost(SuratIzinVm vm)
    {
        if (!ModelState.IsValid) return View("SuratIzin", vm);

        var now = DateTime.UtcNow;

        // Upload file surat izin jika ada
        if (vm.FileSuratIzin?.Length > 0)
        {
            var error = await UploadDokumen(vm.PermohonanPPIDID, vm.FileSuratIzin,
                JenisDokumenId.SuratIzin, "Surat Izin", now);
            if (error != null)
            {
                ModelState.AddModelError(nameof(vm.FileSuratIzin), error);
                return View("SuratIzin", vm);
            }
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;
        p.NoSuratPermohonan = vm.NoSuratIzin;
        p.UpdatedAt         = now;
        p.IsObservasi       = vm.IsObservasi;
        p.IsPermintaanData  = vm.IsPermintaanData;
        p.IsWawancara       = vm.IsWawancara;

        // ── Resolusi disposisi ────────────────────────────────────────────
        // Wawancara-only → langsung ke Produsen Data (tidak melalui KDI)
        // Selainnya      → ke KDI; NamaBidang ditentukan dari pilihan disposisi form
        if (vm.IsWawancaraOnly)
        {
            p.StatusPPIDID     = StatusId.WawancaraDijadwalkan;
            p.NamaProdusenData = vm.NamaProdusenData ?? vm.NamaBidangPrimary;
        }
        else
        {
            p.StatusPPIDID = StatusId.Didisposisi;
            // Ambil nama bidang pertama yang bukan PSMDI, jika ada
            p.NamaBidang = vm.NamaBidangPrimary;
        }

        var tujuan = vm.IsWawancaraOnly
            ? $"Surat izin {vm.NoSuratIzin} diterbitkan, diteruskan ke Produsen Data"
            : $"Surat izin {vm.NoSuratIzin} diterbitkan, didisposisi ke {p.NamaBidang ?? "PSMDI"}";

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, p.StatusPPIDID!.Value, tujuan, CurrentUser);
        await db.SaveChangesAsync();

        var keterangan = vm.IsWawancaraOnly
            ? "dan diteruskan ke Produsen Data untuk penjadwalan wawancara"
            : $"dan didisposisi ke {(string.IsNullOrEmpty(p.NamaBidang) ? "PSMDI" : p.NamaBidang)}";

        TempData["Success"] = $"Surat izin <strong>{vm.NoSuratIzin}</strong> diterbitkan {keterangan}.";
        return RedirectToAction(nameof(Index));
    }
}
