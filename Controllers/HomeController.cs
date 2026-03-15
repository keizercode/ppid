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
    // LACAK — single-factor: NoPermohonan saja sudah cukup karena format
    // PPD/YYYY/XXXXXXXX tidak dapat dienumerasi (32^8 ≈ 1,1 triliun).
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Lacak(string? noPermohonan)
    {
        if (string.IsNullOrEmpty(noPermohonan))
            return View("Index", new LacakViewModel());

        // Normalize: uppercase, trim whitespace
        noPermohonan = noPermohonan.Trim().ToUpperInvariant();

        var permohonan = await db.PermohonanPPID
            .Include(p => p.Status)
            .Include(p => p.Detail).ThenInclude(d => d.Keperluan)
            .Include(p => p.Dokumen)
            .Include(p => p.Jadwal)
            .FirstOrDefaultAsync(p => p.NoPermohonan == noPermohonan);

        if (permohonan == null)
        {
            // Pesan generik — tidak mengkonfirmasi apakah nomor ada/tidak
            // (mitigasi oracle attack meski nomor sudah non-sequential)
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
            Pribadi = pribadi!,
            PribadiPPID = pribadi?.PribadiPPID,
            Detail = permohonan.Detail.ToList(),
            Jadwal = permohonan.Jadwal.OrderBy(j => j.Tanggal).ToList(),
            Riwayat = BuildRiwayat(permohonan)
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
            var statusLama = p.StatusPPIDID;
            p.StatusPPIDID = StatusId.Selesai;
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            db.AuditLog.Add(new AuditLogPPID
            {
                PermohonanPPIDID = model.PermohonanPPIDID,
                StatusLama = statusLama,
                StatusBaru = StatusId.Selesai,
                Keterangan = "Kuesioner kepuasan diisi pemohon",
                Operator = "Pemohon",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        TempData["Success"] = "Terima kasih! Kuesioner berhasil dikirim.";

        // Redirect kembali ke halaman detail — tidak perlu token lagi
        return RedirectToAction("Lacak", new { noPermohonan = model.NoPermohonan });
    }

    // ════════════════════════════════════════════════════════════════════════
    // TIMELINE BUILDER
    // ════════════════════════════════════════════════════════════════════════

    private static List<RiwayatStatusVm> BuildRiwayat(PermohonanPPID p)
    {
        var current = p.StatusPPIDID ?? StatusId.TerdaftarSistem;
        var steps = GetSteps(p);
        int currentIdx = steps.FindIndex(s => s.StatusId == current);
        if (currentIdx < 0) currentIdx = FindNearestIdx(steps, current);

        return steps.Select((s, i) => new RiwayatStatusVm
        {
            StatusId = s.StatusId,
            Label = s.Label,
            Selesai = i < currentIdx,
            AktifSekarang = i == currentIdx
        }).ToList();
    }

    private static List<(int StatusId, string Label)> GetSteps(PermohonanPPID p)
    {
        bool isWawancaraOnly = p.IsWawancara && !p.IsPermintaanData && !p.IsObservasi;

        if (isWawancaraOnly)
        {
            return new List<(int, string)>
            {
                (StatusId.TerdaftarSistem,      "Permohonan Terdaftar"),
                (StatusId.IdentifikasiAwal,     "Identifikasi Awal"),
                (StatusId.SuratIzinTerbit,      "Surat Izin Diterbitkan"),
                (StatusId.WawancaraDijadwalkan, "Wawancara Dijadwalkan"),
                (StatusId.WawancaraSelesai,     "Wawancara Selesai"),
                (StatusId.Selesai,              "Selesai"),
            };
        }

        var steps = new List<(int, string)>
        {
            (StatusId.TerdaftarSistem,  "Permohonan Terdaftar"),
            (StatusId.IdentifikasiAwal, "Identifikasi Awal"),
            (StatusId.SuratIzinTerbit,  "Surat Izin Diterbitkan"),
            (StatusId.Didisposisi,      "Didisposisi ke Unit"),
            (StatusId.DiProses,         "Data Sedang Diproses"),
            (StatusId.DataSiap,         "Data Siap Diunduh"),
            (StatusId.Selesai,          "Selesai"),
        };

        if (p.IsObservasi)
        {
            var idx = steps.FindIndex(s => s.Item1 == StatusId.Didisposisi);
            steps.Insert(idx + 1, (StatusId.ObservasiDijadwalkan, "Observasi Dijadwalkan"));
            steps.Insert(idx + 2, (StatusId.ObservasiSelesai, "Observasi Selesai"));
        }

        if (p.IsWawancara)
        {
            var idx = steps.FindIndex(s => s.Item1 == StatusId.DiProses);
            if (idx >= 0)
                steps.Insert(idx + 1, (StatusId.WawancaraDijadwalkan, "Wawancara Dijadwalkan"));
        }

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
