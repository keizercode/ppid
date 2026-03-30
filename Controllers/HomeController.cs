using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

public class HomeController(AppDbContext db) : Controller
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
            Permohonan = permohonan,
            Pribadi    = pribadi!,
            PribadiPPID = pribadi?.PribadiPPID,
            Detail     = permohonan.Detail.ToList(),
            Jadwal     = permohonan.Jadwal.OrderBy(j => j.Tanggal).ToList(),
            Riwayat    = BuildRiwayat(permohonan)
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
        return View(new KuesionerVm { PermohonanPPIDID = id, NoPermohonan = p.NoPermohonan! });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> KuesionerPost(KuesionerVm model)
    {
        if (!ModelState.IsValid) return View("Kuesioner", model);

        var p = await db.PermohonanPPID.FindAsync(model.PermohonanPPIDID);
        if (p != null)
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
    // ERROR HANDLER
    // ════════════════════════════════════════════════════════════════════════

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = 500;
        return Content(
            "Terjadi kesalahan pada sistem. Silakan kembali ke halaman sebelumnya dan coba lagi.",
            "text/plain; charset=utf-8");
    }

    // ════════════════════════════════════════════════════════════════════════
    // TIMELINE BUILDER — mendukung 9 step sesuai requirements, termasuk
    // status baru: MenungguVerifikasi (14) dan FeedbackPemohon (15)
    // ════════════════════════════════════════════════════════════════════════

    private static List<RiwayatStatusVm> BuildRiwayat(PermohonanPPID p)
    {
        var current    = p.StatusPPIDID ?? StatusId.TerdaftarSistem;
        var steps      = GetSteps(p);
        int currentIdx = steps.FindIndex(s => s.StatusId == current);

        // Fallback: jika current tidak ada di steps, cari step terdekat (nilai ≤ current)
        if (currentIdx < 0) currentIdx = FindNearestIdx(steps, current);

        return steps.Select((s, i) => new RiwayatStatusVm
        {
            StatusId      = s.StatusId,
            Label         = s.Label,
            Selesai       = i < currentIdx,
            AktifSekarang = i == currentIdx
        }).ToList();
    }

    /// <summary>
    /// Bangun daftar step timeline sesuai keperluan permohonan.
    ///
    /// 9 step sesuai requirements:
    ///   1. Permohonan
    ///   2. Tanda Tangan Form Identifikasi Awal
    ///   3. Verifikasi Form Identifikasi Awal (Kasubkel + Disposisi Unit)
    ///   4. Pembuatan Surat Izin
    ///   5. Surat Izin Terbit
    ///   6. Pembuatan Jadwal / Pemrosesan Data
    ///   7. Observasi/Wawancara / Data Tersedia
    ///   8. Pengisian Feedback Pemohon
    ///   9. Selesai
    /// </summary>
    private static List<(int StatusId, string Label)> GetSteps(PermohonanPPID p)
    {
        // ── Base steps — selalu ada ───────────────────────────────────────
        var steps = new List<(int, string)>
        {
            (StatusId.TerdaftarSistem,     "1. Permohonan Terdaftar"),
            (StatusId.IdentifikasiAwal,    "2. Tanda Tangan Identifikasi Awal"),
            (StatusId.MenungguVerifikasi,  "3. Verifikasi Kasubkel & Disposisi Unit"),
            (StatusId.MenungguSuratIzin,   "4. Pembuatan Surat Izin"),
            (StatusId.SuratIzinTerbit,     "5. Surat Izin Terbit"),
        };

        // ── Step 6 & 7 — bervariasi berdasar jenis keperluan ─────────────
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

        // ── Step 8 & 9 — selalu ada ───────────────────────────────────────
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
