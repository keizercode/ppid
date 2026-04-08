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

        // ── Load SubTasks untuk info paralel di timeline ──────────────────
        var subTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == permohonan.PermohonanPPIDID)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        // ── Jadwal aktif per jenis (termasuk info reschedule) ────────────
        var jadwalAktif = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == permohonan.PermohonanPPIDID && j.IsAktif)
            .ToListAsync();

        // ── Hitung lastChangedAt (untuk badge "BARU" di realtime check) ──
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
    // Digunakan oleh Detail.cshtml untuk polling setiap 30 detik.
    // Return JSON ringan: statusId + lastChangedAt (ISO 8601).
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

        // Ambil waktu update subtask & jadwal terbaru
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
            lastChangedAt = allDates.ToString("O")   // ISO 8601 for JS comparison
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // KUESIONER
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Kuesioner(Guid id)
    {
        var p = await db.PermohonanPPID.FindAsync(id);
        if (p == null) return NotFound();

        if (p.StatusPPIDID < StatusId.DataSiap || p.StatusPPIDID == StatusId.Selesai)
        {
            TempData["Error"] = "Kuesioner tidak tersedia untuk status permohonan ini.";
            return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
        }

        return View(new KuesionerVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> KuesionerPost(KuesionerVm model)
    {
        if (!ModelState.IsValid) return View("Kuesioner", model);

        var p = await db.PermohonanPPID.FindAsync(model.PermohonanPPIDID);
        if (p != null && p.StatusPPIDID != StatusId.Selesai)
        {
            var statusLama   = p.StatusPPIDID;
            p.StatusPPIDID   = StatusId.Selesai;
            p.TanggalSelesai = DateOnly.FromDateTime(DateTime.Today);
            p.UpdatedAt      = DateTime.UtcNow;
            await db.SaveChangesAsync();

            db.AuditLog.Add(new AuditLogPPID
            {
                PermohonanPPIDID = model.PermohonanPPIDID,
                StatusLama       = statusLama,
                StatusBaru       = StatusId.Selesai,
                Keterangan       = $"Kuesioner kepuasan diisi pemohon. Nilai: {model.NilaiKepuasan}/5.",
                Operator         = "Pemohon",
                CreatedAt        = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        TempData["Success"] = "Terima kasih! Kuesioner berhasil dikirim.";
        return RedirectToAction("Lacak", new { noPermohonan = model.NoPermohonan });
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
    // TIMELINE BUILDER — 9-step + sub-step informatif
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

    private static List<(int StatusId, string Label, string? SubLabel)> GetSteps(PermohonanPPID p)
    {
        var steps = new List<(int, string, string?)>
        {
            (StatusId.TerdaftarSistem,    "1. Permohonan Terdaftar",                  null),
            (StatusId.IdentifikasiAwal,   "2. Tanda Tangan Identifikasi Awal",        null),
            (StatusId.MenungguVerifikasi, "3. Verifikasi Kasubkel & Disposisi Unit",  null),
            (StatusId.MenungguSuratIzin,  "4. Pembuatan Surat Izin",                  null),
            (StatusId.SuratIzinTerbit,    "5. Surat Izin Terbit",                     null),
        };

        bool isWawancaraOnly = p.IsWawancara && !p.IsPermintaanData && !p.IsObservasi;

        if (isWawancaraOnly)
        {
            // Wawancara-only: step 6 langsung jadwal wawancara
            steps.Add((StatusId.WawancaraDijadwalkan, "6. Jadwal Wawancara Dibuat",
                p.NamaProdusenData != null ? $"PIC: {p.NamaProdusenData}" : null));
        }
        else
        {
            // Parallel mode (data / observasi / wawancara):
            // SATU step 6 saja — detail progress ditampilkan di panel sub-tugas
            var keperluanList = new List<string>();
            if (p.IsPermintaanData) keperluanList.Add("Permintaan Data");
            if (p.IsObservasi)      keperluanList.Add("Observasi");
            if (p.IsWawancara)      keperluanList.Add("Wawancara");

            steps.Add((StatusId.Didisposisi, "6. Pembuatan Jadwal / Pemrosesan Data",
                keperluanList.Count > 0 ? string.Join(" + ", keperluanList) : null));
        }

        // Step 7 & 8 tetap sama
        steps.Add((StatusId.DataSiap,       "7. Data Tersedia",                  null));
        steps.Add((StatusId.FeedbackPemohon, "8. Pengisian Feedback & Upload Laporan", null));
        steps.Add((StatusId.Selesai,          "9. Selesai",                       null));

        return steps;
    }

    private static int FindNearestIdx(List<(int StatusId, string Label, string? SubLabel)> steps, int current)
    {
        int best = -1;
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].StatusId <= current) best = i;
        return best >= 0 ? best : steps.Count - 1;
    }

    // ════════════════════════════════════════════════════════════════════════
    // UPLOAD TUGAS / LAPORAN FINAL PEMOHON
    // Pemohon mengunggah hasil akhir penelitian setelah data diterima.
    // Accessible secara publik (tanpa autentikasi) sama seperti Lacak.
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("upload-tugas/{id:guid}")]
    public async Task<IActionResult> UploadTugas(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Dokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        // Hanya izinkan upload jika data sudah siap
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

        if (!ModelState.IsValid)
        {
            // Re-load existing uploads
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

        // Simpan file
        var uploadsDir = Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads", vm.PermohonanPPIDID.ToString());
        Directory.CreateDirectory(uploadsDir);

        var fn = $"tugas_{now:yyyyMMddHHmmss}_{Path.GetFileName(vm.FileTugas!.FileName)}";
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
}
