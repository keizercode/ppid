using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

// ════════════════════════════════════════════════════════════════════════════
// KASUBKEL UMUM CONTROLLER
// Route  : /kasubkel-umum
// Role   : KasubkelUmum, Admin
// Scope  : Verifikasi form identifikasi untuk permohonan Loket Umum
// ════════════════════════════════════════════════════════════════════════════

[Route("kasubkel-umum")]
[Authorize(Roles = "KasubkelUmum,Admin")]
public class KasubkelUmumController(AppDbContext db) : Controller
{
    private string CurrentUser => User.Identity?.Name ?? "KasubkelUmum";

    // ── Dashboard ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Where(p => p.LoketJenis == LoketJenis.Umum)
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        var vm = new DashboardVm
        {
            Total        = allStatus.Count,
            Proses       = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai      = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };

        var menunggu = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Umum &&
                       (p.StatusPPIDID == StatusId.MenungguVerifikasi ||
                        p.StatusPPIDID == StatusId.IdentifikasiAwal   ||
                        p.StatusPPIDID == StatusId.MenungguSuratIzin))
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();

        ViewData["MenungguVerifikasi"] = menunggu;
        return View(vm);
    }

    // ── List Permohonan ───────────────────────────────────────────────────

    [HttpGet("permohonan")]
    public async Task<IActionResult> Permohonan(string? q, int? status)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Umum)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["Q"]     = q;
        ViewData["Status"]= status;
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── Verifikasi ────────────────────────────────────────────────────────

    [HttpGet("verifikasi/{id}")]
    public async Task<IActionResult> Verifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p == null) return NotFound();

        if (p.StatusPPIDID != StatusId.MenungguVerifikasi &&
            p.StatusPPIDID != StatusId.IdentifikasiAwal   &&
            p.StatusPPIDID != StatusId.MenungguSuratIzin)
        {
            TempData["Error"] = "Permohonan ini tidak dalam status yang memungkinkan verifikasi.";
            return RedirectToAction("Index");
        }

        return View(new VerifikasiVm
        {
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan ?? "",
            NamaPemohon       = p.Pribadi?.Nama ?? "",
            Kategori          = p.KategoriPemohon ?? "",
            JudulPenelitian   = p.JudulPenelitian ?? "",
            LatarBelakang     = p.LatarBelakang ?? "",
            IsObservasi       = p.IsObservasi,
            IsPermintaanData  = p.IsPermintaanData,
            IsWawancara       = p.IsWawancara,
            NamaBidang        = p.NamaBidang ?? "",
        });
    }

    [HttpPost("verifikasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifikasiPost(VerifikasiVm vm)
    {
        if (!ModelState.IsValid) return View("Verifikasi", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var lama = p.StatusPPIDID;
        var now  = DateTime.UtcNow;

        if (vm.Disetujui)
        {
            p.StatusPPIDID = StatusId.MenungguSuratIzin;
            p.UpdatedAt    = now;
            if (vm.DisposisiUnit == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangDisposisi))
                p.NamaBidang = vm.NamaBidangDisposisi;

            db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.MenungguSuratIzin,
                $"Verifikasi disetujui Kasubkel Umum. Disposisi: {vm.DisposisiUnit}. Catatan: {vm.CatatanVerifikasi}",
                CurrentUser);

            TempData["Success"] = $"Permohonan <strong>{vm.NoPermohonan}</strong> diverifikasi dan diteruskan ke Kepegawaian.";
        }
        else
        {
            p.StatusPPIDID = StatusId.IdentifikasiAwal;
            p.UpdatedAt    = now;
            db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.IdentifikasiAwal,
                $"Verifikasi DITOLAK Kasubkel Umum. Alasan: {vm.AlasanDitolak}", CurrentUser);
            TempData["Error"] = $"Permohonan <strong>{vm.NoPermohonan}</strong> dikembalikan. Alasan: {vm.AlasanDitolak}";
        }

        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    // ── Detail ────────────────────────────────────────────────────────────

    [HttpGet("detail/{id}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen).ThenInclude(d => d.JenisDokumen)
            .Include(x => x.AuditLog)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        var subTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();
        ViewData["SubTasks"] = subTasks;
        return View(p);
    }
}
