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

    public override void OnActionExecuting(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        ViewData["NotifApiUrl"]  = "/kasubkel-kepegawaian/notifikasi-json";
        ViewData["NotifPageUrl"] = "/kasubkel-kepegawaian/notifikasi";
    }

    [HttpGet("notifikasi")]
    public IActionResult Notifikasi()
    {
        ViewData["Title"] = "Semua Notifikasi";
        return View("~/Views/Shared/Notifikasi.cshtml");
    }

    [HttpGet("notifikasi-json")]
    public async Task<IActionResult> NotifikasiJson()
{
    var today = DateOnly.FromDateTime(DateTime.Today);

    // ── 1. Permohonan menunggu verifikasi (aksi utama Kasubkel) ──────────
    var menungguVerif = await db.PermohonanPPID
        .Include(p => p.Pribadi)
        .Where(p =>
            (p.LoketJenis == LoketJenis.Kepegawaian ||
             p.LoketJenis == LoketJenis.Umum        ||
             p.KategoriPemohon == "Mahasiswa")       &&
            (p.StatusPPIDID == StatusId.MenungguVerifikasi ||
             p.StatusPPIDID == StatusId.IdentifikasiAwal))
        .OrderBy(p => p.CratedAt)
        .Take(30)
        .ToListAsync();

    // ── 2. Permohonan melewati batas waktu ───────────────────────────────
    var overdueList = await db.PermohonanPPID
        .Include(p => p.Pribadi)
        .Where(p =>
            (p.LoketJenis == LoketJenis.Kepegawaian ||
             p.LoketJenis == LoketJenis.Umum        ||
             p.KategoriPemohon == "Mahasiswa")       &&
            p.BatasWaktu.HasValue                    &&
            p.BatasWaktu < today                     &&
            p.StatusPPIDID < StatusId.Selesai        &&
            p.StatusPPIDID != StatusId.Dibatalkan)
        .OrderBy(p => p.BatasWaktu)
        .Take(20)
        .ToListAsync();

    // ── Pakai value tuple agar sorting aman (tidak ada dynamic) ─────────
    var sortable = new List<(int Priority, string DateKey, object Payload)>();

    foreach (var p in menungguVerif)
    {
        bool isUploadBaru = p.StatusPPIDID == StatusId.MenungguVerifikasi;
        sortable.Add((
            Priority: 0,
            DateKey:  p.UpdatedAt?.ToString("yyyy-MM-dd") ?? "0000-00-00",
            Payload: new
            {
                id        = $"verif_{p.PermohonanPPIDID}",
                type      = "verifikasi",
                icon      = isUploadBaru ? "📋" : "🔁",
                title     = isUploadBaru ? "Menunggu Verifikasi" : "Identifikasi Awal",
                message   = $"{p.Pribadi?.Nama ?? "—"} — {p.NoPermohonan}",
                detail    = isUploadBaru
                    ? "TTD diupload oleh Loket, siap diverifikasi"
                    : "Perlu ditinjau ulang oleh Kasubkel Kepegawaian",
                href      = $"/kasubkel-kepegawaian/verifikasi/{p.PermohonanPPIDID}",
                dateIso   = p.UpdatedAt?.ToString("yyyy-MM-dd"),
                dateLabel = p.UpdatedAt?.ToString("dd MMM yyyy"),
                severity  = isUploadBaru ? "warning" : "info",
                createdAt = p.UpdatedAt?.ToString("yyyy-MM-dd")
            }
        ));
    }

    foreach (var p in overdueList)
    {
        var hariLewat = (today.ToDateTime(TimeOnly.MinValue)
                       - p.BatasWaktu!.Value.ToDateTime(TimeOnly.MinValue)).Days;
        sortable.Add((
            Priority: 1,
            DateKey:  p.BatasWaktu?.ToString("yyyy-MM-dd") ?? "0000-00-00",
            Payload: new
            {
                id        = $"overdue_{p.PermohonanPPIDID}",
                type      = "overdue",
                icon      = "⏰",
                title     = "Batas Waktu Terlewat",
                message   = $"{p.Pribadi?.Nama ?? "—"} — {p.NoPermohonan}",
                detail    = $"Lewat {hariLewat} hari (batas: {p.BatasWaktu?.ToString("dd MMM yyyy")})",
                href      = $"/kasubkel-kepegawaian/detail/{p.PermohonanPPIDID}",
                dateIso   = p.BatasWaktu?.ToString("yyyy-MM-dd"),
                dateLabel = p.BatasWaktu?.ToString("dd MMM yyyy"),
                severity  = "danger",
                createdAt = p.BatasWaktu?.ToString("yyyy-MM-dd")
            }
        ));
    }

    var ordered = sortable
        .OrderBy(x => x.Priority)
        .ThenBy(x => x.DateKey)
        .Select(x => x.Payload)
        .ToList();

    return Json(ordered);
}

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
        if (vm.DisposisiUnits.Count == 0)
        {
            ModelState.AddModelError("DisposisiUnits", "Pilih minimal satu unit disposisi.");
            return View("Verifikasi", vm);
        }

        p.StatusPPIDID = StatusId.MenungguSuratIzin;
        p.UpdatedAt    = now;

        // Simpan seluruh disposisi sebagai pipe-separated string
        p.NamaBidang = vm.DisposisiNamaGabung;

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguSuratIzin,
            $"Verifikasi disetujui. Disposisi: {p.NamaBidang}. Catatan: {vm.CatatanVerifikasi}",
            CurrentUser);

        TempData["Success"] =
            $"Permohonan <strong>{vm.NoPermohonan}</strong> diverifikasi — siap surat izin. " +
            $"Disposisi ke: {p.NamaBidang}.";
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
        // DisposisiUnits dibiarkan kosong — diisi user lewat form
    };
}
