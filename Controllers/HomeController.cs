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

    private static List<(int StatusId, string Label, string? SubLabel)> GetSteps(PermohonanPPID p)
    {
        var steps = new List<(int, string, string?)>
        {
            (StatusId.TerdaftarSistem,    "1. Permohonan Terdaftar",                 null),
            (StatusId.IdentifikasiAwal,   "2. Tanda Tangan Identifikasi Awal",       null),
            (StatusId.MenungguVerifikasi, "3. Verifikasi Kasubkel & Disposisi Unit", null),
            (StatusId.MenungguSuratIzin,  "4. Pembuatan Surat Izin",                 null),
            (StatusId.SuratIzinTerbit,    "5. Surat Izin Terbit",                    null),
        };

        var keperluanList = new List<string>();
        if (p.IsPermintaanData) keperluanList.Add("Permintaan Data");
        if (p.IsObservasi)      keperluanList.Add("Observasi");
        if (p.IsWawancara)      keperluanList.Add("Wawancara");

        steps.Add((StatusId.Didisposisi, "6. Pembuatan Jadwal / Pemrosesan Data",
            keperluanList.Count > 0 ? string.Join(" + ", keperluanList) : null));

        steps.Add((StatusId.DataSiap,        "7. Data Tersedia",                          null));

        // Step 8: Pengisian Feedback per Tugas (Observasi, PermintaanData, Wawancara)
        var feedbackList = new List<string>();
        if (p.IsPermintaanData) feedbackList.Add("Permintaan Data");
        if (p.IsObservasi)      feedbackList.Add("Observasi");
        if (p.IsWawancara)      feedbackList.Add("Wawancara");

        steps.Add((StatusId.FeedbackPemohon, "8. Pengisian Feedback & Unggah Laporan",
            feedbackList.Count > 0 ? "Feedback: " + string.Join(", ", feedbackList) : null));

        steps.Add((StatusId.Selesai, "9. Selesai", null));

        return steps;
    }

    private static int GetWorkflowOrder(int statusId) => statusId switch
    {
        StatusId.TerdaftarSistem                                          => 1,
        StatusId.IdentifikasiAwal                                         => 2,
        StatusId.MenungguVerifikasi or StatusId.MenungguSuratIzin         => 3,
        StatusId.SuratIzinTerbit                                          => 4,
        StatusId.Didisposisi or StatusId.DiProses
            or StatusId.ObservasiDijadwalkan or StatusId.ObservasiSelesai
            or StatusId.WawancaraDijadwalkan or StatusId.WawancaraSelesai => 5,
        StatusId.DataSiap                                                 => 6,
        StatusId.FeedbackPemohon                                          => 7,
        StatusId.Selesai                                                  => 8,
        _                                                                 => 0
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

    // ════════════════════════════════════════════════════════════════════════
    // FEEDBACK PER TASK (OBSERVASI / PERMINTAAN DATA / WAWANCARA)
    // Menggantikan Kuesioner Kepuasan Umum.
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("feedback-task/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> FeedbackTask(Guid id, string jenisTask)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        // Hanya boleh jika sub-task sudah selesai
        var st = await db.SubTaskPPID
            .FirstOrDefaultAsync(t => t.PermohonanPPIDID == id && t.JenisTask == jenisTask);

        if (st is null || !st.IsSelesai)
        {
            TempData["Error"] = $"Feedback {JenisTask.GetLabel(jenisTask)} hanya dapat diisi setelah tugas selesai.";
            return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
        }


// Feedback aktif sejak DataSiap (otomatis setelah semua tugas selesai).
// Selesai / Dibatalkan → tidak bisa diubah lagi.
bool statusBisaFeedback = p.StatusPPIDID == StatusId.DataSiap
                       || p.StatusPPIDID == StatusId.FeedbackPemohon;

if (!statusBisaFeedback)
{
    string pesanError = p.StatusPPIDID < StatusId.DataSiap
        ? "Feedback belum dapat diisi. Pastikan semua proses pengolahan data selesai terlebih dahulu."
        : "Permohonan sudah selesai atau dibatalkan. Feedback tidak dapat diubah lagi.";

    TempData["Error"] = pesanError;
    return RedirectToAction("Lacak", new { noPermohonan = p.NoPermohonan });
}

        var existing = await db.FeedbackTaskPPID
            .FirstOrDefaultAsync(f => f.PermohonanPPIDID == id && f.JenisTask == jenisTask);

        return View(new FeedbackTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan ?? string.Empty,
            JenisTask        = jenisTask,
            NamaPemohon      = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
            TanggalJadwal    = st.TanggalJadwal,
            WaktuJadwal      = st.WaktuJadwal,
            LokasiDetail     = st.LokasiDetail,
            SudahDiisi       = existing is not null,
            NilaiLama        = existing?.NilaiKepuasan ?? 0,
            NilaiKepuasan    = existing?.NilaiKepuasan ?? 0,
            CatatanLama      = existing?.Catatan,
            FilePathLama     = existing?.FileLaporan,
            NamaFileLama     = existing?.NamaFile,
        });
    }

    [HttpPost("feedback-task"), ValidateAntiForgeryToken]
    public async Task<IActionResult> FeedbackTaskPost(FeedbackTaskVm vm)
    {
        if (!ModelState.IsValid)
            return View("FeedbackTask", vm);

        var now    = DateTime.UtcNow;
        string? fp = null;
        string? fn = null;

        // Upload file laporan jika ada
        if (vm.FileLaporan?.Length > 0)
        {
            var uploadsDir = Path.Combine(
                string.IsNullOrEmpty(env.WebRootPath)
                    ? Path.Combine(env.ContentRootPath, "wwwroot")
                    : env.WebRootPath,
                "uploads", vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(uploadsDir);

            fn = $"feedback_{vm.JenisTask}_{now:yyyyMMddHHmmss}_{Path.GetFileName(vm.FileLaporan.FileName)}";
            await using var s = new FileStream(Path.Combine(uploadsDir, fn), FileMode.Create);
            await vm.FileLaporan.CopyToAsync(s);
            fp = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
            fn = vm.FileLaporan.FileName;
        }

        // Upsert feedback
        var existing = await db.FeedbackTaskPPID
            .FirstOrDefaultAsync(f => f.PermohonanPPIDID == vm.PermohonanPPIDID
                                   && f.JenisTask == vm.JenisTask);

        if (existing is null)
        {
            db.FeedbackTaskPPID.Add(new FeedbackTaskPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                JenisTask        = vm.JenisTask,
                NilaiKepuasan    = vm.NilaiKepuasan,
                Catatan          = vm.Catatan,
                FileLaporan      = fp ?? vm.FilePathLama,
                NamaFile         = fn ?? vm.NamaFileLama,
                CreatedAt        = now
            });
        }
        else
        {
            existing.NilaiKepuasan = vm.NilaiKepuasan;
            existing.Catatan       = vm.Catatan;
            if (fp is not null)
            {
                existing.FileLaporan = fp;
                existing.NamaFile    = fn;
            }
        }

        await db.SaveChangesAsync();

        // ── Cek apakah semua feedback task yang wajib sudah terisi ────────
        // Jika ya, otomatis advance status ke Selesai.
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);

        if (p != null
    && (p.StatusPPIDID == StatusId.DataSiap || p.StatusPPIDID == StatusId.FeedbackPemohon)
    && p.StatusPPIDID != StatusId.Selesai
    && p.StatusPPIDID != StatusId.Dibatalkan)
        {
            // ── Cek apakah semua feedback task yang wajib sudah terisi ───────
            // Task yang selesai (bukan dibatalkan) — hanya ini yang perlu feedback
            var completedTaskJenis = await db.SubTaskPPID
                .Where(t => t.PermohonanPPIDID == vm.PermohonanPPIDID
                        && t.StatusTask == SubTaskStatus.Selesai)   // ← unchanged, correct
                .Select(t => t.JenisTask)
                .ToListAsync();

            // TAMBAHAN: pastikan tidak ada task yang masih InProgress/Pending
            // (edge case: task lain belum selesai ketika feedback dikirim lebih awal)
            bool anyStillRunning = await db.SubTaskPPID
                .AnyAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID
                        && (t.StatusTask == SubTaskStatus.Pending
                            || t.StatusTask == SubTaskStatus.InProgress));

            var filledFeedbackJenis = await db.FeedbackTaskPPID
                .Where(f => f.PermohonanPPIDID == vm.PermohonanPPIDID)
                .Select(f => f.JenisTask)
                .ToListAsync();

            bool allFeedbacksDone = !anyStillRunning        // ← new guard
                && completedTaskJenis.Count > 0
                && completedTaskJenis.All(j => filledFeedbackJenis.Contains(j));

            if (allFeedbacksDone)
            {
                var lama             = p.StatusPPIDID;
                p.StatusPPIDID       = StatusId.Selesai;
                p.TanggalSelesai     = DateOnly.FromDateTime(DateTime.Today);
                p.UpdatedAt          = now;

                int jmlFeedback      = filledFeedbackJenis.Count;
                db.AuditLog.Add(new AuditLogPPID
                {
                    PermohonanPPIDID = vm.PermohonanPPIDID,
                    StatusLama       = lama,
                    StatusBaru       = StatusId.Selesai,
                    Keterangan       = $"Semua feedback tugas ({jmlFeedback}) telah diisi pemohon. " +
                                       "Permohonan otomatis diselesaikan.",
                    Operator         = "Pemohon",
                    CreatedAt        = now
                });

                await db.SaveChangesAsync();

                TempData["Success"] =
                    $"Feedback <strong>{JenisTask.GetLabel(vm.JenisTask)}</strong> berhasil dikirim. " +
                    "🎉 Semua feedback telah lengkap — permohonan dinyatakan <strong>Selesai</strong>!";

                return RedirectToAction("Lacak", new { noPermohonan = vm.NoPermohonan });
            }

            // Hitung sisa feedback yang belum diisi
            var sisaFeedback = completedTaskJenis
                .Where(j => !filledFeedbackJenis.Contains(j))
                .Select(JenisTask.GetLabel)
                .ToList();

            TempData["Success"] =
                $"Feedback <strong>{JenisTask.GetLabel(vm.JenisTask)}</strong> berhasil dikirim. " +
                (sisaFeedback.Count > 0
                    ? $"Masih ada {sisaFeedback.Count} feedback yang perlu diisi: {string.Join(", ", sisaFeedback)}."
                    : "Terima kasih!");
        }
        else
        {
            TempData["Success"] = $"Feedback {JenisTask.GetLabel(vm.JenisTask)} berhasil dikirim. Terima kasih!";
        }

        return RedirectToAction("Lacak", new { noPermohonan = vm.NoPermohonan });
    }
}
