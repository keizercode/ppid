using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("produsen-data")]
[Authorize(Roles = $"{AppRoles.ProdusenData},{AppRoles.Admin}")]
public class ProdusenDataController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? AppRoles.ProdusenData;

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

    [HttpGet("jadwal/{id:guid}")]
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
            NoPermohonan      = p.NoPermohonan       ?? string.Empty,
            NamaPemohon       = p.Pribadi?.Nama      ?? string.Empty,
            JudulPenelitian   = p.JudulPenelitian    ?? string.Empty,
            DetailWawancara   = detailWaw?.DetailKeperluan ?? string.Empty,
            NamaProdusenData  = p.NamaProdusenData,
            TanggalWawancara  = jadwalExisting?.Tanggal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            WaktuWawancara    = jadwalExisting?.Waktu   ?? new TimeOnly(9, 0),
            NamaPIC           = jadwalExisting?.NamaPIC ?? string.Empty,
            TeleponPIC        = jadwalExisting?.TeleponPIC,
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
            TeleponPIC       = vm.TeleponPIC,
            CreatedAt        = now
        });

        var sub = await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Wawancara);
        if (sub != null)
        {
            sub.StatusTask    = SubTaskStatus.InProgress;
            sub.TanggalJadwal = vm.TanggalWawancara;
            sub.WaktuJadwal   = vm.WaktuWawancara;
            sub.NamaPIC       = vm.NamaPIC;
            sub.TeleponPIC    = vm.TeleponPIC;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.WawancaraDijadwalkan,
            $"Jadwal wawancara dibuat: {vm.TanggalWawancara:dd MMM yyyy} pukul {vm.WaktuWawancara:HH:mm}, narasumber: {vm.NamaPIC}",
            CurrentUser);

        await db.SaveChangesAsync();

        TempData["Success"] = $"Jadwal wawancara <strong>{vm.TanggalWawancara:dd MMM yyyy}</strong> pukul {vm.WaktuWawancara:HH:mm} berhasil dibuat.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("selesai/{id:guid}")]
    public async Task<IActionResult> SelesaiWawancara(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p == null) return NotFound();

        var jadwal = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == id && j.JenisJadwal == "Wawancara")
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        return View(new SelesaiWawancaraVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
            TanggalWawancara = jadwal?.Tanggal,
            WaktuWawancara   = jadwal?.Waktu,
            NamaPIC          = jadwal?.NamaPIC,
            TeleponPIC       = jadwal?.TeleponPIC,
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

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, p.StatusPPIDID!.Value,
            $"Wawancara selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        p.UpdatedAt = now;
        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Wawancara selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : fp != null
                ? "Wawancara selesai. Dokumen hasil tersedia untuk diunduh pemohon."
                : "Wawancara selesai. Pemohon dapat mengisi kuesioner kepuasan.";

        return RedirectToAction(nameof(Index));
    }
}
