using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Kasubkel KDI — mengelola sub-tugas Permintaan Data secara eksklusif.
/// Observasi dan Wawancara kini dikelola oleh KasubkelKepegawaian.
///
/// Route map:
///   GET  /kasubkel-kdi                          → Dashboard
///   GET  /kasubkel-kdi/permintaan-data          → Sub-menu permintaan data
///   GET  /kasubkel-kdi/detail/{id}              → Detail permohonan
///   GET/POST /kasubkel-kdi/terima/{id}          → Terima disposisi → buat sub-tugas Data
///   GET  /kasubkel-kdi/subtasks/{id}            → Hub sub-tugas Permintaan Data
///   GET/POST /kasubkel-kdi/upload-data/{id}     → Upload file permintaan data
///   GET/POST /kasubkel-kdi/batal-subtask/{id}/{jenis}  → Batalkan sub-tugas Data
///   GET/POST /kasubkel-kdi/reopen-subtask/{id}/{jenis} → Buka kembali sub-tugas Data
///   GET/POST /kasubkel-kdi/revisi-file/{id}/{jenis}    → Revisi file hasil Data
///   GET  /kasubkel-kdi/minta-feedback/{id}      → Minta feedback pemohon
///   POST /kasubkel-kdi/minta-feedback           → Konfirmasi minta feedback
///   POST /kasubkel-kdi/tandai-selesai           → Tandai selesai manual
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

        // Antrian aktif: hanya permohonan dengan IsPermintaanData
        var aktif = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p =>
                p.IsPermintaanData &&
                (p.StatusPPIDID == StatusId.Didisposisi    ||
                 p.StatusPPIDID == StatusId.DiProses       ||
                 p.StatusPPIDID == StatusId.DataSiap       ||
                 p.StatusPPIDID == StatusId.FeedbackPemohon))
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var ids      = aktif.Select(p => p.PermohonanPPIDID).ToList();
        var subTasks = await db.SubTaskPPID
            .Where(t => ids.Contains(t.PermohonanPPIDID)
                     && t.JenisTask == JenisTask.PermintaanData)
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
            .Where(t => t.PermohonanPPIDID == id && t.JenisTask == JenisTask.PermintaanData)
            .ToListAsync();

        return View(p);
    }

    // ── Terima Disposisi — buat sub-tugas PermintaanData ─────────────────

    [HttpGet("terima/{id:guid}")]
    public async Task<IActionResult> TerimaDisposisi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (!p.IsPermintaanData)
        {
            TempData["Error"] = "Permohonan ini tidak memiliki komponen Permintaan Data.";
            return RedirectToAction(nameof(Index));
        }

        // Jika subtask sudah InProgress/Selesai → sudah pernah diterima sebelumnya
        var existingTask = await db.SubTaskPPID
            .FirstOrDefaultAsync(t => t.PermohonanPPIDID == id
                                   && t.JenisTask == JenisTask.PermintaanData);

        if (existingTask is not null && existingTask.IsInProgress)
        {
            // Subtask sudah aktif → langsung ke halaman kelola
            TempData["Success"] = "Sub-tugas Permintaan Data sudah aktif dan dapat dikerjakan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        if (existingTask is not null && existingTask.IsSelesai)
        {
            TempData["Success"] = "Sub-tugas Permintaan Data sudah selesai.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        // Pending (pre-created) atau belum ada → tampilkan form konfirmasi
        return View(new TerimaDisposisiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan       ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama       ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian     ?? string.Empty,
            LatarBelakang    = p.LatarBelakang       ?? string.Empty,
            // Obs/Waw dikelola Kepegawaian — selalu false di sini
            PerluObservasi   = false,
            PerluWawancara   = false,
        });
    }

    [HttpPost("terima"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TerimaDisposisiPost(TerimaDisposisiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        if (!p.IsPermintaanData)
        {
            TempData["Error"] = "Permohonan ini tidak memiliki komponen Permintaan Data.";
            return RedirectToAction(nameof(Index));
        }

        var now        = DateTime.UtcNow;
        var statusLama = p.StatusPPIDID;

        // ── Cek subtask PermintaanData ────────────────────────────────────
        // Sejak fix, subtask sudah dibuat oleh Loket saat SuratIzin terbit.
        // KDI "Terima Disposisi" = AKTIVASI subtask dari Pending → InProgress.
        // Jika subtask belum ada (edge case: permohonan lama sebelum fix),
        // buat subtask baru sebagai fallback.
        var existingDataTask = await db.SubTaskPPID
            .FirstOrDefaultAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID
                                   && t.JenisTask == JenisTask.PermintaanData);

        if (existingDataTask is null)
        {
            // Fallback: subtask belum ada (permohonan sebelum fix diterapkan)
            db.CreateSubTasks(
                vm.PermohonanPPIDID,
                perluData:    true,
                perluObs:     false,
                perluWaw:     false,
                operatorName: CurrentUser);
        }
        else if (existingDataTask.IsPending)
        {
            // Normal path: aktivasi subtask yang sudah pre-dibuat oleh Loket
            existingDataTask.StatusTask = SubTaskStatus.InProgress;
            existingDataTask.Operator   = CurrentUser;
            existingDataTask.UpdatedAt  = now;
        }
        else if (existingDataTask.IsSelesai || existingDataTask.IsDibatalkan)
        {
            // Subtask sudah terminal — tidak perlu aksi apapun
            TempData["Error"] = $"Sub-tugas Permintaan Data sudah dalam status '{SubTaskStatus.GetLabel(existingDataTask.StatusTask)}'. " +
                                "Tidak ada perubahan yang dilakukan.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }
        // else: InProgress sudah → lanjut update status permohonan saja

        // Advance status permohonan ke DiProses jika masih di Didisposisi
        if (p.StatusPPIDID == StatusId.Didisposisi || p.StatusPPIDID == StatusId.SuratIzinTerbit)
        {
            p.StatusPPIDID = StatusId.DiProses;
            p.UpdatedAt    = now;
        }

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, p.StatusPPIDID ?? StatusId.DiProses,
            $"Disposisi diterima KDI. Sub-tugas Permintaan Data diaktifkan. " +
            $"Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Disposisi diterima. <strong>Sub-tugas Permintaan Data</strong> siap dikerjakan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Sub-tugas Hub (hanya PermintaanData) ─────────────────────────────

    [HttpGet("subtasks/{id:guid}")]
    public async Task<IActionResult> SubTasks(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        // KDI hanya mengelola sub-tugas PermintaanData
        var tasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id && t.JenisTask == JenisTask.PermintaanData)
            .ToListAsync();

        // Laporan final pemohon
        var tugasDocs = await db.DokumenPPID
            .Where(d => d.PermohonanPPIDID == id
                     && d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        // Status sub-tugas Obs/Waw dari Kepegawaian (untuk informasi saja)
        var obsWawTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id &&
                        (t.JenisTask == JenisTask.Observasi || t.JenisTask == JenisTask.Wawancara))
            .ToListAsync();

        ViewData["TugasDocs"]  = tugasDocs;
        ViewData["ObsWawTasks"] = obsWawTasks;

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
            : "Data diupload. Sub-tugas Obs/Waw Kepegawaian masih dalam proses.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Batalkan SubTask Data ─────────────────────────────────────────────

    [HttpGet("batal-subtask/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> BatalSubTask(Guid id, string jenisTask)
    {
        if (jenisTask != JenisTask.PermintaanData)
        {
            TempData["Error"] = $"{JenisTask.GetLabel(jenisTask)} dikelola oleh Kepegawaian.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null)
        {
            TempData["Error"] = "Sub-tugas tidak ditemukan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }
        if (sub.IsDibatalkan)
        {
            TempData["Error"] = "Sub-tugas ini sudah dalam status dibatalkan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        bool statusWasTerminal = p.StatusPPIDID == StatusId.DataSiap
                              || p.StatusPPIDID == StatusId.FeedbackPemohon
                              || p.StatusPPIDID == StatusId.Selesai;

        return View("BatalSubTask", new BatalSubTaskVm
        {
            PermohonanPPIDID   = id,
            NoPermohonan       = p.NoPermohonan  ?? string.Empty,
            NamaPemohon        = p.Pribadi?.Nama ?? string.Empty,
            JenisTask          = jenisTask,
            StatusSaatIni      = SubTaskStatus.GetLabel(sub.StatusTask),
            AkanAdvanceStatus  = false,
            AkanRollbackStatus = sub.IsSelesai && statusWasTerminal,
        });
    }

    [HttpPost("batal-subtask"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BatalSubTaskPost(BatalSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("BatalSubTask", vm);

        var subBefore      = await db.GetSubTask(vm.PermohonanPPIDID, vm.JenisTask);
        bool wasSelesai    = subBefore?.IsSelesai ?? false;
        var pBefore        = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        bool statusTerminal = pBefore is not null && (
            pBefore.StatusPPIDID == StatusId.DataSiap ||
            pBefore.StatusPPIDID == StatusId.FeedbackPemohon ||
            pBefore.StatusPPIDID == StatusId.Selesai);

        var success = await db.BatalSubTask(
            vm.PermohonanPPIDID, vm.JenisTask, vm.AlasanBatal, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Pembatalan gagal — sub-tugas tidak ditemukan atau sudah dibatalkan.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        await db.SaveChangesAsync();

    bool rolledBack = false;
    bool advanced   = false;   // ← add this

    if (wasSelesai && statusTerminal)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is not null)
        {
            var lama       = p.StatusPPIDID;
            p.StatusPPIDID = StatusId.DiProses;
            p.UpdatedAt    = DateTime.UtcNow;
            db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.DiProses,
                $"Status di-rollback ke DiProses karena Permintaan Data dibatalkan. " +
                $"Alasan: {vm.AlasanBatal}", CurrentUser);
            await db.SaveChangesAsync();
            rolledBack = true;
        }
    }
    else
    {
        // FIX: always attempt advance — cancelled task should unblock the others
        advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();
    }

    TempData["Success"] = rolledBack
        ? "Sub-tugas Permintaan Data dibatalkan. Status dimundurkan ke <strong>Sedang Diproses</strong>."
        : advanced
            ? "Sub-tugas dibatalkan. Tugas lain sudah selesai — status menjadi <strong>Data Siap</strong>."
            : "Sub-tugas Permintaan Data berhasil dibatalkan.";

    return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
}

    // ── Reopen SubTask Data ───────────────────────────────────────────────

    [HttpGet("reopen-subtask/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> ReopenSubTask(Guid id, string jenisTask)
    {
        if (jenisTask != JenisTask.PermintaanData)
        {
            TempData["Error"] = $"{JenisTask.GetLabel(jenisTask)} dikelola oleh Kepegawaian.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null || !sub.IsTerminal)
        {
            TempData["Error"] = "Hanya sub-tugas yang sudah selesai atau dibatalkan yang bisa di-reopen.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

        bool needsRollback = p.StatusPPIDID == StatusId.DataSiap
                          || p.StatusPPIDID == StatusId.FeedbackPemohon
                          || p.StatusPPIDID == StatusId.Selesai;

        return View("ReopenSubTask", new ReopenSubTaskVm
        {
            PermohonanPPIDID     = id,
            NoPermohonan         = p.NoPermohonan  ?? string.Empty,
            NamaPemohon          = p.Pribadi?.Nama ?? string.Empty,
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
                    $"Status di-rollback ke DiProses karena Permintaan Data di-reopen. " +
                    $"Alasan: {vm.AlasanReopen}", CurrentUser);
            }
        }

        await db.SaveChangesAsync();

        string msg = "Sub-tugas Permintaan Data berhasil di-reopen.";
        if (needsRollback) msg += " Status dimundurkan ke <strong>Sedang Diproses</strong>.";

        TempData["Success"] = msg;
        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Revisi File Data ──────────────────────────────────────────────────

    [HttpGet("revisi-file/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> RevisiFile(Guid id, string jenisTask)
    {
        if (jenisTask != JenisTask.PermintaanData)
        {
            TempData["Error"] = $"{JenisTask.GetLabel(jenisTask)} dikelola oleh Kepegawaian.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }

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
            NoPermohonan     = p.NoPermohonan  ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama ?? string.Empty,
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
            NamaDokumenPPID    = $"Revisi Data Hasil ({now:dd MMM HH:mm})",
            UploadDokumenPPID  = fp,
            JenisDokumenPPIDID = JenisDokumenId.DataHasil,
            CreatedAt          = now
        });

        var success = await db.ReplaceFileSubTask(
            vm.PermohonanPPIDID, vm.JenisTask, fp, nama, vm.CatatanRevisi, CurrentUser);

        if (!success)
        {
            TempData["Error"] = "Revisi file gagal.";
            return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "File data hasil berhasil direvisi. File lama tetap tersimpan di riwayat dokumen.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Minta Feedback — dipindah ke Loket Kepegawaian ───────────────────────
// KDI tidak lagi mengelola feedback pemohon. Loket adalah aktor yang
// berinteraksi langsung dengan pemohon.

[HttpGet("minta-feedback/{id:guid}")]
public IActionResult MintaFeedback(Guid id)
{
    TempData["Error"] =
        "Permintaan feedback dikelola oleh <strong>Loket Kepegawaian</strong>, " +
        "bukan KDI. Hubungi petugas loket untuk mengubah status ke Feedback Pemohon.";
    return RedirectToAction(nameof(SubTasks), new { id });
}

// TandaiSelesai tetap di KDI sebagai override darurat jika Loket tidak tersedia
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

    var lama         = p.StatusPPIDID;
    var now          = DateTime.UtcNow;
    p.StatusPPIDID   = StatusId.Selesai;
    p.TanggalSelesai = DateOnly.FromDateTime(DateTime.Today);
    p.UpdatedAt      = now;

    db.AddAuditLog(permohonanId, lama, StatusId.Selesai,
        "Permohonan ditandai selesai (override) oleh KDI karena Loket tidak merespons.",
        CurrentUser);

    await db.SaveChangesAsync();

    TempData["Success"] = "Permohonan berhasil ditandai <strong>Selesai</strong>.";
    return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
}
}
