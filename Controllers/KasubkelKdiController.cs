using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Kasubkel KDI — digabung dengan KDI dan ProdusenData.
///
/// Route map:
///   GET  /kasubkel-kdi                          → Dashboard
///   GET  /kasubkel-kdi/permintaan-data          → Sub-menu permintaan data
///   GET  /kasubkel-kdi/detail/{id}              → Detail permohonan
///   GET/POST /kasubkel-kdi/terima/{id}          → Terima disposisi → buat SubTask paralel
///   GET  /kasubkel-kdi/subtasks/{id}            → Hub manajemen SubTask
///   GET/POST /kasubkel-kdi/upload-data/{id}     → Upload file permintaan data
///   GET/POST /kasubkel-kdi/jadwal-observasi/{id} → Jadwal observasi
///   GET/POST /kasubkel-kdi/selesai-observasi/{id}→ Konfirmasi observasi selesai
///   GET/POST /kasubkel-kdi/jadwal-wawancara/{id} → Jadwal wawancara (was ProdusenData)
///   GET/POST /kasubkel-kdi/selesai-wawancara/{id}→ Konfirmasi wawancara selesai
/// </summary>
[Route("kasubkel-kdi")]
[Authorize(Roles = $"{AppRoles.KasubkelKDI},{AppRoles.Admin}")]
public class KasubkelKdiController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? AppRoles.KasubkelKDI;

    // ── Dashboard ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Where(p => p.IsPermintaanData)
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        var vm = new DashboardVm
        {
            Total        = allStatus.Count,
            Proses       = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai      = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };

        // Semua disposisi aktif untuk tampilan tabel dashboard
        var aktif = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p =>
                p.StatusPPIDID == StatusId.Didisposisi   ||
                p.StatusPPIDID == StatusId.DiProses       ||
                p.StatusPPIDID == StatusId.ObservasiDijadwalkan ||
                p.StatusPPIDID == StatusId.ObservasiSelesai     ||
                p.StatusPPIDID == StatusId.WawancaraDijadwalkan  ||
                p.StatusPPIDID == StatusId.WawancaraSelesai      ||
                p.StatusPPIDID == StatusId.DataSiap)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var ids      = aktif.Select(p => p.PermohonanPPIDID).ToList();
        var subTasks = await db.SubTaskPPID
            .Where(t => ids.Contains(t.PermohonanPPIDID))
            .ToListAsync();

        ViewData["DisposisiAktif"] = aktif;
        ViewData["SubTaskMap"]     = subTasks
            .GroupBy(t => t.PermohonanPPIDID)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(vm);
    }

    // ── Sub-menu: Permintaan Data ─────────────────────────────────────────

    [HttpGet("permintaan-data")]
    public async Task<IActionResult> PermintaanData(string? q, int? status)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.IsPermintaanData)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["Q"]      = q;
        ViewData["Status"] = status;
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── Detail ────────────────────────────────────────────────────────────

    [HttpGet("detail/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen).ThenInclude(d => d.JenisDokumen)
            .Include(x => x.AuditLog)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        ViewData["SubTasks"] = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        return View(p);
    }

    // ── Terima Disposisi — buat SubTask paralel ───────────────────────────

    [HttpGet("terima/{id:guid}")]
    public async Task<IActionResult> TerimaDisposisi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        return View(new TerimaDisposisiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan       ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama       ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian     ?? string.Empty,
            LatarBelakang    = p.LatarBelakang       ?? string.Empty,
            PerluObservasi   = p.IsObservasi,
            PerluWawancara   = p.IsWawancara,
        });
    }

    [HttpPost("terima"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TerimaDisposisiPost(TerimaDisposisiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var statusLama = p.StatusPPIDID;
        var now        = DateTime.UtcNow;

        // Idempotent: buat SubTask hanya jika belum ada
        var existing = await db.SubTaskPPID
            .CountAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID);

        if (existing == 0)
            db.CreateSubTasks(
                vm.PermohonanPPIDID,
                perluData:    p.IsPermintaanData,
                perluObs:     p.IsObservasi,
                perluWaw:     p.IsWawancara,
                operatorName: CurrentUser);

        p.StatusPPIDID = StatusId.DiProses;
        p.UpdatedAt    = now;

        int taskCount = (p.IsPermintaanData ? 1 : 0) +
                        (p.IsObservasi      ? 1 : 0) +
                        (p.IsWawancara      ? 1 : 0);

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.DiProses,
            $"Disposisi diterima KDI. {taskCount} sub-tugas paralel dibuat: " +
            $"{(p.IsPermintaanData ? "[Data] " : "")}" +
            $"{(p.IsObservasi      ? "[Observasi] " : "")}" +
            $"{(p.IsWawancara      ? "[Wawancara]"  : "")}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] =
            $"Disposisi diterima. <strong>{taskCount} tugas paralel</strong> siap dikerjakan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── SubTask Hub ───────────────────────────────────────────────────────

    [HttpGet("subtasks/{id:guid}")]
    public async Task<IActionResult> SubTasks(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        var tasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        ViewData["SubTasks"] = tasks;
        return View(new ParallelTasksVm { Permohonan = p, SubTasks = tasks });
    }

    // ── Upload Permintaan Data ────────────────────────────────────────────

    [HttpGet("upload-data/{id:guid}")]
    public async Task<IActionResult> UploadData(Guid id)
    {
        var subTask = await db.SubTaskPPID
            .Include(t => t.Permohonan).ThenInclude(p => p!.Pribadi)
            .FirstOrDefaultAsync(t =>
                t.PermohonanPPIDID == id &&
                t.JenisTask        == JenisTask.PermintaanData);

        if (subTask?.Permohonan is not null)
            return View(new UploadDataSubTaskVm
            {
                SubTaskID        = subTask.SubTaskID,
                PermohonanPPIDID = id,
                NoPermohonan     = subTask.Permohonan.NoPermohonan ?? string.Empty,
                NamaPemohon      = subTask.Permohonan.Pribadi?.Nama ?? string.Empty,
                JudulPenelitian  = subTask.Permohonan.JudulPenelitian ?? string.Empty,
            });

        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        return View(new UploadDataSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
        });
    }

    [HttpPost("upload-data"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadDataPost(UploadDataSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("UploadData", vm);

        var now      = DateTime.UtcNow;
        string? fp   = null;
        string? nama = null;

        if (vm.FileData?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"data_{Path.GetFileName(vm.FileData.FileName)}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileData.CopyToAsync(s);
            fp   = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
            nama = vm.FileData.FileName;

            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID   = vm.PermohonanPPIDID,
                NamaDokumenPPID    = "Data Hasil",
                UploadDokumenPPID  = fp,
                JenisDokumenPPIDID = JenisDokumenId.DataHasil,
                CreatedAt          = now
            });
        }

        var subTask = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.PermintaanData);

        if (subTask is not null)
        {
            subTask.StatusTask = SubTaskStatus.Selesai;
            subTask.FilePath   = fp;
            subTask.NamaFile   = nama;
            subTask.Catatan    = vm.Catatan;
            subTask.Operator   = CurrentUser;
            subTask.SelesaiAt  = now;
            subTask.UpdatedAt  = now;
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is not null)
            db.AddAuditLog(vm.PermohonanPPIDID, p.StatusPPIDID,
                p.StatusPPIDID ?? StatusId.DiProses,
                $"Sub-tugas Permintaan Data selesai. File: {nama ?? "(tidak ada)"}.",
                CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Data diupload. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "Data diupload. Tugas lain masih dalam proses.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Observasi ──────────────────────────────────────────────────

    [HttpGet("jadwal-observasi/{id:guid}")]
    public async Task<IActionResult> JadwalObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);

        return View("JadwalSubTask", new JadwalSubTaskVm
        {
            SubTaskID         = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan      ?? string.Empty,
            NamaPemohon       = p.Pribadi?.Nama      ?? string.Empty,
            JudulPenelitian   = p.JudulPenelitian    ?? string.Empty,
            JenisTask         = JenisTask.Observasi,
            NamaBidangTerkait = p.NamaBidang,
            Tanggal           = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu             = sub?.WaktuJadwal   ?? new TimeOnly(9, 0),
            NamaPIC           = sub?.NamaPIC       ?? string.Empty,
            TeleponPIC        = sub?.TeleponPIC,
        });
    }

    [HttpPost("jadwal-observasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalObservasiPost(JadwalSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalSubTask", vm);

        var now = DateTime.UtcNow;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal      = "Observasi",
            Tanggal          = vm.Tanggal,
            Waktu            = vm.Waktu,
            NamaPIC          = vm.NamaPIC,
            TeleponPIC       = vm.TeleponPIC,
            CreatedAt        = now
        });

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Observasi);

        if (sub is not null)
        {
            sub.StatusTask    = SubTaskStatus.InProgress;
            sub.TanggalJadwal = vm.Tanggal;
            sub.WaktuJadwal   = vm.Waktu;
            sub.NamaPIC       = vm.NamaPIC;
            sub.TeleponPIC    = vm.TeleponPIC;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is not null)
        {
            var lama = p.StatusPPIDID;
            if (p.StatusPPIDID == StatusId.DiProses)
            {
                p.StatusPPIDID = StatusId.ObservasiDijadwalkan;
                p.UpdatedAt    = now;
            }
            db.AddAuditLog(vm.PermohonanPPIDID, lama, p.StatusPPIDID!.Value,
                $"Observasi dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, PIC: {vm.NamaPIC}",
                CurrentUser);
        }

        await db.SaveChangesAsync();
        TempData["Success"] =
            $"Jadwal observasi: <strong>{vm.Tanggal:dd MMM yyyy}</strong> pukul {vm.Waktu:HH:mm}";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Selesai Observasi ─────────────────────────────────────────────────

    [HttpGet("selesai-observasi/{id:guid}")]
    public async Task<IActionResult> SelesaiObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);

        return View("SelesaiSubTask", new SelesaiSubTaskVm
        {
            SubTaskID        = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan      ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama      ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian    ?? string.Empty,
            JenisTask        = JenisTask.Observasi,
            TanggalJadwal    = sub?.TanggalJadwal,
            WaktuJadwal      = sub?.WaktuJadwal,
            NamaPIC          = sub?.NamaPIC,
            TeleponPIC       = sub?.TeleponPIC,
        });
    }

    [HttpPost("selesai-observasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiObservasiPost(SelesaiSubTaskVm vm)
    {
        var now = DateTime.UtcNow;
        var p   = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var lama = p.StatusPPIDID;

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Observasi);

        if (sub is not null)
        {
            sub.StatusTask = SubTaskStatus.Selesai;
            sub.Catatan    = vm.Catatan;
            sub.Operator   = CurrentUser;
            sub.SelesaiAt  = now;
            sub.UpdatedAt  = now;
        }

        p.StatusPPIDID = StatusId.ObservasiSelesai;
        p.UpdatedAt    = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.ObservasiSelesai,
            $"Observasi selesai. Catatan: {vm.Catatan}", CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Observasi selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "Observasi selesai. Tugas lain masih berjalan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Wawancara (merged dari ProdusenDataController) ─────────────

    [HttpGet("jadwal-wawancara/{id:guid}")]
    public async Task<IActionResult> JadwalWawancara(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub       = await db.GetSubTask(id, JenisTask.Wawancara);
        var detailWaw = p.Detail.FirstOrDefault(d => d.KeperluanID == KeperluanId.Wawancara);

        // Cek apakah jadwal sudah ada (dari alur wawancara-only)
        var jadwalExisting = await db.JadwalPPID
            .Where(j => j.PermohonanPPIDID == id && j.JenisJadwal == "Wawancara")
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        return View("JadwalSubTask", new JadwalSubTaskVm
        {
            SubTaskID         = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan      ?? string.Empty,
            NamaPemohon       = p.Pribadi?.Nama      ?? string.Empty,
            JudulPenelitian   = p.JudulPenelitian    ?? string.Empty,
            JenisTask         = JenisTask.Wawancara,
            DetailKeperluan   = detailWaw?.DetailKeperluan,
            NamaBidangTerkait = p.NamaBidang ?? p.NamaProdusenData,
            Tanggal           = jadwalExisting?.Tanggal ?? sub?.TanggalJadwal
                                ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu             = jadwalExisting?.Waktu   ?? sub?.WaktuJadwal
                                ?? new TimeOnly(9, 0),
            NamaPIC           = jadwalExisting?.NamaPIC ?? sub?.NamaPIC ?? string.Empty,
            TeleponPIC        = jadwalExisting?.TeleponPIC ?? sub?.TeleponPIC,
        });
    }

    [HttpPost("jadwal-wawancara"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalSubTask", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var now  = DateTime.UtcNow;
        var lama = p.StatusPPIDID;

        db.JadwalPPID.Add(new JadwalPPID
        {
            PermohonanPPIDID = vm.PermohonanPPIDID,
            JenisJadwal      = "Wawancara",
            Tanggal          = vm.Tanggal,
            Waktu            = vm.Waktu,
            NamaPIC          = vm.NamaPIC,
            TeleponPIC       = vm.TeleponPIC,
            CreatedAt        = now
        });

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Wawancara);

        if (sub is not null)
        {
            sub.StatusTask    = SubTaskStatus.InProgress;
            sub.TanggalJadwal = vm.Tanggal;
            sub.WaktuJadwal   = vm.Waktu;
            sub.NamaPIC       = vm.NamaPIC;
            sub.TeleponPIC    = vm.TeleponPIC;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
        p.UpdatedAt    = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.WawancaraDijadwalkan,
            $"Wawancara dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, PIC: {vm.NamaPIC}",
            CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] =
            $"Jadwal wawancara: <strong>{vm.Tanggal:dd MMM yyyy}</strong> pukul {vm.Waktu:HH:mm}";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Selesai Wawancara ─────────────────────────────────────────────────

    [HttpGet("selesai-wawancara/{id:guid}")]
    public async Task<IActionResult> SelesaiWawancara(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Wawancara);

        return View("SelesaiSubTask", new SelesaiSubTaskVm
        {
            SubTaskID        = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan      ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama      ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian    ?? string.Empty,
            JenisTask        = JenisTask.Wawancara,
            TanggalJadwal    = sub?.TanggalJadwal,
            WaktuJadwal      = sub?.WaktuJadwal,
            NamaPIC          = sub?.NamaPIC,
            TeleponPIC       = sub?.TeleponPIC,
        });
    }

    [HttpPost("selesai-wawancara"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiWawancaraPost(SelesaiSubTaskVm vm)
    {
        var now = DateTime.UtcNow;
        var p   = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var lama     = p.StatusPPIDID;
        string? fp   = null;
        string? nama = null;

        if (vm.FileHasil?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"wawancara_{Path.GetFileName(vm.FileHasil.FileName)}";
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
        }

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Wawancara);

        if (sub is not null)
        {
            sub.StatusTask = SubTaskStatus.Selesai;
            sub.FilePath   = fp;
            sub.NamaFile   = nama;
            sub.Catatan    = vm.Catatan;
            sub.Operator   = CurrentUser;
            sub.SelesaiAt  = now;
            sub.UpdatedAt  = now;
        }

        p.StatusPPIDID = StatusId.WawancaraSelesai;
        p.UpdatedAt    = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.WawancaraSelesai,
            $"Wawancara selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Wawancara selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "Wawancara selesai. Tugas lain masih berjalan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── GET /kasubkel-kdi/minta-feedback/{id} ─────────────────────────────
[HttpGet("minta-feedback/{id:guid}")]
public async Task<IActionResult> MintaFeedback(Guid id)
{
    var p = await db.PermohonanPPID
        .Include(x => x.Pribadi)
        .Include(x => x.Dokumen)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

    if (p is null) return NotFound();

    if (p.StatusPPIDID != StatusId.DataSiap)
    {
        TempData["Error"] = "Status harus Data Siap sebelum meminta feedback pemohon.";
        return RedirectToAction(nameof(SubTasks), new { id });
    }

    ViewData["NoPermohonan"] = p.NoPermohonan;
    ViewData["NamaPemohon"]  = p.Pribadi?.Nama;
    ViewData["HasData"]      = p.Dokumen.Any(d => d.JenisDokumenPPIDID == JenisDokumenId.DataHasil);

    return View(p);
}

// ── POST /kasubkel-kdi/minta-feedback ────────────────────────────────
[HttpPost("minta-feedback"), ValidateAntiForgeryToken]
public async Task<IActionResult> MintaFeedbackPost([FromForm] Guid permohonanId)
{
    var p = await db.PermohonanPPID.FindAsync(permohonanId);
    if (p is null) return NotFound();

    if (p.StatusPPIDID != StatusId.DataSiap)
    {
        TempData["Error"] = "Status sudah berubah. Refresh halaman.";
        return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
    }

    var lama    = p.StatusPPIDID;
    var now     = DateTime.UtcNow;
    p.StatusPPIDID = StatusId.FeedbackPemohon;
    p.UpdatedAt    = now;

    db.AddAuditLog(permohonanId, lama, StatusId.FeedbackPemohon,
        "Data siap. Pemohon diminta mengisi kuesioner kepuasan layanan via portal publik.",
        CurrentUser);

    await db.SaveChangesAsync();

    TempData["Success"] =
        $"Status diperbarui ke <strong>Feedback Pemohon</strong>. " +
        $"Pemohon dapat mengisi kuesioner di portal publik dengan nomor " +
        $"permohonan mereka.";

    return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
}

// ── POST /kasubkel-kdi/tandai-selesai ────────────────────────────────
// Manual override: jika pemohon tidak kunjung mengisi kuesioner,
// KDI dapat force-close permohonan.
[HttpPost("tandai-selesai"), ValidateAntiForgeryToken]
public async Task<IActionResult> TandaiSelesai([FromForm] Guid permohonanId)
{
    var p = await db.PermohonanPPID.FindAsync(permohonanId);
    if (p is null) return NotFound();

    // Boleh dari DataSiap (10) atau FeedbackPemohon (15)
    if (p.StatusPPIDID < StatusId.DataSiap || p.StatusPPIDID == StatusId.Selesai)
    {
        TempData["Error"] = "Tidak dapat ditandai selesai pada status ini.";
        return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
    }

    var lama    = p.StatusPPIDID;
    var now     = DateTime.UtcNow;
    p.StatusPPIDID   = StatusId.Selesai;
    p.TanggalSelesai = DateOnly.FromDateTime(DateTime.Today);
    p.UpdatedAt      = now;

    db.AddAuditLog(permohonanId, lama, StatusId.Selesai,
        "Permohonan ditandai selesai secara manual oleh KDI (tanpa kuesioner pemohon).",
        CurrentUser);

    await db.SaveChangesAsync();

    TempData["Success"] = "Permohonan berhasil ditandai <strong>Selesai</strong>.";
    return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
}

// ── FIX: HomeController.KuesionerPost ────────────────────────────────
// Kuesioner sudah bisa diisi dari DataSiap (10) atau FeedbackPemohon (15).
// Audit log sekarang mencatat dari status mana pemohon mengisi.
// (Tidak ada perubahan logic, hanya perjelas audit trail)
//
// Kuesioner bisa diisi kapanpun statusPPIDID >= DataSiap (sebelum Selesai).
// HomeController sudah handle ini dengan benar, tidak perlu diubah.
// Yang perlu: FeedbackPemohon harus di-set dulu oleh KDI (action di atas).
}
