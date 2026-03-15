using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

// ════════════════════════════════════════════════════════════════════════════
// KEPEGAWAIAN
// ════════════════════════════════════════════════════════════════════════════

[Route("kepegawaian")]
[Authorize(Roles = "Kepegawaian,Admin")]
public class KepegawaianController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? "Kepegawaian";

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

        var statusLama = p.StatusPPIDID;

        p.NoSuratPermohonan = vm.NoSuratIzin;
        p.UpdatedAt = now;

        p.IsObservasi = vm.IsObservasi;
        p.IsPermintaanData = vm.IsPermintaanData;
        p.IsWawancara = vm.IsWawancara;

        if (vm.IsWawancaraOnly)
        {
            p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
            p.NamaProdusenData = vm.NamaProdusenData ?? vm.NamaBidangTerkait;
        }
        else
        {
            p.StatusPPIDID = StatusId.Didisposisi;
            p.NamaBidang = vm.DisposisiKe == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangTerkait)
                ? vm.NamaBidangTerkait
                : null;
        }

        db.AddAuditLog(
            vm.PermohonanPPIDID,
            statusLama,
            p.StatusPPIDID!.Value,
            vm.IsWawancaraOnly
                ? $"Surat izin {vm.NoSuratIzin} diterbitkan, diteruskan ke Produsen Data"
                : $"Surat izin {vm.NoSuratIzin} diterbitkan, didisposisi ke {(p.NamaBidang ?? "PSMDI")}",
            CurrentUser
        );

        await db.SaveChangesAsync();

        var tujuan = vm.IsWawancaraOnly
            ? "dan diteruskan ke Produsen Data untuk penjadwalan wawancara"
            : $"dan didisposisi ke {(p.NamaBidang == null ? "PSMDI" : p.NamaBidang)}";

        TempData["Success"] = $"Surat izin <strong>{vm.NoSuratIzin}</strong> diterbitkan {tujuan}.";
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════════════════════
// KDI
// ════════════════════════════════════════════════════════════════════════════

[Route("kdi")]
[Authorize(Roles = "KDI,Admin")]
public class KdiController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? "KDI";

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? status)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi
                     && p.StatusPPIDID <= StatusId.ObservasiSelesai)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["Q"] = q;
        ViewData["Status"] = status;
        return View(await query.OrderByDescending(p => p.UpdatedAt).ToListAsync());
    }

    [HttpGet("psmdi")]
    public async Task<IActionResult> Psmdi(string? q)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi
                     && p.StatusPPIDID <= StatusId.ObservasiSelesai
                     && p.NamaBidang == null)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["Q"] = q;
        return View(await query.OrderByDescending(p => p.UpdatedAt).ToListAsync());
    }

    [HttpGet("bidang")]
    public async Task<IActionResult> Bidang(string? q)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID >= StatusId.Didisposisi
                     && p.StatusPPIDID <= StatusId.ObservasiSelesai
                     && p.NamaBidang != null)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["Q"] = q;
        return View(await query.OrderByDescending(p => p.UpdatedAt).ToListAsync());
    }

    // ── Terima Disposisi ──────────────────────────────────────────────────

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
            PerluObservasi = p.IsObservasi,
            PerluWawancara = p.IsWawancara,
        });
    }

    [HttpPost("terima"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TerimaDisposisiPost(TerimaDisposisiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.DiProses;
        p.UpdatedAt = DateTime.UtcNow;

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.DiProses,
            $"Disposisi diterima KDI. Catatan: {vm.Catatan}", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Disposisi diterima. Silakan siapkan data.";

        if (vm.PerluObservasi)
            return RedirectToAction("JadwalObservasi", new { id = vm.PermohonanPPIDID });

        if (vm.PerluWawancara)
            return RedirectToAction("JadwalWawancara", new { id = vm.PermohonanPPIDID });

        return RedirectToAction("UploadData", new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Observasi ──────────────────────────────────────────────────

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
        if (p != null)
        {
            var statusLama = p.StatusPPIDID;
            p.StatusPPIDID = StatusId.ObservasiDijadwalkan;
            p.UpdatedAt = now;
            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.ObservasiDijadwalkan,
                $"Observasi dijadwalkan {vm.TanggalObservasi:dd MMM yyyy} pukul {vm.WaktuObservasi:HH:mm}, PIC: {vm.NamaPIC}",
                CurrentUser);
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"Jadwal observasi: <strong>{vm.TanggalObservasi:dd MMM yyyy}</strong> pukul {vm.WaktuObservasi:HH:mm}";
        return RedirectToAction("Index");
    }

    // ── Selesai Observasi ─────────────────────────────────────────────────

    [HttpGet("selesai-observasi/{id}")]
    public async Task<IActionResult> SelesaiObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        if (p.StatusPPIDID != StatusId.ObservasiDijadwalkan)
        {
            TempData["Error"] = "Permohonan ini tidak dalam status Observasi Dijadwalkan.";
            return RedirectToAction("Index");
        }

        return View(new SelesaiObservasiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? ""
        });
    }

    [HttpPost("selesai-observasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiObservasiPost(SelesaiObservasiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        if (p.StatusPPIDID != StatusId.ObservasiDijadwalkan)
        {
            TempData["Error"] = "Permohonan ini tidak dalam status Observasi Dijadwalkan.";
            return RedirectToAction("Index");
        }

        var now = DateTime.UtcNow;
        var statusLama = p.StatusPPIDID;

        bool wawancaraPerlu = p.IsWawancara;
        bool wawancaraSudahDijadwal = wawancaraPerlu &&
            await db.JadwalPPID.AnyAsync(j =>
                j.PermohonanPPIDID == vm.PermohonanPPIDID &&
                j.JenisJadwal == "Wawancara");

        bool perluJadwalWawancara = wawancaraPerlu && !wawancaraSudahDijadwal;

        p.StatusPPIDID = StatusId.ObservasiSelesai;
        p.UpdatedAt = now;

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.ObservasiSelesai,
            perluJadwalWawancara
                ? $"Observasi selesai. Catatan: {vm.Catatan}. Diarahkan ke penjadwalan wawancara."
                : $"Observasi selesai. Catatan: {vm.Catatan}. Diarahkan ke upload data.",
            CurrentUser);

        await db.SaveChangesAsync();

        if (perluJadwalWawancara)
        {
            TempData["Success"] = "Observasi selesai. Silakan jadwalkan sesi wawancara.";
            return RedirectToAction("JadwalWawancara", new { id = vm.PermohonanPPIDID });
        }

        TempData["Success"] = "Observasi selesai. Silakan upload data hasil.";
        return RedirectToAction("UploadData", new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Wawancara ──────────────────────────────────────────────────

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

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        bool statusValid = p.StatusPPIDID == StatusId.DiProses
                        || p.StatusPPIDID == StatusId.ObservasiSelesai;
        if (!statusValid)
        {
            TempData["Error"] =
                "Permohonan ini tidak dalam status yang memungkinkan penjadwalan wawancara.";
            return RedirectToAction("Index");
        }

        var now = DateTime.UtcNow;
        var statusLama = p.StatusPPIDID;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal = "Wawancara",
            Tanggal = vm.TanggalWawancara,
            Waktu = vm.WaktuWawancara,
            NamaPIC = vm.NamaPIC,
            CreatedAt = now
        });

        p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
        p.UpdatedAt = now;

        db.AddAuditLog(
            vm.PermohonanPPIDID,
            statusLama,
            StatusId.WawancaraDijadwalkan,
            $"Jadwal wawancara dibuat: {vm.TanggalWawancara:dd MMM yyyy} " +
            $"pukul {vm.WaktuWawancara:HH:mm}, PIC: {vm.NamaPIC}",
            CurrentUser);

        await db.SaveChangesAsync();

        TempData["Success"] =
            $"Jadwal wawancara <strong>{vm.TanggalWawancara:dd MMM yyyy}</strong> " +
            $"pukul {vm.WaktuWawancara:HH:mm} berhasil dibuat.";

        return RedirectToAction("Index");
    }

    // ── Upload Data ───────────────────────────────────────────────────────

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
        if (p != null)
        {
            var statusLama = p.StatusPPIDID;
            p.StatusPPIDID = StatusId.DataSiap;
            p.UpdatedAt = now;
            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.DataSiap,
                "Data hasil diupload, pemohon dapat mengunduh.", CurrentUser);
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Data berhasil diupload. Pemohon dapat mengunduh data.";
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════════════════════
// PRODUSEN DATA
// ════════════════════════════════════════════════════════════════════════════

[Route("produsen-data")]
[Authorize(Roles = "ProdusenData,Admin")]
public class ProdusenDataController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? "ProdusenData";

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID == StatusId.WawancaraDijadwalkan
                     || p.StatusPPIDID == StatusId.WawancaraSelesai)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["Q"] = q;
        return View(await query.OrderByDescending(p => p.UpdatedAt).ToListAsync());
    }

    [HttpGet("jadwal/{id}")]
    public async Task<IActionResult> JadwalWawancara(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var jadwalExisting = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == id && j.JenisJadwal == "Wawancara")
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        var detailWaw = p.Detail.FirstOrDefault(d => d.KeperluanID == KeperluanId.Wawancara);
        return View(new JadwalWawancaraVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            JudulPenelitian = p.JudulPenelitian ?? "",
            DetailWawancara = detailWaw?.DetailKeperluan ?? "",
            NamaProdusenData = p.NamaProdusenData,

            TanggalWawancara = jadwalExisting?.Tanggal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            WaktuWawancara = jadwalExisting?.Waktu ?? new TimeOnly(9, 0),
            NamaPIC = jadwalExisting?.NamaPIC ?? "",
            JadwalSudahAda = jadwalExisting != null,
        });
    }

    [HttpPost("jadwal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalWawancaraVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalWawancara", vm);

        var now = DateTime.UtcNow;

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;

        // ══ BUG FIX: status wajib di-set, sebelumnya terlewat ══
        // Audit log mencatat WawancaraDijadwalkan tapi entity tidak pernah
        // di-update sehingga status permohonan tidak berubah di DB.
        p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
        p.UpdatedAt = now;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal = "Wawancara",
            Tanggal = vm.TanggalWawancara,
            Waktu = vm.WaktuWawancara,
            NamaPIC = vm.NamaPIC,
            CreatedAt = now
        });

        db.AddAuditLog(
            vm.PermohonanPPIDID,
            statusLama,
            StatusId.WawancaraDijadwalkan,
            $"Jadwal wawancara dibuat: {vm.TanggalWawancara:dd MMM yyyy} " +
            $"pukul {vm.WaktuWawancara:HH:mm}, narasumber: {vm.NamaPIC}",
            CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] =
            $"Jadwal wawancara <strong>{vm.TanggalWawancara:dd MMM yyyy}</strong> " +
            $"pukul {vm.WaktuWawancara:HH:mm} berhasil dibuat.";
        return RedirectToAction("Index");
    }

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

    [HttpPost("selesai"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiWawancaraPost(UploadDataVm vm)
    {
        var now = DateTime.UtcNow;
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;

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
            p.StatusPPIDID = StatusId.WawancaraSelesai;
            TempData["Success"] = "Wawancara selesai. Pemohon dapat mengisi kuesioner kepuasan.";
        }

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, p.StatusPPIDID!.Value,
            "Wawancara selesai oleh Produsen Data.", CurrentUser);

        p.UpdatedAt = now;
        await db.SaveChangesAsync();
        return RedirectToAction("Index");
    }
}
