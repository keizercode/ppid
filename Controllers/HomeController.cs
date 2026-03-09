using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

public class HomeController(AppDbContext db) : Controller
{
    // GET /
    public IActionResult Index() => View(new LacakViewModel());

    // GET /Home/Lacak?noPermohonan=PPD/2024/0001
    [HttpGet]
    public async Task<IActionResult> Lacak(string? noPermohonan)
    {
        if (string.IsNullOrEmpty(noPermohonan))
            return View("Index", new LacakViewModel());

        var permohonan = await db.PermohonanPPID
            .Include(p => p.Status)
            .Include(p => p.Detail).ThenInclude(d => d.Keperluan)
            .Include(p => p.Dokumen)
            .Include(p => p.Jadwal)
            .FirstOrDefaultAsync(p => p.NoPermohonan == noPermohonan);

        if (permohonan == null)
        {
            TempData["Error"] = $"Nomor permohonan <strong>{noPermohonan}</strong> tidak ditemukan.";
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
            Riwayat     = BuildRiwayat(permohonan.StatusPPIDID ?? 1)
        };

        return View("Detail", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult LacakPost(LacakViewModel model)
    {
        if (!ModelState.IsValid) return View("Index", model);
        return RedirectToAction("Lacak", new { noPermohonan = model.NoPermohonan.Trim() });
    }

    // GET/POST Kuesioner
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

        // Simpan ke catatan (bisa tambah tabel kuesioner nanti)
        var p = await db.PermohonanPPID.FindAsync(model.PermohonanPPIDID);
        if (p != null)
        {
            p.StatusPPIDID = StatusId.Selesai;
            p.UpdatedAt    = DateTime.Now;
            await db.SaveChangesAsync();
        }

        TempData["Success"] = "Terima kasih! Kuesioner berhasil dikirim.";
        return RedirectToAction("Lacak", new { noPermohonan = model.NoPermohonan });
    }

    // ── Helper ────────────────────────────────────────────────────────────────
    private static List<RiwayatStatusVm> BuildRiwayat(int current)
    {
        var steps = new[]
        {
            (StatusId.TerdaftarSistem,      "Permohonan Terdaftar"),
            (StatusId.IdentifikasiAwal,     "Identifikasi Awal"),
            (StatusId.SuratIzinTerbit,      "Surat Izin Diterbitkan"),
            (StatusId.Didisposisi,          "Didisposisi ke Unit"),
            (StatusId.DiProses,             "Data Sedang Diproses"),
            (StatusId.DataSiap,             "Data Siap Diunduh"),
            (StatusId.Selesai,              "Selesai"),
        };

        var result = new List<RiwayatStatusVm>();
        foreach (var (id, label) in steps)
        {
            result.Add(new RiwayatStatusVm
            {
                StatusId      = id,
                Label         = label,
                Selesai       = current > id,
                AktifSekarang = current == id
            });
        }

        // Sisipkan observasi jika sedang di tahap itu
        if (current is StatusId.ObservasiDijadwalkan or StatusId.ObservasiSelesai)
        {
            var idx = result.FindIndex(r => r.StatusId == StatusId.Didisposisi);
            result.Insert(idx + 1, new RiwayatStatusVm
            {
                StatusId      = StatusId.ObservasiDijadwalkan,
                Label         = "Observasi / Wawancara",
                Selesai       = current == StatusId.ObservasiSelesai,
                AktifSekarang = current == StatusId.ObservasiDijadwalkan
            });
        }

        return result;
    }
}
