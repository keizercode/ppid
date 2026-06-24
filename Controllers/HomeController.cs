using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Helpers;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

public class HomeController(
    AppDbContext db,
    ILogger<HomeController> logger,
    IWebHostEnvironment env) : Controller
{
    public IActionResult Index(string? noPermohonan, string? anchor)
{
    if (anchor == "lacak")
        ViewData["ScrollTo"] = "lacak";
    return View(new LacakViewModel { NoPermohonan = noPermohonan ?? string.Empty });
}

    // ════════════════════════════════════════════════════════════════════════
    // LACAK
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Lacak(string? noPermohonan)
    {
        if (string.IsNullOrEmpty(noPermohonan))
            return View("Index", new LacakViewModel());

        noPermohonan = noPermohonan.Trim().ToUpperInvariant();

        var permohonan = await db.PermohonanPPID
            .Include(p => p.Status)
            .Include(p => p.Detail).ThenInclude(d => d.Keperluan)
            .Include(p => p.Dokumen)
            .Include(p => p.Jadwal)
            .FirstOrDefaultAsync(p => p.NoPermohonan == noPermohonan);

        if (permohonan == null)
        {
            TempData["Error"] = "Nomor permohonan tidak ditemukan. "
                            + "Pastikan nomor diketik persis seperti yang tertera di formulir Anda.";
            return RedirectToAction("Index", new { anchor = "lacak", noPermohonan });
        }

        var pribadi = await db.Pribadi
            .Include(p => p.PribadiPPID)
            .FirstOrDefaultAsync(p => p.PribadiID == permohonan.PribadiID);

        var subTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == permohonan.PermohonanPPIDID)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        var jadwalAktif = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == permohonan.PermohonanPPIDID && j.IsAktif)
            .ToListAsync();

        var feedbacks = await db.FeedbackTaskPPID
            .AsNoTracking()
            .Where(f => f.PermohonanPPIDID == permohonan.PermohonanPPIDID)
            .ToListAsync();

        ViewData["FeedbackMap"] = feedbacks
            .GroupBy(f => f.JenisTask)
            .ToDictionary(g => g.Key, g => true);

        var subTaskLastUpdate = subTasks.Any()
            ? subTasks.Max(t => t.UpdatedAt ?? t.CreatedAt)
            : (DateTime?)null;
        var jadwalLastUpdate  = jadwalAktif.Any()
            ? jadwalAktif.Max(j => j.UpdatedAt ?? j.CreatedAt ?? DateTime.MinValue)
            : (DateTime?)null;

        var vm = new DetailLacakViewModel
        {
            Permohonan      = permohonan,
            Pribadi         = pribadi!,
            PribadiPPID     = pribadi?.PribadiPPID,
            Detail          = permohonan.Detail.ToList(),
            Jadwal          = permohonan.Jadwal.OrderBy(j => j.Tanggal).ToList(),
            Riwayat         = BuildRiwayat(permohonan),
            SubTasks        = subTasks,
            JadwalAktif     = jadwalAktif,
            LastChangedAt   = new[] {
                permohonan.UpdatedAt,
                subTaskLastUpdate,
                jadwalLastUpdate
            }.Where(d => d.HasValue).Select(d => d!.Value).DefaultIfEmpty(DateTime.MinValue).Max()
        };

        return View("Detail", vm);
    }

    [HttpPost("lacak")]
    [ValidateAntiForgeryToken]
    public IActionResult LacakPost(LacakViewModel model)
    {
        if (!ModelState.IsValid) return View("Index", model);
        return RedirectToAction("Lacak", new
        {
            noPermohonan = model.NoPermohonan.Trim().ToUpperInvariant()
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // CEK STATUS (REALTIME POLLING API)
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("cek-status")]
public async Task<IActionResult> CekStatus([FromQuery] string? no)
{
    if (string.IsNullOrEmpty(no)) return Json(null);

    no = no.Trim().ToUpperInvariant();

    // Validasi format dasar: MHS/UMM + 6 digit + /PPID/ + angka romawi + / + tahun
    // Contoh: MHS123456/PPID/III/2026 — max 30 karakter
    if (no.Length > 35 || no.Length < 15
        || (!no.StartsWith("MHS", StringComparison.Ordinal)
            && !no.StartsWith("UMM", StringComparison.Ordinal)))
    {
        return Json(null);
    }

        var p = await db.PermohonanPPID
            .Where(x => x.NoPermohonan == no)
            .Select(x => new
            {
                x.StatusPPIDID,
                x.UpdatedAt,
                x.PermohonanPPIDID
            })
            .FirstOrDefaultAsync();

        if (p is null) return Json(null);

        var stUpdate = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == p.PermohonanPPIDID)
            .Select(t => (DateTime?)t.UpdatedAt)
            .MaxAsync() ?? (DateTime?)null;

        var jadUpdate = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == p.PermohonanPPIDID && j.IsAktif)
            .Select(j => j.UpdatedAt ?? j.CreatedAt)
            .MaxAsync();

        var allDates = new[] { p.UpdatedAt, stUpdate, jadUpdate }
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        return Json(new
        {
            statusId      = p.StatusPPIDID,
            lastChangedAt = allDates.ToString("O")
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // ERROR HANDLER
    // ════════════════════════════════════════════════════════════════════════

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = 500;

        var exFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var ex        = exFeature?.Error;
        var path      = exFeature?.Path ?? HttpContext.Request.Path;

        if (ex is not null)
        {
            logger.LogError(ex,
                "Unhandled exception | Path: {Path} | User: {User} | IP: {IP}",
                path,
                User.Identity?.Name ?? "anonymous",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        if (env.IsDevelopment() && ex is not null)
        {
            var detail = $"""
                [DEV MODE — tidak ditampilkan di production]

                Exception: {ex.GetType().FullName}
                Message  : {ex.Message}
                Path     : {path}
                Time     : {DateTime.UtcNow:O}

                Stack Trace:
                {ex.StackTrace}

                Inner Exception:
                {ex.InnerException?.Message ?? "(none)"}
                """;

            return Content(detail, "text/plain; charset=utf-8");
        }

        return Content(
            "Terjadi kesalahan pada sistem. " +
            "Silakan kembali ke halaman sebelumnya dan coba lagi. " +
            $"Jika masalah berlanjut, hubungi administrator. " +
            $"(Ref: {DateTime.UtcNow:yyyyMMddHHmmss})",
            "text/plain; charset=utf-8");
    }

    // ════════════════════════════════════════════════════════════════════════
    // TIMELINE BUILDER
    // ════════════════════════════════════════════════════════════════════════

    private static List<RiwayatStatusVm> BuildRiwayat(PermohonanPPID p)
    {
        var current    = p.StatusPPIDID ?? StatusId.TerdaftarSistem;
        var steps      = GetSteps(p);
        int currentIdx = steps.FindIndex(s => s.StatusId == current);

        if (currentIdx < 0) currentIdx = FindNearestIdx(steps, current);

        return steps.Select((s, i) => new RiwayatStatusVm
        {
            StatusId      = s.StatusId,
            Label         = s.Label,
            SubLabel      = s.SubLabel,
            Selesai       = i < currentIdx,
            AktifSekarang = i == currentIdx
        }).ToList();
    }

    // metode GetSteps
private static List<(int StatusId, string Label, string? SubLabel)> GetSteps(PermohonanPPID p)
{
    var keperluanList = new List<string>();
    if (p.IsPermintaanData) keperluanList.Add("Permintaan Data");
    if (p.IsObservasi)      keperluanList.Add("Observasi");
    if (p.IsWawancara)      keperluanList.Add("Wawancara");
    string? keperluanSub = keperluanList.Count > 0 ? string.Join(" + ", keperluanList) : null;

    var steps = new List<(int, string, string?)>
    {
        (StatusId.TerdaftarSistem,    "1. Permohonan Terdaftar",                 null),
        (StatusId.IdentifikasiAwal,   "2. Tanda Tangan Identifikasi Awal",       null),
        (StatusId.MenungguVerifikasi, "3. Verifikasi Kasubkel & Disposisi Unit", null),
        (StatusId.MenungguSuratIzin,  "4. Pembuatan Surat Izin",                 null),
        (StatusId.SuratIzinTerbit,    "5. Surat Izin Terbit",                    null),
        (StatusId.Didisposisi,        "6. Pemrosesan Data & Penjadwalan",        keperluanSub),
    };

    if (!PermohonanRules.IsLsm(p))
        steps.Add((StatusId.FeedbackPemohon, "7. Unggah Hasil Laporan & Isi Feedback", null));

    steps.Add((StatusId.Selesai,
        PermohonanRules.IsLsm(p) ? "7. Selesai" : "8. Selesai",
        null));

    return steps;
}

    // metode GetWorkflowOrder
    private static int GetWorkflowOrder(int statusId) => statusId switch
    {
        StatusId.TerdaftarSistem                                              => 1,
        StatusId.IdentifikasiAwal                                             => 2,
        StatusId.MenungguVerifikasi or StatusId.MenungguSuratIzin             => 3,
        StatusId.SuratIzinTerbit                                              => 4,
        StatusId.Didisposisi or StatusId.DiProses
            or StatusId.ObservasiDijadwalkan or StatusId.ObservasiSelesai
            or StatusId.WawancaraDijadwalkan or StatusId.WawancaraSelesai    => 5,
        // DataSiap & FeedbackPemohon → keduanya mengaktifkan step 7 "Unggah Laporan & Isi Feedback"
        StatusId.DataSiap or StatusId.FeedbackPemohon                        => 6,
        StatusId.Selesai                                                      => 7,
        _                                                                     => 0
    };

    private static int FindNearestIdx(
        List<(int StatusId, string Label, string? SubLabel)> steps,
        int current)
    {
        int currentOrder = GetWorkflowOrder(current);
        int best         = -1;

        for (int i = 0; i < steps.Count; i++)
        {
            if (GetWorkflowOrder(steps[i].StatusId) <= currentOrder)
                best = i;
        }

        return best >= 0 ? best : steps.Count - 1;
    }

    // ════════════════════════════════════════════════════════════════════════
    // UPLOAD TUGAS / LAPORAN FINAL PEMOHON
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("upload-tugas/{id:guid}")]
    public async Task<IActionResult> UploadTugas(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Dokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (PermohonanRules.IsLsm(p))
        {
            TempData["Error"] = "Permohonan LSM tidak memerlukan unggah laporan hasil penelitian.";
            return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
        }

        if ((p.StatusPPIDID ?? 0) < StatusId.Didisposisi)
        {
            TempData["Error"] = "Laporan hanya dapat diunggah setelah permohonan mulai diproses.";
            return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
        }

        var uploaded = p.Dokumen
            .Where(d => d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        return View(new UploadTugasVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
            FilesUploaded    = uploaded,
        });
    }

    [HttpPost("upload-tugas"), ValidateAntiForgeryToken]
public async Task<IActionResult> UploadTugasPost(UploadTugasVm vm)
{
    if (vm.FileTugas == null || vm.FileTugas.Length == 0)
        ModelState.AddModelError(nameof(vm.FileTugas), "File wajib dipilih.");

    if (vm.FileTugas != null && vm.FileTugas.Length > 0)
    {
        var valTugas = Services.FileValidator.ValidateDataFile(vm.FileTugas);
        if (!valTugas.IsValid)
            ModelState.AddModelError(nameof(vm.FileTugas), valTugas.ErrorMessage!);
    }

    if (!ModelState.IsValid)
    {
        var pReload = await db.PermohonanPPID
            .Include(x => x.Dokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == vm.PermohonanPPIDID);
        vm.FilesUploaded = pReload?.Dokumen
            .Where(d => d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToList() ?? [];
        return View("UploadTugas", vm);
    }

    var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
    if (p is null) return NotFound();

    if ((p.StatusPPIDID ?? 0) < StatusId.Didisposisi)
    {
        TempData["Error"] = "Upload tidak diizinkan pada status ini.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    var now = DateTime.UtcNow;
    var fn  = $"tugas_{now:yyyyMMddHHmmss}_{Services.FileValidator.SanitizeFileName(vm.FileTugas!.FileName)}";

    var finalDir  = Path.Combine(
        string.IsNullOrEmpty(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath,
        "uploads", vm.PermohonanPPIDID.ToString());

    var tempPath  = Path.Combine(Path.GetTempPath(), $"ppid_tugas_{Guid.NewGuid()}_{fn}");
    var finalPath = Path.Combine(finalDir, fn);
    var fp        = $"/uploads/{vm.PermohonanPPIDID}/{fn}";

    // Tulis ke temp path dulu
    await using (var s = new FileStream(tempPath, FileMode.Create))
        await vm.FileTugas.CopyToAsync(s);

    db.DokumenPPID.Add(new DokumenPPID
    {
        PermohonanPPIDID     = vm.PermohonanPPIDID,
        NamaDokumenPPID      = $"Laporan/Tugas Final — {vm.FileTugas.FileName}",
        UploadDokumenPPID    = fp,
        JenisDokumenPPIDID   = JenisDokumenId.TugasFinal,
        NamaJenisDokumenPPID = "Tugas / Laporan Final",
        CreatedAt            = now
    });

    db.AuditLog.Add(new AuditLogPPID
    {
        PermohonanPPIDID = vm.PermohonanPPIDID,
        StatusLama       = p.StatusPPIDID,
        StatusBaru       = p.StatusPPIDID ?? StatusId.DataSiap,
        Keterangan       = $"Pemohon mengunggah laporan/tugas final: {vm.FileTugas.FileName}. Catatan: {vm.Catatan ?? "(kosong)"}",
        Operator         = "Pemohon",
        CreatedAt        = now
    });

    try
    {
        await db.SaveChangesAsync();
    }
    catch
    {
        try { System.IO.File.Delete(tempPath); } catch { /* best-effort */ }
        throw;
    }

    // Pindah ke final hanya setelah DB berhasil
    try
    {
        Directory.CreateDirectory(finalDir);
        System.IO.File.Move(tempPath, finalPath, overwrite: true);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Gagal pindah file tugas dari temp ke final. Temp={T} Final={F}",
            tempPath, finalPath);
    }

    TempData["SuccessTugas"] = "Laporan berhasil diunggah! Terima kasih telah menyelesaikan penelitian Anda.";
    return RedirectToAction("Lacak", new { noPermohonan = vm.NoPermohonan });
}

    // ═══════════════════════════════════════════════════════════════════════
// DOWNLOAD TEMPLATE LAPORAN
// ═══════════════════════════════════════════════════════════════════════

[HttpGet("download-template/{id:guid}")]
public async Task<IActionResult> DownloadTemplate(Guid id)
{
    var p = await db.PermohonanPPID
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
    if (p is null) return NotFound();

    var templatePath = Path.Combine(
        string.IsNullOrEmpty(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath,
        "templates", "template-laporan-penelitian.docx");

    if (!System.IO.File.Exists(templatePath))
    {
        TempData["Error"] = "File template belum tersedia. Hubungi administrator.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    return PhysicalFile(templatePath,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "Template-Laporan-Penelitian.docx");
}

// ═══════════════════════════════════════════════════════════════════════
// UPLOAD LAPORAN UNIFIED
// Menggantikan UploadTugas + UploadDokumentasiTask.
// Tersedia saat DataSiap; setelah upload → auto-advance ke FeedbackPemohon.
// ═══════════════════════════════════════════════════════════════════════

[HttpGet("upload-laporan/{id:guid}")]
public async Task<IActionResult> UploadLaporan(Guid id)
{
    // Dialihkan ke halaman Feedback terpadu
    var p = await db.PermohonanPPID.FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
    if (p is null) return NotFound();
    return RedirectToAction("Feedback", new { id });
}

[HttpPost("upload-laporan"), ValidateAntiForgeryToken]
public async Task<IActionResult> UploadLaporanPost(UploadLaporanUnifiedVm vm)
{
    // Backward-compat: arahkan ke Feedback
    return RedirectToAction("Feedback", new { id = vm.PermohonanPPIDID });
}

// ═══════════════════════════════════════════════════════════════════════
// FEEDBACK UNIFIED
// Satu feedback untuk semua keperluan; tersedia setelah laporan diunggah.
// Submit → otomatis Selesai.
// ═══════════════════════════════════════════════════════════════════════

// REPLACE action Feedback GET
[HttpGet("feedback/{id:guid}")]
public async Task<IActionResult> Feedback(Guid id)
{
    var p = await db.PermohonanPPID
        .Include(x => x.Pribadi)
        .Include(x => x.Dokumen)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
    if (p is null) return NotFound();

    if (PermohonanRules.IsLsm(p))
    {
        TempData["Error"] = "Permohonan LSM tidak memerlukan pengisian feedback.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    // Buka feedback di DataSiap DAN FeedbackPemohon
    if (p.StatusPPIDID != StatusId.FeedbackPemohon
     && p.StatusPPIDID != StatusId.DataSiap)
    {
        TempData["Error"] = p.StatusPPIDID == StatusId.Selesai
            ? "Permohonan sudah selesai. Feedback tidak dapat diubah."
            : "Feedback belum dapat diisi saat ini.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    bool laporanAda = p.Dokumen.Any(d => d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal);

    var existing = await db.FeedbackTaskPPID
        .FirstOrDefaultAsync(f => f.PermohonanPPIDID == id && f.JenisTask == JenisTask.Semua);

    var keperluans = new List<string>();
    if (p.IsPermintaanData) keperluans.Add("Permintaan Data");
    if (p.IsObservasi)      keperluans.Add("Observasi");
    if (p.IsWawancara)      keperluans.Add("Wawancara");

    return View(new FeedbackUnifiedVm
    {
        PermohonanPPIDID     = id,
        NoPermohonan         = p.NoPermohonan    ?? string.Empty,
        NamaPemohon          = p.Pribadi?.Nama   ?? string.Empty,
        JudulPenelitian      = p.JudulPenelitian ?? string.Empty,
        Keperluans           = keperluans,
        LaporanSudahDiunggah = laporanAda,
        SudahDiisi           = existing is not null,
        NilaiLama            = existing?.NilaiKepuasan ?? 0,
        NilaiKepuasan        = existing?.NilaiKepuasan ?? 0,
        CatatanLama          = existing?.Catatan,
    });
}

[HttpPost("feedback"), ValidateAntiForgeryToken]
public async Task<IActionResult> FeedbackPost(FeedbackUnifiedVm vm)
{
    var now = DateTime.UtcNow;

    bool laporanAda = await db.DokumenPPID.AnyAsync(d =>
        d.PermohonanPPIDID   == vm.PermohonanPPIDID &&
        d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal);

    if (!laporanAda && (vm.FileLaporan == null || vm.FileLaporan.Length == 0))
        ModelState.AddModelError(nameof(vm.FileLaporan),
            "File laporan wajib diunggah sebelum mengisi feedback.");

    if (vm.FileLaporan != null && vm.FileLaporan.Length > 0)
    {
        var val = Services.FileValidator.ValidateDataFile(vm.FileLaporan);
        if (!val.IsValid)
            ModelState.AddModelError(nameof(vm.FileLaporan), val.ErrorMessage!);
    }

    if (string.IsNullOrWhiteSpace(vm.Catatan))
        ModelState.AddModelError(nameof(vm.Catatan), "Masukan / saran wajib diisi.");

    if (!ModelState.IsValid)
    {
        vm.LaporanSudahDiunggah = laporanAda;
        return View("Feedback", vm);
    }

    var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
    if (p is null) return NotFound();

    // ── P-04 FIX: Tulis file ke temp path terlebih dahulu ────────────────
    // File hanya dipindahkan ke final path SETELAH SaveChangesAsync berhasil.
    // Ini mencegah file orphan di disk jika transaksi DB gagal.
    string? tempFilePath  = null;
    string? finalFilePath = null;
    string? finalDir      = null;
    string? webRelPath    = null;
    string? namaFile      = null;

    if (vm.FileLaporan != null && vm.FileLaporan.Length > 0)
    {
        var fn = $"laporan_{now:yyyyMMddHHmmss}_{Services.FileValidator.SanitizeFileName(vm.FileLaporan.FileName)}";

        finalDir     = Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads", vm.PermohonanPPIDID.ToString());

        tempFilePath  = Path.Combine(Path.GetTempPath(), $"ppid_laporan_{Guid.NewGuid()}_{fn}");
        finalFilePath = Path.Combine(finalDir, fn);
        webRelPath    = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
        namaFile      = vm.FileLaporan.FileName;

        await using var s = new FileStream(tempFilePath, FileMode.Create);
        await vm.FileLaporan.CopyToAsync(s);
    }

    // ── Daftarkan dokumen ke DbContext (belum commit) ─────────────────────
    if (tempFilePath is not null)
    {
        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID     = vm.PermohonanPPIDID,
            NamaDokumenPPID      = $"Laporan Hasil Penelitian — {vm.FileLaporan!.FileName}",
            UploadDokumenPPID    = webRelPath,
            JenisDokumenPPIDID   = JenisDokumenId.TugasFinal,
            NamaJenisDokumenPPID = "Laporan Final Pemohon",
            CreatedAt            = now
        });

        db.AuditLog.Add(new AuditLogPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            StatusLama       = p.StatusPPIDID,
            StatusBaru       = p.StatusPPIDID ?? StatusId.DataSiap,
            Keterangan       = $"Pemohon mengunggah laporan hasil penelitian: {namaFile}.",
            Operator         = "Pemohon",
            CreatedAt        = now
        });
    }

    // ── Upsert feedback ───────────────────────────────────────────────────
    var existing = await db.FeedbackTaskPPID
        .FirstOrDefaultAsync(f => f.PermohonanPPIDID == vm.PermohonanPPIDID
                               && f.JenisTask        == JenisTask.Semua);

    if (existing is null)
    {
        db.FeedbackTaskPPID.Add(new FeedbackTaskPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisTask        = JenisTask.Semua,
            NilaiKepuasan    = vm.NilaiKepuasan,
            Catatan          = vm.Catatan,
            CreatedAt        = now
        });
    }
    else
    {
        existing.NilaiKepuasan = vm.NilaiKepuasan;
        existing.Catatan       = vm.Catatan;
    }

    // ── Advance status ────────────────────────────────────────────────────
    var lama       = p.StatusPPIDID;
    p.StatusPPIDID = StatusId.FeedbackPemohon;
    p.UpdatedAt    = now;

    db.AuditLog.Add(new AuditLogPPID
    {
        PermohonanPPIDID = vm.PermohonanPPIDID,
        StatusLama       = lama,
        StatusBaru       = StatusId.FeedbackPemohon,
        Keterangan       = $"Pemohon mengunggah laporan & mengisi feedback " +
                           $"(nilai: {vm.NilaiKepuasan}/5). " +
                           "Menunggu konfirmasi selesai dari Loket Kepegawaian.",
        Operator         = "Pemohon",
        CreatedAt        = now
    });

    // ── Single SaveChanges — jika gagal, file temp belum dipindah ─────────
    try
    {
        await db.SaveChangesAsync();
    }
    catch
    {
        // DB gagal: hapus file temp agar tidak menumpuk
        if (tempFilePath is not null)
        {
            try { System.IO.File.Delete(tempFilePath); } catch { /* best-effort */ }
        }
        throw; // biarkan exception handler global menangani
    }

    // ── DB berhasil: pindahkan file dari temp ke final ────────────────────
    if (tempFilePath is not null && finalFilePath is not null && finalDir is not null)
    {
        try
        {
            Directory.CreateDirectory(finalDir);
            System.IO.File.Move(tempFilePath, finalFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // DB sudah commit — log error, jangan throw (tidak bisa rollback DB)
            // File ada di temp path, perlu penanganan manual oleh admin
            logger.LogError(ex,
                "Gagal memindahkan file laporan dari temp ke final setelah DB commit. " +
                "TempPath={Temp} FinalPath={Final} PermohonanID={Id}",
                tempFilePath, finalFilePath, vm.PermohonanPPIDID);
        }
    }

    TempData["Success"] =
        "Laporan & feedback berhasil dikirim! " +
        "Permohonan Anda akan dikonfirmasi selesai oleh <strong>petugas loket</strong>.";
    return RedirectToAction("Lacak", new { noPermohonan = vm.NoPermohonan });
}
}
