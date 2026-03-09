using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

// ── KEPEGAWAIAN ───────────────────────────────────────────────────────────────

[Route("kepegawaian")]
public class KepegawaianController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID == StatusId.MenungguSuratIzin)
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();
        return View(list);
    }

    [HttpGet("surat-izin/{id}")]
    public async Task<IActionResult> SuratIzin(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi).FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new SuratIzinVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            Kategori         = p.KategoriPemohon ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? ""
        });
    }

    [HttpPost("surat-izin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuratIzinPost(SuratIzinVm vm)
    {
        if (!ModelState.IsValid) return View("SuratIzin", vm);

        // Upload surat izin
        if (vm.FileSuratIzin?.Length > 0)
        {
            var dir = Path.Combine(env.WebRootPath, "uploads", vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"surat_izin_{vm.FileSuratIzin.FileName}";
            using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileSuratIzin.CopyToAsync(s);
            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                NamaDokumenPPID  = "Surat Izin",
                UploadDokumenPPID = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.SuratIzin,
                CreatedAt        = DateTime.Now
            });
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null)
        {
            p.NoSuratPermohonan = vm.NoSuratIzin;
            p.StatusPPIDID      = StatusId.Didisposisi;
            p.UpdatedAt         = DateTime.Now;
            // Disposisi ke bidang terkait jika dipilih
            if (vm.DisposisiKe == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangTerkait))
                p.NamaBidang = vm.NamaBidangTerkait;
        }
        await db.SaveChangesAsync();

        TempData["Success"] = $"Surat izin <strong>{vm.NoSuratIzin}</strong> diterbitkan dan permohonan didisposisi.";
        return RedirectToAction("Index");
    }
}

// ── KDI ───────────────────────────────────────────────────────────────────────

[Route("kdi")]
public class KdiController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi && p.StatusPPIDID <= StatusId.ObservasiSelesai)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
        return View(list);
    }

    [HttpGet("psmdi")]
    public async Task<IActionResult> Psmdi()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi && p.StatusPPIDID <= StatusId.ObservasiSelesai && p.NamaBidang == null)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
        return View(list);
    }

    [HttpGet("bidang")]
    public async Task<IActionResult> Bidang()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi && p.StatusPPIDID <= StatusId.ObservasiSelesai && p.NamaBidang != null)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
        return View(list);
    }

    [HttpGet("terima/{id}")]
    public async Task<IActionResult> TerimaDisposisi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi).FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new TerimaDisposisiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? "",
            LatarBelakang    = p.LatarBelakang ?? "",
            PerluObservasi   = p.IsObservasi || p.IsWawancara
        });
    }

    [HttpPost("terima"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TerimaDisposisiPost(TerimaDisposisiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.DiProses; p.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
        TempData["Success"] = "Disposisi diterima. Silakan siapkan data.";

        if (vm.PerluObservasi)
            return RedirectToAction("JadwalObservasi", new { id = vm.PermohonanPPIDID });
        return RedirectToAction("UploadData", new { id = vm.PermohonanPPIDID });
    }

    [HttpGet("jadwal/{id}")]
    public async Task<IActionResult> JadwalObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi).FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new JadwalObservasiVm { PermohonanPPIDID = id, NoPermohonan = p.NoPermohonan!, NamaPemohon = p.Pribadi?.Nama ?? "" });
    }

    [HttpPost("jadwal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalObservasiPost(JadwalObservasiVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalObservasi", vm);
        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            Tanggal          = vm.TanggalObservasi,
            Waktu            = vm.WaktuObservasi,
            NamaPIC          = vm.NamaPIC,
            CreatedAt        = DateTime.Now
        });
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.ObservasiDijadwalkan; p.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
        TempData["Success"] = $"Jadwal observasi berhasil dibuat: <strong>{vm.TanggalObservasi:dd MMM yyyy}</strong> pukul {vm.WaktuObservasi:HH:mm}";
        return RedirectToAction("Index");
    }

    [HttpGet("upload-data/{id}")]
    public async Task<IActionResult> UploadData(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi).FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new UploadDataVm { PermohonanPPIDID = id, NoPermohonan = p.NoPermohonan!, NamaPemohon = p.Pribadi?.Nama ?? "", JudulPenelitian = p.JudulPenelitian ?? "" });
    }

    [HttpPost("upload-data"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDataPost(UploadDataVm vm)
    {
        if (!ModelState.IsValid) return View("UploadData", vm);

        if (vm.FileData?.Length > 0)
        {
            var dir = Path.Combine(env.WebRootPath, "uploads", vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"data_{vm.FileData.FileName}";
            using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileData.CopyToAsync(s);
            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID  = vm.PermohonanPPIDID,
                NamaDokumenPPID   = "Data Hasil",
                UploadDokumenPPID = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.DataHasil,
                CreatedAt         = DateTime.Now
            });
        }
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.DataSiap; p.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();

        TempData["Success"] = "Data berhasil diupload. Pemohon dapat mengunduh data.";
        return RedirectToAction("Index");
    }
}
