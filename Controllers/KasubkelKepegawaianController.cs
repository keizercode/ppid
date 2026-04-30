using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Kasubkel Kepegawaian — verifikasi identifikasi, penerbitan surat izin,
/// dan disposisi ke unit terkait.
///
/// Lingkup tanggung jawab (setelah refactor):
///   ✓ Verifikasi form identifikasi awal (dari Loket)
///   ✓ Terbitkan surat izin + routing ke KDI/Loket
///   ✓ Buat sub-tugas Obs/Waw — dieksekusi oleh Loket Kepegawaian
///   ✗ Manajemen jadwal Obs/Waw dipindah ke PetugasLoketController
///
/// Cakupan permohonan (semua loket):
///   - LoketJenis.Kepegawaian  (prefix MHS)
///   - LoketJenis.Umum         (prefix UMM)
///   - KategoriPemohon = "Mahasiswa" (legacy fallback)
/// </summary>
[Route("kasubkel-kepegawaian")]
[Authorize(Roles = $"{AppRoles.KasubkelKepegawaian},{AppRoles.Admin}")]
public class KasubkelKepegawaianController(
    AppDbContext db,
    IWebHostEnvironment env) : LoketBaseController(db, env)
{
    private string CurrentUser =>
        User.Identity?.Name ?? AppRoles.KasubkelKepegawaian;

    private static bool IsInScope(PermohonanPPID p) =>
        p.LoketJenis == LoketJenis.Kepegawaian ||
        p.LoketJenis == LoketJenis.Umum        ||
        p.KategoriPemohon == "Mahasiswa";

    // ── Dashboard ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Where(p => p.LoketJenis == LoketJenis.Kepegawaian
                     || p.LoketJenis == LoketJenis.Umum
                     || p.KategoriPemohon == "Mahasiswa")
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        var vm = new DashboardVm
        {
            Total        = allStatus.Count,
            Proses       = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai      = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };

        // ── Antrian yang memerlukan tindakan Kasubkel ─────────────────────
        // Hanya verifikasi dan penerbitan surat izin — Obs/Waw dikelola Loket
        ViewData["MenungguVerifikasi"] = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p =>
                (p.LoketJenis == LoketJenis.Kepegawaian ||
                 p.LoketJenis == LoketJenis.Umum        ||
                 p.KategoriPemohon == "Mahasiswa")
                &&
                (p.StatusPPIDID == StatusId.MenungguVerifikasi ||
                 p.StatusPPIDID == StatusId.IdentifikasiAwal   ||
                 p.StatusPPIDID == StatusId.MenungguSuratIzin))
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();

        return View(vm);
    }

    // ── Daftar permohonan ─────────────────────────────────────────────────

    [HttpGet("permohonan")]
    public async Task<IActionResult> Permohonan(string? q, int? status)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Kepegawaian
                     || p.LoketJenis == LoketJenis.Umum
                     || p.KategoriPemohon == "Mahasiswa")
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["Q"]      = q;
        ViewData["Status"] = status;
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── Detail ────────────────────────────────────────────────────────────

    [HttpGet("detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen).ThenInclude(d => d.JenisDokumen)
            .Include(x => x.AuditLog)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        ViewData["SubTasks"] = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        return View(p);
    }

    // ── Verifikasi identifikasi awal ──────────────────────────────────────

    [HttpGet("verifikasi/{id:guid}")]
    public async Task<IActionResult> Verifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (!IsVerifikasiAllowed(p.StatusPPIDID))
        {
            TempData["Error"] = "Permohonan ini tidak dalam status yang dapat diverifikasi.";
            return RedirectToAction(nameof(Index));
        }

        return View(BuildVerifikasiVm(p));
    }

    [HttpPost("verifikasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifikasiPost(VerifikasiVm vm)
    {
        if (!ModelState.IsValid) return View("Verifikasi", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var statusLama = p.StatusPPIDID;
        var now        = DateTime.UtcNow;

        if (vm.Disetujui)
        {
            p.StatusPPIDID = StatusId.MenungguSuratIzin;
            p.UpdatedAt    = now;

            // Simpan unit disposisi — termasuk PSMDI
            p.NamaBidang = vm.DisposisiUnit == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangDisposisi)
                ? vm.NamaBidangDisposisi
                : vm.DisposisiUnit;

            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguSuratIzin,
            $"Verifikasi disetujui. Disposisi: {p.NamaBidang}. Catatan: {vm.CatatanVerifikasi}",
            CurrentUser);

            TempData["Success"] =
                $"Permohonan <strong>{vm.NoPermohonan}</strong> diverifikasi — siap surat izin.";
        }
        else
        {
            p.StatusPPIDID = StatusId.IdentifikasiAwal;
            p.UpdatedAt    = now;

            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.IdentifikasiAwal,
                $"Verifikasi DITOLAK. Alasan: {vm.AlasanDitolak}", CurrentUser);

            TempData["Error"] =
                $"Permohonan <strong>{vm.NoPermohonan}</strong> dikembalikan. Alasan: {vm.AlasanDitolak}";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ── Surat Izin ────────────────────────────────────────────────────────
// ── Surat Izin — DIKELOLA OLEH LOKET KEPEGAWAIAN ─────────────────────
// Penerbitan surat izin dipindahkan sepenuhnya ke PetugasLoketController.
// Kasubkel hanya bertugas verifikasi identifikasi awal.
// Route ini sengaja dikembalikan sebagai redirect agar tidak bisa diakses.

[HttpGet("surat-izin/{id:guid}")]
public IActionResult SuratIzin(Guid id)
{
    TempData["Error"] =
        "Penerbitan surat izin dilakukan oleh <strong>Loket Kepegawaian</strong>. " +
        "Informasikan ke petugas loket untuk melanjutkan proses.";
    return RedirectToAction(nameof(Index));
}

    // ── Hasil Feedback (view-only) ────────────────────────────────────────

    [HttpGet("feedback/{id:guid}")]
    public async Task<IActionResult> HasilFeedback(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        var feedbacks = await db.FeedbackTaskPPID
            .Where(f => f.PermohonanPPIDID == id)
            .ToListAsync();

        var subTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        var tugasDocs = await db.DokumenPPID
            .Where(d => d.PermohonanPPIDID == id &&
                        d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        ViewData["TugasDocs"] = tugasDocs;

        return View(new HasilFeedbackVm
        {
            Permohonan = p,
            Feedbacks  = feedbacks,
            SubTasks   = subTasks,
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static bool IsVerifikasiAllowed(int? statusId) =>
        statusId is StatusId.MenungguVerifikasi
                 or StatusId.IdentifikasiAwal
                 or StatusId.MenungguSuratIzin;

    private static VerifikasiVm BuildVerifikasiVm(PermohonanPPID p) => new()
    {
        PermohonanPPIDID = p.PermohonanPPIDID,
        NoPermohonan     = p.NoPermohonan    ?? string.Empty,
        NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
        Kategori         = p.KategoriPemohon ?? string.Empty,
        JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
        LatarBelakang    = p.LatarBelakang   ?? string.Empty,
        IsObservasi      = p.IsObservasi,
        IsPermintaanData = p.IsPermintaanData,
        IsWawancara      = p.IsWawancara,
        NamaBidang       = p.NamaBidang      ?? string.Empty,
    };
}
