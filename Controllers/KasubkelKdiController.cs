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
                p.StatusPPIDID == StatusId.DataSiap              ||
                p.StatusPPIDID == StatusId.FeedbackPemohon)
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

        // Laporan/tugas final yang sudah diunggah pemohon
        var tugasDocs = await db.DokumenPPID
            .Where(d => d.PermohonanPPIDID == id
                     && d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        ViewData["SubTasks"] = tasks;
        ViewData["TugasDocs"] = tugasDocs;

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
            LokasiJenis      = sub?.LokasiJenis  ?? "Offline",
            LokasiDetail     = sub?.LokasiDetail,
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
        LokasiJenis      = vm.LokasiJenis,
        LokasiDetail     = vm.LokasiDetail,
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
            sub.LokasiJenis  = vm.LokasiJenis;
            sub.LokasiDetail = vm.LokasiDetail;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is not null)
        {
            var lama = p.StatusPPIDID;

            // Hanya set ObservasiDijadwalkan jika ini satu-satunya task (observasi-only).
            // Untuk parallel mode, pertahankan DiProses agar AdvanceIfAllSubTasksDone
            // tidak ter-block oleh status < DataSiap check yang salah.
            bool isObservasiOnly = p.IsObservasi && !p.IsPermintaanData && !p.IsWawancara;
            if (isObservasiOnly && p.StatusPPIDID == StatusId.DiProses)
            {
                p.StatusPPIDID = StatusId.ObservasiDijadwalkan;
            }
            p.UpdatedAt = now;

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

        string? fp   = null;
        string? nama = null;

        if (vm.FileHasil?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"observasi_{Path.GetFileName(vm.FileHasil.FileName)}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileHasil.CopyToAsync(s);
            fp   = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
            nama = vm.FileHasil.FileName;

            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID   = vm.PermohonanPPIDID,
                NamaDokumenPPID    = "Hasil Observasi",
                UploadDokumenPPID  = fp,
                JenisDokumenPPIDID = JenisDokumenId.DataHasil,
                CreatedAt          = now
            });
        }

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Observasi);

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

        // EC-5: TIDAK set status permohonan ke intermediate.
        // Biarkan AdvanceIfAllSubTasksDone yang putuskan.
        p.UpdatedAt = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, lama ?? StatusId.DiProses,
            $"Sub-tugas Observasi selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Observasi selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "Observasi selesai. Tugas lain masih berjalan — status permohonan belum berubah.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Wawancara ──────────────────────────────────────────────────

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
            LokasiJenis       = jadwalExisting?.LokasiJenis  ?? sub?.LokasiJenis  ?? "Offline",
            LokasiDetail      = jadwalExisting?.LokasiDetail ?? sub?.LokasiDetail,
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
            LokasiJenis      = vm.LokasiJenis,
            LokasiDetail     = vm.LokasiDetail,
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
            sub.LokasiJenis   = vm.LokasiJenis;
            sub.LokasiDetail  = vm.LokasiDetail;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        // ════════════════════════════════════════════════════════════════════
        // BUG FIX EC-CORE-3: JadwalWawancaraPost status update
        //
        // BUG LAMA: unconditionally set p.StatusPPIDID = WawancaraDijadwalkan (12)
        //   - WawancaraDijadwalkan (12) > DataSiap (10)
        //   - Menyebabkan AdvanceIfAllSubTasksDone selalu return false (bug lama guard)
        //   - Bahkan setelah fix guard, setting status ke 12 di parallel mode
        //     tidak akurat — status utama seharusnya tetap DiProses (7)
        //
        // FIX: hanya set WawancaraDijadwalkan untuk wawancara-only flow.
        //   Untuk parallel (ada Data/Observasi juga), pertahankan status DiProses.
        //   Status subtask (InProgress) sudah cukup sebagai indikator di panel KDI.
        // ════════════════════════════════════════════════════════════════════
        bool isWawancaraOnly = p.IsWawancara && !p.IsPermintaanData && !p.IsObservasi;

        if (isWawancaraOnly)
        {
            // Flow wawancara-only: update status utama ke WawancaraDijadwalkan
            p.StatusPPIDID = StatusId.WawancaraDijadwalkan;
        }
        else
        {
            // Parallel mode: JANGAN ubah status utama permohonan.
            // Status DiProses (7) sudah merefleksikan "sedang dikerjakan".
            // AdvanceIfAllSubTasksDone akan naikkan ke DataSiap saat semua selesai.
        }
        p.UpdatedAt = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, p.StatusPPIDID!.Value,
            $"Wawancara dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, PIC: {vm.NamaPIC}" +
            (isWawancaraOnly ? "" : " [parallel mode — status utama tidak berubah]"),
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

        // EC-5: TIDAK set status ke WawancaraSelesai.
        // Biarkan AdvanceIfAllSubTasksDone yang putuskan.
        p.UpdatedAt = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, lama ?? StatusId.DiProses,
            $"Sub-tugas Wawancara selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Wawancara selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "Wawancara selesai. Tugas lain masih berjalan — status permohonan belum berubah.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Minta Feedback ────────────────────────────────────────────────────

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

    [HttpPost("tandai-selesai"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TandaiSelesai([FromForm] Guid permohonanId)
    {
        var p = await db.PermohonanPPID.FindAsync(permohonanId);
        if (p is null) return NotFound();

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

    // ── Reschedule ────────────────────────────────────────────────────────

    [HttpGet("reschedule/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> Reschedule(Guid id, string jenisTask)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null)
        {
            TempData["Error"] = "Sub-tugas tidak ditemukan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        if (sub.IsTerminal)
        {
            TempData["Error"] = $"Sub-tugas {JenisTask.GetLabel(jenisTask)} sudah selesai/dibatalkan. " +
                                "Gunakan Reopen untuk membuka kembali.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        if (sub.IsPending)
        {
            TempData["Error"] = "Buat jadwal terlebih dahulu sebelum melakukan reschedule.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        return View("RescheduleSubTask", new RescheduleSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan      ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama      ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian    ?? string.Empty,
            JenisTask        = jenisTask,
            TanggalLama      = sub.TanggalJadwal,
            WaktuLama        = sub.WaktuJadwal,
            NamaPICLama      = sub.NamaPIC,
            RescheduleCount  = sub.RescheduleCount,
            TanggalBaru      = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            WaktuBaru        = sub.WaktuJadwal ?? new TimeOnly(9, 0),
            NamaPICBaru      = sub.NamaPIC ?? string.Empty,
            TeleponPICBaru   = sub.TeleponPIC,
        });
    }

    [HttpPost("reschedule"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReschedulePost(RescheduleSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("RescheduleSubTask", vm);

        if (vm.TanggalBaru < DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(vm.TanggalBaru), "Tanggal baru tidak boleh di masa lalu.");
            return View("RescheduleSubTask", vm);
        }

        var success = await db.RescheduleSubTask(
            vm.PermohonanPPIDID, vm.JenisTask,
            vm.TanggalBaru, vm.WaktuBaru,
            vm.NamaPICBaru, vm.TeleponPICBaru,
            vm.LokasiJenis, vm.LokasiDetail,
            vm.AlasanReschedule, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Reschedule gagal — sub-tugas tidak ditemukan atau sudah selesai.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        await db.SaveChangesAsync();

        string rescheduleKe = vm.RescheduleCount + 1 > 1
            ? $" (reschedule ke-{vm.RescheduleCount + 1})"
            : "";

        TempData["Success"] =
            $"Jadwal {JenisTask.GetLabel(vm.JenisTask)} diperbarui{rescheduleKe}: " +
            $"<strong>{vm.TanggalBaru:dd MMM yyyy}</strong> pukul {vm.WaktuBaru:HH:mm}.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Batalkan SubTask ──────────────────────────────────────────────────

    [HttpGet("batal-subtask/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> BatalSubTask(Guid id, string jenisTask)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null)
        {
            TempData["Error"] = "Sub-tugas tidak ditemukan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        if (sub.IsSelesai)
        {
            TempData["Error"] = "Sub-tugas yang sudah selesai tidak bisa dibatalkan. " +
                                "Gunakan Reopen jika hasil perlu direvisi.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        if (sub.IsDibatalkan)
        {
            TempData["Error"] = "Sub-tugas ini sudah dalam status dibatalkan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        var allTasks    = await db.SubTaskPPID.Where(t => t.PermohonanPPIDID == id).ToListAsync();
        var activeTasks = allTasks.Where(t => !t.IsDibatalkan && t.SubTaskID != sub.SubTaskID).ToList();
        bool allOtherDone = activeTasks.Count == 0 || activeTasks.All(t => t.IsSelesai);

        return View("BatalSubTask", new BatalSubTaskVm
        {
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan   ?? string.Empty,
            NamaPemohon       = p.Pribadi?.Nama  ?? string.Empty,
            JenisTask         = jenisTask,
            StatusSaatIni     = SubTaskStatus.GetLabel(sub.StatusTask),
            AkanAdvanceStatus = allOtherDone,
        });
    }

    [HttpPost("batal-subtask"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BatalSubTaskPost(BatalSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("BatalSubTask", vm);

        var success = await db.BatalSubTask(
            vm.PermohonanPPIDID, vm.JenisTask,
            vm.AlasanBatal, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Pembatalan gagal — sub-tugas tidak ditemukan atau sudah selesai.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} dibatalkan. " +
              "Task aktif lainnya sudah selesai — status menjadi <strong>Data Siap</strong>."
            : $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} berhasil dibatalkan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Reopen SubTask ────────────────────────────────────────────────────

    [HttpGet("reopen-subtask/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> ReopenSubTask(Guid id, string jenisTask)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null || !sub.IsTerminal)
        {
            TempData["Error"] = "Hanya sub-tugas yang sudah selesai atau dibatalkan yang bisa di-reopen.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        // FIX: hanya perlu rollback jika status sudah di DataSiap/FeedbackPemohon/Selesai.
        // BUKAN >= DataSiap karena WawancaraDijadwalkan(12) > DataSiap(10) tapi bukan terminal.
        bool needsRollback = p.StatusPPIDID == StatusId.DataSiap
                          || p.StatusPPIDID == StatusId.FeedbackPemohon
                          || p.StatusPPIDID == StatusId.Selesai;

        return View("ReopenSubTask", new ReopenSubTaskVm
        {
            PermohonanPPIDID     = id,
            NoPermohonan         = p.NoPermohonan   ?? string.Empty,
            NamaPemohon          = p.Pribadi?.Nama  ?? string.Empty,
            JenisTask            = jenisTask,
            StatusSaatIni        = p.StatusPPIDID ?? StatusId.TerdaftarSistem,
            StatusAkanDiRollback = needsRollback,
        });
    }

    [HttpPost("reopen-subtask"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenSubTaskPost(ReopenSubTaskVm vm)
    {
        if (vm.StatusAkanDiRollback && !vm.KonfirmasiRollback)
        {
            ModelState.AddModelError(nameof(vm.KonfirmasiRollback),
                "Centang konfirmasi untuk melanjutkan karena status permohonan akan mundur.");
        }

        if (!ModelState.IsValid) return View("ReopenSubTask", vm);

        var (success, needsRollback) = await db.ReopenSubTask(
            vm.PermohonanPPIDID, vm.JenisTask, vm.AlasanReopen, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Reopen gagal — sub-tugas tidak ditemukan atau tidak dalam status terminal.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        if (needsRollback)
        {
            var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
            if (p is not null)
            {
                var lama       = p.StatusPPIDID;
                p.StatusPPIDID = StatusId.DiProses;
                p.UpdatedAt    = DateTime.UtcNow;
                db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.DiProses,
                    $"Status di-rollback ke DiProses karena sub-tugas {JenisTask.GetLabel(vm.JenisTask)} " +
                    $"di-reopen. Alasan: {vm.AlasanReopen}",
                    CurrentUser);
            }
        }

        await db.SaveChangesAsync();

        string msg = $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} berhasil di-reopen.";
        if (needsRollback)
            msg += " Status permohonan dimundurkan ke <strong>Sedang Diproses</strong>.";

        TempData["Success"] = msg;
        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Update PIC ────────────────────────────────────────────────────────

    [HttpGet("update-pic/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> UpdatePIC(Guid id, string jenisTask)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null || sub.IsTerminal)
        {
            TempData["Error"] = "Sub-tugas tidak ditemukan atau sudah selesai.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        return View(new UpdatePICVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan   ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama  ?? string.Empty,
            JenisTask        = jenisTask,
            NamaPICSaatIni   = sub.NamaPIC,
            NamaPICBaru      = sub.NamaPIC      ?? string.Empty,
            TeleponPICBaru   = sub.TeleponPIC,
        });
    }

    [HttpPost("update-pic"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePICPost(UpdatePICVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var success = await db.UpdatePICSubTask(
            vm.PermohonanPPIDID, vm.JenisTask,
            vm.NamaPICBaru, vm.TeleponPICBaru,
            vm.CatatanPerubahan, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Update PIC gagal.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"PIC {JenisTask.GetLabel(vm.JenisTask)} berhasil diperbarui: " +
                              $"<strong>{vm.NamaPICBaru}</strong>.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Revisi File ───────────────────────────────────────────────────────

    [HttpGet("revisi-file/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> RevisiFile(Guid id, string jenisTask)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null || !sub.IsSelesai)
        {
            TempData["Error"] = "Hanya sub-tugas yang sudah selesai yang file-nya bisa direvisi.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        return View(new ReplaceFileSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan   ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama  ?? string.Empty,
            JenisTask        = jenisTask,
            FilePathLama     = sub.FilePath,
            NamaFileLama     = sub.NamaFile,
        });
    }

    [HttpPost("revisi-file"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RevisiFilePost(ReplaceFileSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        if (vm.FileRevisi == null || vm.FileRevisi.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.FileRevisi), "File revisi wajib diupload.");
            return View(vm);
        }

        var now = DateTime.UtcNow;
        var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
        Directory.CreateDirectory(dir);

        var fn = $"revisi_{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileName(vm.FileRevisi.FileName)}";
        await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
        await vm.FileRevisi.CopyToAsync(s);
        s.Close();

        var fp   = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
        var nama = vm.FileRevisi.FileName;

        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID   = vm.PermohonanPPIDID,
            NamaDokumenPPID    = $"Revisi {JenisTask.GetLabel(vm.JenisTask)} ({now:dd MMM HH:mm})",
            UploadDokumenPPID  = fp,
            JenisDokumenPPIDID = JenisDokumenId.DataHasil,
            CreatedAt          = now
        });

        var success = await db.ReplaceFileSubTask(
            vm.PermohonanPPIDID, vm.JenisTask,
            fp, nama, vm.CatatanRevisi, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Revisi file gagal.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        await db.SaveChangesAsync();
        TempData["Success"] = $"File hasil {JenisTask.GetLabel(vm.JenisTask)} berhasil direvisi. " +
                              "File lama tetap tersimpan di riwayat dokumen.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }
}
