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

        var vm = new DetailLacakViewModel
        {
            Permohonan  = permohonan,
            Pribadi     = pribadi!,
            PribadiPPID = pribadi?.PribadiPPID,
            Detail      = permohonan.Detail.ToList(),
            Jadwal      = permohonan.Jadwal.OrderBy(j => j.Tanggal).ToList(),
            Riwayat     = BuildRiwayat(permohonan)
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
    // KUESIONER
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Kuesioner(Guid id)
    {
        var p = await db.PermohonanPPID.FindAsync(id);
        if (p == null) return NotFound();

        // Kuesioner hanya bisa diisi jika status >= FeedbackPemohon (atau DataSiap sebagai fallback)
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
                Keterangan       = "Kuesioner kepuasan diisi pemohon",
                Operator         = "Pemohon",
                CreatedAt        = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        TempData["Success"] = "Terima kasih! Kuesioner berhasil dikirim.";
        return RedirectToAction("Lacak", new { noPermohonan = model.NoPermohonan });
    }

    // ════════════════════════════════════════════════════════════════════════
    // ERROR HANDLER — professional exception capture
    // ════════════════════════════════════════════════════════════════════════

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = 500;

        var exFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var ex        = exFeature?.Error;
        var path      = exFeature?.Path ?? HttpContext.Request.Path;

        // Log exception dengan konteks lengkap
        if (ex is not null)
        {
            logger.LogError(ex,
                "Unhandled exception | Path: {Path} | User: {User} | IP: {IP}",
                path,
                User.Identity?.Name ?? "anonymous",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        }

        // Development: tampilkan detail teknis
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

        // Production: pesan ramah tanpa bocoran teknis
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
            Selesai       = i < currentIdx,
            AktifSekarang = i == currentIdx
        }).ToList();
    }

    private static List<(int StatusId, string Label)> GetSteps(PermohonanPPID p)
    {
        var steps = new List<(int, string)>
        {
            (StatusId.TerdaftarSistem,    "1. Permohonan Terdaftar"),
            (StatusId.IdentifikasiAwal,   "2. Tanda Tangan Identifikasi Awal"),
            (StatusId.MenungguVerifikasi, "3. Verifikasi Kasubkel & Disposisi Unit"),
            (StatusId.MenungguSuratIzin,  "4. Pembuatan Surat Izin"),
            (StatusId.SuratIzinTerbit,    "5. Surat Izin Terbit"),
        };

        bool isWawancaraOnly = p.IsWawancara && !p.IsPermintaanData && !p.IsObservasi;

        if (isWawancaraOnly)
        {
            steps.Add((StatusId.WawancaraDijadwalkan, "6. Jadwal Wawancara Dibuat"));
            steps.Add((StatusId.WawancaraSelesai,     "7. Wawancara Selesai"));
        }
        else
        {
            steps.Add((StatusId.Didisposisi, "6. Pembuatan Jadwal / Pemrosesan Data"));

            if (p.IsObservasi)
            {
                steps.Add((StatusId.ObservasiDijadwalkan, "6b. Observasi Dijadwalkan"));
                steps.Add((StatusId.ObservasiSelesai,     "6c. Observasi Selesai"));
            }

            if (p.IsWawancara)
            {
                steps.Add((StatusId.WawancaraDijadwalkan, "6d. Wawancara Dijadwalkan"));
                steps.Add((StatusId.WawancaraSelesai,     "6e. Wawancara Selesai"));
            }

            steps.Add((StatusId.DataSiap, "7. Data Tersedia"));
        }

        steps.Add((StatusId.FeedbackPemohon, "8. Pengisian Feedback Pemohon"));
        steps.Add((StatusId.Selesai,         "9. Selesai"));

        return steps;
    }

    private static int FindNearestIdx(List<(int StatusId, string Label)> steps, int current)
    {
        int best = -1;
        for (int i = 0; i < steps.Count; i++)
            if (steps[i].StatusId <= current) best = i;
        return best >= 0 ? best : steps.Count - 1;
    }
}
