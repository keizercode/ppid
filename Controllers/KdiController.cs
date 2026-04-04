using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

// ════════════════════════════════════════════════════════════════════════════
// KDI CONTROLLER — Parallel Workflow
//
// Semua sub-tugas (PermintaanData / Observasi / Wawancara) berjalan paralel.
// Saat SEMUA SubTask.StatusTask == Selesai → auto-advance ke DataSiap.
//
// Route map:
//   GET/POST /kdi                          → dashboard
//   GET      /kdi/psmdi                    → daftar disposisi ke PSMDI
//   GET      /kdi/bidang                   → daftar disposisi ke bidang
//   GET/POST /kdi/terima/{id}              → terima disposisi + buat subtask
//   GET      /kdi/subtasks/{id}            → hub manajemen subtask
//   GET/POST /kdi/upload-data/{id}         → upload file permintaan data
//   GET/POST /kdi/jadwal/{id}              → jadwal observasi
//   GET/POST /kdi/selesai-observasi/{id}   → konfirmasi observasi selesai
//   GET/POST /kdi/jadwal-wawancara/{id}    → jadwal wawancara
//   GET/POST /kdi/selesai-wawancara/{id}   → konfirmasi wawancara selesai
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

    // ── Kondisi status yang relevan bagi KDI (inline agar EF Core bisa translate ke SQL)
    private static readonly int[] KdiStatuses =
    {
        StatusId.Didisposisi, StatusId.DiProses,
        StatusId.ObservasiDijadwalkan, StatusId.ObservasiSelesai,
        StatusId.WawancaraDijadwalkan, StatusId.WawancaraSelesai,
        StatusId.DataSiap
    };

    // ════════════════════════════════════════════════════════════════════════
    // DASHBOARD
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? status)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p =>
                (p.StatusPPIDID >= StatusId.Didisposisi &&
                 p.StatusPPIDID <= StatusId.ObservasiSelesai) ||
                p.StatusPPIDID == StatusId.DiProses               ||
                p.StatusPPIDID == StatusId.WawancaraDijadwalkan   ||
                p.StatusPPIDID == StatusId.WawancaraSelesai       ||
                p.StatusPPIDID == StatusId.DataSiap)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        var list    = await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        var permIds = list.Select(p => p.PermohonanPPIDID).ToList();

        ViewData["Q"]      = q;
        ViewData["Status"] = status;
        ViewData["SubTasks"] = (await db.SubTaskPPID
                .Where(t => permIds.Contains(t.PermohonanPPIDID))
                .ToListAsync())
            .GroupBy(t => t.PermohonanPPIDID)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(list);
    }

    // ── PSMDI ─────────────────────────────────────────────────────────────

    [HttpGet("psmdi")]
    public async Task<IActionResult> Psmdi(string? q)
    {
        var query = db.PermohonanPPID.Include(p => p.Pribadi).Include(p => p.Status)
            .Where(p =>
                ((p.StatusPPIDID >= StatusId.Didisposisi &&
                  p.StatusPPIDID <= StatusId.ObservasiSelesai) ||
                  p.StatusPPIDID == StatusId.DiProses             ||
                  p.StatusPPIDID == StatusId.WawancaraDijadwalkan ||
                  p.StatusPPIDID == StatusId.WawancaraSelesai)
                && p.NamaBidang == null)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        var list    = await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        var permIds = list.Select(p => p.PermohonanPPIDID).ToList();
        ViewData["Q"] = q;
        ViewData["SubTasks"] = (await db.SubTaskPPID
                .Where(t => permIds.Contains(t.PermohonanPPIDID)).ToListAsync())
            .GroupBy(t => t.PermohonanPPIDID)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(list);
    }

    // ── Bidang ────────────────────────────────────────────────────────────

    [HttpGet("bidang")]
    public async Task<IActionResult> Bidang(string? q)
    {
        var query = db.PermohonanPPID.Include(p => p.Pribadi).Include(p => p.Status)
            .Where(p =>
                ((p.StatusPPIDID >= StatusId.Didisposisi &&
                  p.StatusPPIDID <= StatusId.ObservasiSelesai) ||
                  p.StatusPPIDID == StatusId.DiProses             ||
                  p.StatusPPIDID == StatusId.WawancaraDijadwalkan ||
                  p.StatusPPIDID == StatusId.WawancaraSelesai)
                && p.NamaBidang != null)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        var list    = await query.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        var permIds = list.Select(p => p.PermohonanPPIDID).ToList();
        ViewData["Q"] = q;
        ViewData["SubTasks"] = (await db.SubTaskPPID
                .Where(t => permIds.Contains(t.PermohonanPPIDID)).ToListAsync())
            .GroupBy(t => t.PermohonanPPIDID)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(list);
    }

    // ════════════════════════════════════════════════════════════════════════
    // TERIMA DISPOSISI — buat subtask paralel
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("terima/{id}")]
    public async Task<IActionResult> TerimaDisposisi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        return View(new TerimaDisposisiVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? "",
            LatarBelakang    = p.LatarBelakang ?? "",
            PerluObservasi   = p.IsObservasi,
            PerluWawancara   = p.IsWawancara,
        });
    }

    [HttpPost("terima"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TerimaDisposisiPost(TerimaDisposisiVm vm)
    {
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var now        = DateTime.UtcNow;
        var statusLama = p.StatusPPIDID;

        // ── Buat subtask paralel (idempotent) ─────────────────────────────
        var existingCount = await db.SubTaskPPID
            .CountAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID);

        if (existingCount == 0)
        {
            db.CreateSubTasks(
                vm.PermohonanPPIDID,
                perluData:    p.IsPermintaanData,
                perluObs:     p.IsObservasi,
                perluWaw:     p.IsWawancara,
                operatorName: CurrentUser);
        }

        p.StatusPPIDID = StatusId.DiProses;
        p.UpdatedAt    = now;

        int taskCount = (p.IsPermintaanData ? 1 : 0) +
                        (p.IsObservasi      ? 1 : 0) +
                        (p.IsWawancara      ? 1 : 0);

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.DiProses,
            $"Disposisi diterima KDI. {taskCount} sub-tugas paralel dibuat: " +
            $"{(p.IsPermintaanData ? "[Data] " : "")}" +
            $"{(p.IsObservasi ? "[Observasi] " : "")}" +
            $"{(p.IsWawancara ? "[Wawancara]" : "")}. " +
            $"Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        TempData["Success"] =
            $"Disposisi diterima. <strong>{taskCount} tugas paralel</strong> siap dikerjakan.";
        return RedirectToAction("SubTasks", new { id = vm.PermohonanPPIDID });
    }

    // ════════════════════════════════════════════════════════════════════════
    // PARALLEL TASK MANAGER HUB
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("subtasks/{id}")]
    public async Task<IActionResult> SubTasks(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var tasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        return View(new ParallelTasksVm { Permohonan = p, SubTasks = tasks });
    }

    // ════════════════════════════════════════════════════════════════════════
    // PERMINTAAN DATA — upload file
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("upload-data/{id}")]
    public async Task<IActionResult> UploadData(Guid id)
    {
        var subTask = await db.SubTaskPPID
            .Include(t => t.Permohonan).ThenInclude(p => p!.Pribadi)
            .FirstOrDefaultAsync(t =>
                t.PermohonanPPIDID == id &&
                t.JenisTask        == JenisTask.PermintaanData);

        if (subTask?.Permohonan == null)
        {
            // Fallback: permohonan tanpa subtask (data lama)
            var p = await db.PermohonanPPID.Include(x => x.Pribadi)
                .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
            if (p == null) return NotFound();

            return View(new UploadDataSubTaskVm
            {
                PermohonanPPIDID = id,
                NoPermohonan     = p.NoPermohonan!,
                NamaPemohon      = p.Pribadi?.Nama ?? "",
                JudulPenelitian  = p.JudulPenelitian ?? ""
            });
        }

        return View(new UploadDataSubTaskVm
        {
            SubTaskID        = subTask.SubTaskID,
            PermohonanPPIDID = id,
            NoPermohonan     = subTask.Permohonan.NoPermohonan ?? "",
            NamaPemohon      = subTask.Permohonan.Pribadi?.Nama ?? "",
            JudulPenelitian  = subTask.Permohonan.JudulPenelitian ?? ""
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

        // ── Update subtask ────────────────────────────────────────────────
        var subTask = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.PermintaanData);

        if (subTask != null)
        {
            subTask.StatusTask = SubTaskStatus.Selesai;
            subTask.FilePath   = fp;
            subTask.NamaFile   = nama;
            subTask.Catatan    = vm.Catatan;
            subTask.Operator   = CurrentUser;
            subTask.SelesaiAt  = now;
            subTask.UpdatedAt  = now;
        }

        // ── Audit log ─────────────────────────────────────────────────────
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null)
            db.AddAuditLog(
                vm.PermohonanPPIDID, p.StatusPPIDID,
                p.StatusPPIDID ?? StatusId.DiProses,
                $"Sub-tugas Permintaan Data selesai. File: {nama ?? "(tidak ada)"}.",
                CurrentUser);

        await db.SaveChangesAsync();

        // ── Auto-advance jika semua subtask selesai ───────────────────────
        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "✅ Data berhasil diupload. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "✅ Data berhasil diupload. Tugas lain masih dalam proses.";

        return RedirectToAction("SubTasks", new { id = vm.PermohonanPPIDID });
    }

    // ════════════════════════════════════════════════════════════════════════
    // OBSERVASI — jadwal
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("jadwal/{id}")]
    public async Task<IActionResult> JadwalObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);

        return View("JadwalSubTask", new JadwalSubTaskVm
        {
            SubTaskID         = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan!,
            NamaPemohon       = p.Pribadi?.Nama ?? "",
            JudulPenelitian   = p.JudulPenelitian ?? "",
            JenisTask         = JenisTask.Observasi,
            NamaBidangTerkait = p.NamaBidang,
            Tanggal           = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu             = sub?.WaktuJadwal   ?? new TimeOnly(9, 0),
            NamaPIC           = sub?.NamaPIC       ?? "",
            TeleponPIC        = sub?.TeleponPIC,
        });
    }

    [HttpPost("jadwal"), ValidateAntiForgeryToken]
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

        if (sub != null)
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
        if (p != null)
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
        return RedirectToAction("SubTasks", new { id = vm.PermohonanPPIDID });
    }

    // ── Selesai Observasi ─────────────────────────────────────────────────

    [HttpGet("selesai-observasi/{id}")]
    public async Task<IActionResult> SelesaiObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);

        return View("SelesaiSubTask", new SelesaiSubTaskVm
        {
            SubTaskID        = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? "",
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
        if (p == null) return NotFound();

        var lama = p.StatusPPIDID;

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Observasi);

        if (sub != null)
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
            ? "✅ Observasi selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "✅ Observasi selesai. Tugas lain masih berjalan.";

        return RedirectToAction("SubTasks", new { id = vm.PermohonanPPIDID });
    }

    // ════════════════════════════════════════════════════════════════════════
    // WAWANCARA — jadwal
    // ════════════════════════════════════════════════════════════════════════

    [HttpGet("jadwal-wawancara/{id}")]
    public async Task<IActionResult> JadwalWawancara(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var sub        = await db.GetSubTask(id, JenisTask.Wawancara);
        var detailWaw  = p.Detail.FirstOrDefault(d => d.KeperluanID == KeperluanId.Wawancara);

        return View("JadwalSubTask", new JadwalSubTaskVm
        {
            SubTaskID         = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan!,
            NamaPemohon       = p.Pribadi?.Nama ?? "",
            JudulPenelitian   = p.JudulPenelitian ?? "",
            JenisTask         = JenisTask.Wawancara,
            DetailKeperluan   = detailWaw?.DetailKeperluan,
            NamaBidangTerkait = p.NamaBidang ?? p.NamaProdusenData,
            Tanggal           = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu             = sub?.WaktuJadwal   ?? new TimeOnly(9, 0),
            NamaPIC           = sub?.NamaPIC       ?? "",
            TeleponPIC        = sub?.TeleponPIC,
        });
    }

    [HttpPost("jadwal-wawancara"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalSubTaskVm vm)
    {
        if (!ModelState.IsValid) return View("JadwalSubTask", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

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

        if (sub != null)
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
        return RedirectToAction("SubTasks", new { id = vm.PermohonanPPIDID });
    }

    // ── Selesai Wawancara ─────────────────────────────────────────────────

    [HttpGet("selesai-wawancara/{id}")]
    public async Task<IActionResult> SelesaiWawancara(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Wawancara);

        return View("SelesaiSubTask", new SelesaiSubTaskVm
        {
            SubTaskID        = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            JudulPenelitian  = p.JudulPenelitian ?? "",
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
        if (p == null) return NotFound();

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

        p.StatusPPIDID = StatusId.WawancaraSelesai;
        p.UpdatedAt    = now;

        db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.WawancaraSelesai,
            $"Wawancara selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "✅ Wawancara selesai. Semua tugas selesai — status menjadi <strong>Data Siap</strong>!"
            : "✅ Wawancara selesai. Tugas lain masih berjalan.";

        return RedirectToAction("SubTasks", new { id = vm.PermohonanPPIDID });
    }
}
