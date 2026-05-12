using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

public class HomeController(
    AppDbContext db,
    ILogger<HomeController> logger,
    IWebHostEnvironment env) : Controller
{
    public IActionResult Index() => View(new LacakViewModel());

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
            return View("Index", new LacakViewModel { NoPermohonan = noPermohonan });
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

    [HttpPost]
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

    return new List<(int, string, string?)>
    {
        (StatusId.TerdaftarSistem,    "1. Permohonan Terdaftar",                 null),
        (StatusId.IdentifikasiAwal,   "2. Tanda Tangan Identifikasi Awal",       null),
        (StatusId.MenungguVerifikasi, "3. Verifikasi Kasubkel & Disposisi Unit", null),
        (StatusId.MenungguSuratIzin,  "4. Pembuatan Surat Izin",                 null),
        (StatusId.SuratIzinTerbit,    "5. Surat Izin Terbit",                    null),
        // Step 6 mencakup pemrosesan s.d. DataSiap — tidak ada step 7 terpisah
        (StatusId.Didisposisi,        "6. Pemrosesan Data & Penjadwalan",        keperluanSub),
        // Step 7: upload laporan + feedback (dulu step 8)
        (StatusId.FeedbackPemohon,    "7. Unggah Laporan & Isi Feedback",        null),
        (StatusId.Selesai,            "8. Selesai",                              null),
    };
}

    // metode GetWorkflowOrder
    private static int GetWorkflowOrder(int statusId) => statusId switch
    {
        StatusId.TerdaftarSistem                                              => 1,
        StatusId.IdentifikasiAwal                                             => 2,
        StatusId.MenungguVerifikasi or StatusId.MenungguSuratIzin             => 3,
        StatusId.SuratIzinTerbit                                              => 4,
        // DataSiap masuk ke grup step 6 (bukan level tersendiri)
        StatusId.Didisposisi or StatusId.DiProses
            or StatusId.ObservasiDijadwalkan or StatusId.ObservasiSelesai
            or StatusId.WawancaraDijadwalkan or StatusId.WawancaraSelesai
            or StatusId.DataSiap                                              => 5,
        StatusId.FeedbackPemohon                                              => 6,
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

        if (p.StatusPPIDID < StatusId.DataSiap)
        {
            TempData["Error"] = "Laporan hanya dapat diunggah setelah data tersedia.";
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
        {
            ModelState.AddModelError(nameof(vm.FileTugas), "File wajib dipilih.");
        }

            // Validasi tipe & ukuran file (server-side — tidak bisa di-bypass)
    if (vm.FileTugas != null && vm.FileTugas.Length > 0)
    {
        var valTugas = Services.FileValidator.ValidateDocument(vm.FileTugas);
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

        if (p.StatusPPIDID < StatusId.DataSiap)
        {
            TempData["Error"] = "Upload tidak diizinkan pada status ini.";
            return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
        }

        var now = DateTime.UtcNow;

        var uploadsDir = Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads", vm.PermohonanPPIDID.ToString());
        Directory.CreateDirectory(uploadsDir);

        // UploadTugasPost
        var fn = $"tugas_{now:yyyyMMddHHmmss}_{Services.FileValidator.SanitizeFileName(vm.FileTugas!.FileName)}";
        await using (var s = new FileStream(Path.Combine(uploadsDir, fn), FileMode.Create))
            await vm.FileTugas.CopyToAsync(s);

        var fp = $"/uploads/{vm.PermohonanPPIDID}/{fn}";

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

        await db.SaveChangesAsync();

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
    var p = await db.PermohonanPPID
        .Include(x => x.Pribadi)
        .Include(x => x.Dokumen)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
    if (p is null) return NotFound();

    if (p.StatusPPIDID != StatusId.DataSiap)
    {
        TempData["Error"] = p.StatusPPIDID < StatusId.DataSiap
            ? "Laporan dapat diunggah setelah semua proses penyelesaian data selesai."
            : "Laporan sudah diunggah. Silakan isi feedback.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    var keperluans = new List<string>();
    if (p.IsPermintaanData) keperluans.Add("Permintaan Data");
    if (p.IsObservasi)      keperluans.Add("Observasi");
    if (p.IsWawancara)      keperluans.Add("Wawancara");

    return View(new UploadLaporanUnifiedVm
    {
        PermohonanPPIDID = id,
        NoPermohonan     = p.NoPermohonan    ?? string.Empty,
        NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
        JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
        Keperluans       = keperluans,
        FilesUploaded    = p.Dokumen
            .Where(d => d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToList(),
    });
}

[HttpPost("upload-laporan"), ValidateAntiForgeryToken]
public async Task<IActionResult> UploadLaporanPost(UploadLaporanUnifiedVm vm)
{
    if (vm.FileLaporan == null || vm.FileLaporan.Length == 0)
        ModelState.AddModelError(nameof(vm.FileLaporan), "File laporan wajib dipilih.");

    if (vm.FileLaporan != null && vm.FileLaporan.Length > 0)
    {
        var val = Services.FileValidator.ValidateDocument(vm.FileLaporan);
        if (!val.IsValid)
            ModelState.AddModelError(nameof(vm.FileLaporan), val.ErrorMessage!);
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
        return View("UploadLaporan", vm);
    }

    var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
    if (p is null) return NotFound();

    if (p.StatusPPIDID != StatusId.DataSiap)
    {
        TempData["Error"] = "Upload tidak diizinkan pada status ini.";
        return RedirectToAction("Lacak", new { noPermohonan = vm.NoPermohonan });
    }

    var now = DateTime.UtcNow;
    var uploadsDir = Path.Combine(
        string.IsNullOrEmpty(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath,
        "uploads", vm.PermohonanPPIDID.ToString());
    Directory.CreateDirectory(uploadsDir);

    var fn = $"laporan_{now:yyyyMMddHHmmss}_{Services.FileValidator.SanitizeFileName(vm.FileLaporan!.FileName)}";
    await using (var s = new FileStream(Path.Combine(uploadsDir, fn), FileMode.Create))
        await vm.FileLaporan.CopyToAsync(s);

    var fp = $"/uploads/{vm.PermohonanPPIDID}/{fn}";

    db.DokumenPPID.Add(new DokumenPPID
    {
        PermohonanPPIDID     = vm.PermohonanPPIDID,
        NamaDokumenPPID      = $"Laporan Hasil Penelitian — {vm.FileLaporan.FileName}",
        UploadDokumenPPID    = fp,
        JenisDokumenPPIDID   = JenisDokumenId.TugasFinal,
        NamaJenisDokumenPPID = "Laporan Final Pemohon",
        CreatedAt            = now
    });

    // Auto-advance: DataSiap → FeedbackPemohon
    var lama = p.StatusPPIDID;
    p.StatusPPIDID = StatusId.FeedbackPemohon;
    p.UpdatedAt    = now;

    db.AuditLog.Add(new AuditLogPPID
    {
        PermohonanPPIDID = vm.PermohonanPPIDID,
        StatusLama       = lama,
        StatusBaru       = StatusId.FeedbackPemohon,
        Keterangan       = $"Pemohon mengunggah laporan hasil penelitian: {vm.FileLaporan.FileName}. " +
                           $"Catatan: {vm.Catatan ?? "(kosong)"}",
        Operator         = "Pemohon",
        CreatedAt        = now
    });

    await db.SaveChangesAsync();

    TempData["Success"] = "Laporan berhasil diunggah! Silakan isi feedback Anda.";
    return RedirectToAction("Feedback", new { id = vm.PermohonanPPIDID });
}

// ═══════════════════════════════════════════════════════════════════════
// FEEDBACK UNIFIED
// Satu feedback untuk semua keperluan; tersedia setelah laporan diunggah.
// Submit → otomatis Selesai.
// ═══════════════════════════════════════════════════════════════════════

[HttpGet("feedback/{id:guid}")]
public async Task<IActionResult> Feedback(Guid id)
{
    var p = await db.PermohonanPPID
        .Include(x => x.Pribadi)
        .Include(x => x.Dokumen)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
    if (p is null) return NotFound();

    bool laporanAda = p.Dokumen.Any(d => d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal);
    if (!laporanAda)
    {
        TempData["Error"] = "Unggah laporan hasil penelitian terlebih dahulu sebelum mengisi feedback.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    if (p.StatusPPIDID != StatusId.FeedbackPemohon)
    {
        TempData["Error"] = p.StatusPPIDID == StatusId.Selesai
            ? "Permohonan sudah selesai. Feedback tidak dapat diubah."
            : "Feedback belum dapat diisi saat ini.";
        return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
    }

    var existing = await db.FeedbackTaskPPID
        .FirstOrDefaultAsync(f => f.PermohonanPPIDID == id && f.JenisTask == JenisTask.Semua);

    var keperluans = new List<string>();
    if (p.IsPermintaanData) keperluans.Add("Permintaan Data");
    if (p.IsObservasi)      keperluans.Add("Observasi");
    if (p.IsWawancara)      keperluans.Add("Wawancara");

    return View(new FeedbackUnifiedVm
    {
        PermohonanPPIDID = id,
        NoPermohonan     = p.NoPermohonan    ?? string.Empty,
        NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
        JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
        Keperluans       = keperluans,
        SudahDiisi       = existing is not null,
        NilaiLama        = existing?.NilaiKepuasan ?? 0,
        NilaiKepuasan    = existing?.NilaiKepuasan ?? 0,
        CatatanLama      = existing?.Catatan,
    });
}

[HttpPost("feedback"), ValidateAntiForgeryToken]
public async Task<IActionResult> FeedbackPost(FeedbackUnifiedVm vm)
{
    if (string.IsNullOrWhiteSpace(vm.Catatan))
        ModelState.AddModelError(nameof(vm.Catatan), "Masukan / saran wajib diisi.");

    if (!ModelState.IsValid)
        return View("Feedback", vm);

    var now = DateTime.UtcNow;

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

    var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
    if (p is null) return NotFound();

    var lama = p.StatusPPIDID;
    p.StatusPPIDID   = StatusId.Selesai;
    p.TanggalSelesai = DateOnly.FromDateTime(DateTime.Today);
    p.UpdatedAt      = now;

    db.AuditLog.Add(new AuditLogPPID
    {
        PermohonanPPIDID = vm.PermohonanPPIDID,
        StatusLama       = lama,
        StatusBaru       = StatusId.Selesai,
        Keterangan       = $"Pemohon mengisi feedback (nilai: {vm.NilaiKepuasan}/5). " +
                           "Permohonan otomatis diselesaikan.",
        Operator         = "Pemohon",
        CreatedAt        = now
    });

    await db.SaveChangesAsync();

    TempData["Success"] =
        $"🎉 Feedback berhasil dikirim! Permohonan dinyatakan <strong>Selesai</strong>.";
    return RedirectToAction("Lacak", new { noPermohonan = vm.NoPermohonan });
}
}
