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
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            Kategori         = p.KategoriPemohon ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? "",
            IsObservasi      = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara      = p.IsWawancara,
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
                PermohonanPPIDID   = vm.PermohonanPPIDID,
                NamaDokumenPPID    = "Surat Izin",
                UploadDokumenPPID  = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.SuratIzin,
                CreatedAt          = now
            });
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;

        p.NoSuratPermohonan = vm.NoSuratIzin;
        p.UpdatedAt         = now;
        p.IsObservasi       = vm.IsObservasi;
        p.IsPermintaanData  = vm.IsPermintaanData;
        p.IsWawancara       = vm.IsWawancara;

        if (vm.IsWawancaraOnly)
        {
            p.StatusPPIDID     = StatusId.WawancaraDijadwalkan;
            p.NamaProdusenData = vm.NamaProdusenData ?? vm.NamaBidangTerkait;
        }
        else
        {
            p.StatusPPIDID = StatusId.Didisposisi;
            p.NamaBidang   = vm.DisposisiKe == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangTerkait)
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
// PRODUSEN DATA
// ═══════════════════════════════  ═════════════════════════════════════════════

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
            .Where(p =>
                p.StatusPPIDID == StatusId.WawancaraDijadwalkan ||
                p.StatusPPIDID == StatusId.WawancaraSelesai)
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
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan!,
            NamaPemohon       = p.Pribadi?.Nama ?? "",
            JudulPenelitian   = p.JudulPenelitian ?? "",
            DetailWawancara   = detailWaw?.DetailKeperluan ?? "",
            NamaProdusenData  = p.NamaProdusenData,
            TanggalWawancara  = jadwalExisting?.Tanggal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            WaktuWawancara    = jadwalExisting?.Waktu   ?? new TimeOnly(9, 0),
            NamaPIC           = jadwalExisting?.NamaPIC ?? "",
            JadwalSudahAda    = jadwalExisting != null,
        });
    }

    [HttpPost("jadwal"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalWawancaraVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalWawancara", vm);

        var now = DateTime.UtcNow;
        var p   = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
        p.UpdatedAt    = now;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal      = "Wawancara",
            Tanggal          = vm.TanggalWawancara,
            Waktu            = vm.WaktuWawancara,
            NamaPIC          = vm.NamaPIC,
            CreatedAt        = now
        });

        // ── Sync ke SubTask jika ada ──────────────────────────────────────
        var sub = await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Wawancara);
        if (sub != null)
        {
            sub.StatusTask    = SubTaskStatus.InProgress;
            sub.TanggalJadwal = vm.TanggalWawancara;
            sub.WaktuJadwal   = vm.WaktuWawancara;
            sub.NamaPIC       = vm.NamaPIC;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        db.AddAuditLog(
            vm.PermohonanPPIDID, statusLama, StatusId.WawancaraDijadwalkan,
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

        var jadwal = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == id && j.JenisJadwal == "Wawancara")
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        return View(new SelesaiWawancaraVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? "",
            TanggalWawancara = jadwal?.Tanggal,
            WaktuWawancara   = jadwal?.Waktu,
            NamaPIC          = jadwal?.NamaPIC,
        });
    }

    [HttpPost("selesai"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiWawancaraPost(SelesaiWawancaraVm vm)
    {
        var now = DateTime.UtcNow;
        var p   = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;
        string? fp     = null;
        string? nama   = null;

        if (vm.FileHasil?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"data_{vm.FileHasil.FileName}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileHasil.CopyToAsync(s);
            fp   = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
            nama = vm.FileHasil.FileName;

            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID   = vm.PermohonanPPIDID,
                NamaDokumenPPID    = "Hasil Wawancara",
                UploadDokumenPPID  = fp,
                JenisDokumenPPIDID = JenisDokumenId.DataHasil,
                CreatedAt          = now
            });

            p.StatusPPIDID = StatusId.DataSiap;
        }
        else
        {
            p.StatusPPIDID = StatusId.WawancaraSelesai;
        }

        // ── Tandai SubTask selesai (jika ada, untuk permohonan via sistem paralel) ──
        var sub = await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Wawancara);
        if (sub != null)
        {
            sub.StatusTask = SubTaskStatus.Selesai;
            sub.FilePath   = fp;
            sub.NamaFile   = nama;
            sub.Catatan    = vm.Catatan;
            sub.Operator   = CurrentUser;
            sub.SelesaiAt  = now;
            sub.UpdatedAt  = now;
        }

        db.AddAuditLog(
            vm.PermohonanPPIDID, statusLama, p.StatusPPIDID!.Value,
            $"Wawancara selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        p.UpdatedAt = now;
        await db.SaveChangesAsync();

        // ── Auto-advance jika semua subtask selesai ───────────────────────
        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Wawancara selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : fp != null
                ? "Wawancara selesai. Dokumen hasil tersedia untuk diunduh pemohon."
                : "Wawancara selesai. Pemohon dapat mengisi kuesioner kepuasan.";

        return RedirectToAction("Index");
    }
}
