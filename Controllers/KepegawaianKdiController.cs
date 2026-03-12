using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

// ════════════════════════════════════════════════════════════════════════════════
// KEPEGAWAIAN — Menerbitkan Surat Izin & Mendisposisi ke KDI atau Produsen Data
// ════════════════════════════════════════════════════════════════════════════════

[Route("kepegawaian")]
public class KepegawaianController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

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
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        return View(new SuratIzinVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            Kategori = p.KategoriPemohon ?? "",
            JudulPenelitian = p.JudulPenelitian ?? "",
            // Pre-fill keperluan dari data pemohon (dapat di-override kepegawaian)
            IsObservasi = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara = p.IsWawancara,
        });
    }

    [HttpPost("surat-izin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuratIzinPost(SuratIzinVm vm)
    {
        if (!ModelState.IsValid) return View("SuratIzin", vm);

        var now = DateTime.UtcNow;

        // Upload surat izin (opsional)
        if (vm.FileSuratIzin?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"surat_izin_{vm.FileSuratIzin.FileName}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileSuratIzin.CopyToAsync(s);
            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                NamaDokumenPPID = "Surat Izin",
                UploadDokumenPPID = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.SuratIzin,
                CreatedAt = now
            });
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        p.NoSuratPermohonan = vm.NoSuratIzin;
        p.UpdatedAt = now;

        // ── ROUTING LOGIC ───────────────────────────────────────────────────
        //
        //  Aturan bisnis:
        //  ┌───────────────────────────────────────────────────────────────┐
        //  │ WAWANCARA-ONLY (tanpa data & tanpa observasi)                 │
        //  │   → Status: WawancaraDijadwalkan                              │
        //  │   → Diteruskan ke Produsen Data (/produsen-data)              │
        //  │   → Produsen Data menjadwalkan & melayani wawancara langsung  │
        //  │                                                               │
        //  │ ADA PERMINTAAN DATA atau OBSERVASI (± wawancara)              │
        //  │   → Status: SuratIzinTerbit → Didisposisi                    │
        //  │   → Diteruskan ke KDI (/kdi)                                 │
        //  │   → KDI menangani data + observasi; wawancara dapat           │
        //  │     dijadwalkan secara terpisah oleh KDI                      │
        //  └───────────────────────────────────────────────────────────────┘
        //
        if (vm.IsWawancaraOnly)
        {
            // Wawancara langsung ke Produsen Data
            p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
            p.NamaProdusenData = vm.NamaProdusenData ?? vm.NamaBidangTerkait;
            // NamaBidang tidak diisi (bukan jalur KDI)
        }
        else
        {
            // Ada data / observasi → ke KDI
            // Gunakan SuratIzinTerbit secara singkat, langsung ke Didisposisi
            p.StatusPPIDID = StatusId.Didisposisi;

            if (vm.DisposisiKe == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangTerkait))
                p.NamaBidang = vm.NamaBidangTerkait;
            else
                p.NamaBidang = null; // null = PSMDI
        }

        await db.SaveChangesAsync();

        var tujuan = vm.IsWawancaraOnly
            ? "dan diteruskan ke Produsen Data untuk penjadwalan wawancara"
            : $"dan didisposisi ke {(p.NamaBidang == null ? "PSMDI" : p.NamaBidang)}";

        TempData["Success"] = $"Surat izin <strong>{vm.NoSuratIzin}</strong> diterbitkan {tujuan}.";
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// KDI — Memproses Permintaan Data + Observasi
//        (Juga menangani wawancara jika dikombinasikan dengan data/observasi)
// ════════════════════════════════════════════════════════════════════════════════

[Route("kdi")]
public class KdiController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi
                     && p.StatusPPIDID <= StatusId.ObservasiSelesai)
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
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi
                     && p.StatusPPIDID <= StatusId.ObservasiSelesai
                     && p.NamaBidang == null)
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
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi
                     && p.StatusPPIDID <= StatusId.ObservasiSelesai
                     && p.NamaBidang != null)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
        return View(list);
    }

    // ── Terima Disposisi ──────────────────────────────────────────────────────

    [HttpGet("terima/{id}")]
    public async Task<IActionResult> TerimaDisposisi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        return View(new TerimaDisposisiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? "",
            LatarBelakang = p.LatarBelakang ?? "",
            // Observasi dan Wawancara dipisah:
            // - PerluObservasi: butuh penjadwalan di lapangan (site visit)
            // - PerluWawancara: butuh jadwal wawancara dengan unit penghasil data
            PerluObservasi = p.IsObservasi,
            PerluWawancara = p.IsWawancara,  // kombinasi: wawancara + data/observasi
        });
    }

    [HttpPost("terima"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TerimaDisposisiPost(TerimaDisposisiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null)
        {
            p.StatusPPIDID = StatusId.DiProses;
            p.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        TempData["Success"] = "Disposisi diterima. Silakan siapkan data.";

        // Tentukan langkah berikutnya berdasarkan keperluan
        if (vm.PerluObservasi)
            return RedirectToAction("JadwalObservasi", new { id = vm.PermohonanPPIDID });

        if (vm.PerluWawancara)
            // Wawancara dikombinasikan dengan data — jadwalkan wawancara juga
            return RedirectToAction("JadwalWawancara", new { id = vm.PermohonanPPIDID });

        return RedirectToAction("UploadData", new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Observasi ──────────────────────────────────────────────────────

    [HttpGet("jadwal/{id}")]
    public async Task<IActionResult> JadwalObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new JadwalObservasiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? ""
        });
    }

    [HttpPost("jadwal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalObservasiPost(JadwalObservasiVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalObservasi", vm);
        var now = DateTime.UtcNow;
        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal = "Observasi",
            Tanggal = vm.TanggalObservasi,
            Waktu = vm.WaktuObservasi,
            NamaPIC = vm.NamaPIC,
            CreatedAt = now
        });
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.ObservasiDijadwalkan; p.UpdatedAt = now; }
        await db.SaveChangesAsync();
        TempData["Success"] = $"Jadwal observasi: <strong>{vm.TanggalObservasi:dd MMM yyyy}</strong> pukul {vm.WaktuObservasi:HH:mm}";
        return RedirectToAction("Index");
    }

    // ── Jadwal Wawancara (kombinasi dengan data) ──────────────────────────────
    // Digunakan ketika pemohon memilih Wawancara + (Observasi/Data).
    // Wawancara-only ditangani oleh ProdusenDataController.

    [HttpGet("jadwal-wawancara/{id}")]
    public async Task<IActionResult> JadwalWawancara(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var detailWaw = p.Detail.FirstOrDefault(d => d.KeperluanID == KeperluanId.Wawancara);
        return View(new JadwalWawancaraVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? "",
            DetailWawancara = detailWaw?.DetailKeperluan ?? "",
            NamaProdusenData = p.NamaBidang,
        });
    }

    [HttpPost("jadwal-wawancara"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalWawancaraVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalWawancara", vm);
        var now = DateTime.UtcNow;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal = "Wawancara",
            Tanggal = vm.TanggalWawancara,
            Waktu = vm.WaktuWawancara,
            NamaPIC = vm.NamaPIC,
            CreatedAt = now
        });

        // Status tetap DiProses; wawancara adalah bagian dari proses,
        // bukan status utama (hanya wawancara-only yang punya status sendiri)
        await db.SaveChangesAsync();
        TempData["Success"] = $"Jadwal wawancara: <strong>{vm.TanggalWawancara:dd MMM yyyy}</strong> pukul {vm.WaktuWawancara:HH:mm}";
        return RedirectToAction("UploadData", new { id = vm.PermohonanPPIDID });
    }

    // ── Upload Data ───────────────────────────────────────────────────────────

    [HttpGet("upload-data/{id}")]
    public async Task<IActionResult> UploadData(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new UploadDataVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? ""
        });
    }

    [HttpPost("upload-data"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDataPost(UploadDataVm vm)
    {
        if (!ModelState.IsValid) return View("UploadData", vm);

        var now = DateTime.UtcNow;

        if (vm.FileData?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"data_{vm.FileData.FileName}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileData.CopyToAsync(s);
            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                NamaDokumenPPID = "Data Hasil",
                UploadDokumenPPID = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.DataHasil,
                CreatedAt = now
            });
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.DataSiap; p.UpdatedAt = now; }
        await db.SaveChangesAsync();

        TempData["Success"] = "Data berhasil diupload. Pemohon dapat mengunduh data.";
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════════════════════════
// PRODUSEN DATA — Melayani Wawancara / Interview Langsung (Wawancara-Only)
//
// Alur:
//   Kepegawaian menerbitkan surat izin
//     → StatusPPIDID = WawancaraDijadwalkan
//     → NamaProdusenData diisi dengan nama unit yang bertanggung jawab
//   Produsen Data menerima notifikasi, menjadwalkan wawancara
//   Setelah wawancara selesai → StatusPPIDID = WawancaraSelesai
//   Jika perlu hasil tertulis → upload data → StatusPPIDID = DataSiap
//   Jika tidak ada data → langsung Selesai
// ════════════════════════════════════════════════════════════════════════════════

[Route("produsen-data")]
public class ProdusenDataController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    /// <summary>Dashboard Produsen Data — menampilkan permintaan wawancara masuk.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID == StatusId.WawancaraDijadwalkan
                     || p.StatusPPIDID == StatusId.WawancaraSelesai)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
        return View(list);
    }

    // ── Jadwal Wawancara ──────────────────────────────────────────────────────

    [HttpGet("jadwal/{id}")]
    public async Task<IActionResult> JadwalWawancara(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var detailWaw = p.Detail.FirstOrDefault(d => d.KeperluanID == KeperluanId.Wawancara);
        return View(new JadwalWawancaraVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? "",
            DetailWawancara = detailWaw?.DetailKeperluan ?? "",
            NamaProdusenData = p.NamaProdusenData,
        });
    }

    [HttpPost("jadwal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalWawancaraVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalWawancara", vm);
        var now = DateTime.UtcNow;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal = "Wawancara",
            Tanggal = vm.TanggalWawancara,
            Waktu = vm.WaktuWawancara,
            NamaPIC = vm.NamaPIC,
            CreatedAt = now
        });
        // Status tetap WawancaraDijadwalkan (sudah di-set oleh Kepegawaian)
        await db.SaveChangesAsync();

        TempData["Success"] = $"Jadwal wawancara <strong>{vm.TanggalWawancara:dd MMM yyyy}</strong> pukul {vm.WaktuWawancara:HH:mm} berhasil dibuat. Pemohon dapat melihat jadwal ini secara online.";
        return RedirectToAction("Index");
    }

    // ── Selesai Wawancara ─────────────────────────────────────────────────────

    [HttpGet("selesai/{id}")]
    public async Task<IActionResult> SelesaiWawancara(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new UploadDataVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? ""
        });
    }

    /// <summary>
    /// Menandai wawancara selesai.
    /// Jika ada file hasil wawancara (transkip/notulen) → status DataSiap.
    /// Jika tidak ada file → status WawancaraSelesai → Selesai.
    /// </summary>
    [HttpPost("selesai"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiWawancaraPost(UploadDataVm vm)
    {
        var now = DateTime.UtcNow;
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        if (vm.FileData?.Length > 0)
        {
            // Ada dokumen hasil (transkip, notulen, dll) — bisa diunduh pemohon
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"data_{vm.FileData.FileName}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileData.CopyToAsync(s);
            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                NamaDokumenPPID = "Hasil Wawancara",
                UploadDokumenPPID = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.DataHasil,
                CreatedAt = now
            });
            p.StatusPPIDID = StatusId.DataSiap;
            TempData["Success"] = "Wawancara selesai. Dokumen hasil tersedia untuk diunduh pemohon.";
        }
        else
        {
            // Tidak ada file hasil — wawancara langsung, tandai selesai
            p.StatusPPIDID = StatusId.WawancaraSelesai;
            TempData["Success"] = "Wawancara selesai. Pemohon dapat mengisi kuesioner kepuasan.";
        }

        p.UpdatedAt = now;
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
