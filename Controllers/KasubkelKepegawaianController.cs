using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Kasubkel Kepegawaian — verifikasi, surat izin, dan manajemen
/// sub-tugas Observasi + Wawancara secara paralel.
///
/// Cakupan permohonan (semua loket):
///   - LoketJenis.Kepegawaian  (prefix MHS — mahasiswa)
///   - LoketJenis.Umum         (prefix UMM — LSM/organisasi/perusahaan)
///   - KategoriPemohon = "Mahasiswa" (legacy fallback)
///
/// Paralel flow:
///   Kepegawaian mengelola Obs/Waw  ──┐
///                                     ├─→ AdvanceIfAllSubTasksDone → DataSiap
///   KDI mengelola PermintaanData   ──┘
/// </summary>
[Route("kasubkel-kepegawaian")]
[Authorize(Roles = $"{AppRoles.KasubkelKepegawaian},{AppRoles.Admin}")]
public class KasubkelKepegawaianController(
    AppDbContext db,
    IWebHostEnvironment env) : LoketBaseController(db, env)
{
    private string CurrentUser =>
        User.Identity?.Name ?? AppRoles.KasubkelKepegawaian;

    // ── Predicate: cakupan permohonan yang menjadi tanggung jawab Kepegawaian ─
    private static bool IsInScope(PermohonanPPID p) =>
        p.LoketJenis == LoketJenis.Kepegawaian ||
        p.LoketJenis == LoketJenis.Umum        ||
        p.KategoriPemohon == "Mahasiswa";

    // ── Dashboard ─────────────────────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // ── Total statistik (semua loket) ─────────────────────────────────────
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Where(p => p.LoketJenis == LoketJenis.Kepegawaian
                     || p.LoketJenis == LoketJenis.Umum
                     || p.KategoriPemohon == "Mahasiswa")
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        var vm = new DashboardVm
        {
            Total        = allStatus.Count,
            Proses       = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai      = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };

        // ── FIX: Antrian verifikasi/surat izin — semua loket, bukan hanya Kepegawaian ──
        // BUG SEBELUMNYA: filter hanya mencakup LoketJenis.Kepegawaian + Mahasiswa,
        // sehingga permohonan Loket Umum (LSM/organisasi) tidak muncul di dashboard.
        ViewData["MenungguVerifikasi"] = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p =>
                // Cakupan: semua loket yang diverifikasi Kepegawaian
                (p.LoketJenis == LoketJenis.Kepegawaian ||
                 p.LoketJenis == LoketJenis.Umum        ||
                 p.KategoriPemohon == "Mahasiswa")
                &&
                // Status yang memerlukan tindakan
                (p.StatusPPIDID == StatusId.MenungguVerifikasi ||
                 p.StatusPPIDID == StatusId.IdentifikasiAwal   ||
                 p.StatusPPIDID == StatusId.MenungguSuratIzin))
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();

        // ── Antrian sub-tugas Obs/Waw aktif (semua loket) ─────────────────────
        var obsWawAktif = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p =>
                p.StatusPPIDID == StatusId.DiProses             ||
                p.StatusPPIDID == StatusId.Didisposisi          ||
                p.StatusPPIDID == StatusId.ObservasiDijadwalkan ||
                p.StatusPPIDID == StatusId.ObservasiSelesai     ||
                p.StatusPPIDID == StatusId.WawancaraDijadwalkan ||
                p.StatusPPIDID == StatusId.WawancaraSelesai     ||
                p.StatusPPIDID == StatusId.DataSiap             ||
                p.StatusPPIDID == StatusId.FeedbackPemohon)
            .Where(p => p.IsObservasi || p.IsWawancara)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();

        var obsWawIds   = obsWawAktif.Select(p => p.PermohonanPPIDID).ToList();
        var obsWawTasks = await db.SubTaskPPID
            .Where(t => obsWawIds.Contains(t.PermohonanPPIDID)
                     && (t.JenisTask == JenisTask.Observasi
                      || t.JenisTask == JenisTask.Wawancara))
            .ToListAsync();

        ViewData["ObsWawAktif"] = obsWawAktif;
        ViewData["ObsWawTaskMap"] = obsWawTasks
            .GroupBy(t => t.PermohonanPPIDID)
            .ToDictionary(g => g.Key, g => g.ToList());

        return View(vm);
    }

    // ── Daftar permohonan ─────────────────────────────────────────────────────
    [HttpGet("permohonan")]
    public async Task<IActionResult> Permohonan(string? q, int? status)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Kepegawaian
                     || p.LoketJenis == LoketJenis.Umum
                     || p.KategoriPemohon == "Mahasiswa")
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

    // ── Detail ────────────────────────────────────────────────────────────────
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

    // ── Verifikasi identifikasi awal ──────────────────────────────────────────
    [HttpGet("verifikasi/{id:guid}")]
    public async Task<IActionResult> Verifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (!IsVerifikasiAllowed(p.StatusPPIDID))
        {
            TempData["Error"] = "Permohonan ini tidak dalam status yang dapat diverifikasi.";
            return RedirectToAction(nameof(Index));
        }

        return View(BuildVerifikasiVm(p));
    }

    [HttpPost("verifikasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifikasiPost(VerifikasiVm vm)
    {
        if (!ModelState.IsValid) return View("Verifikasi", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var statusLama = p.StatusPPIDID;
        var now        = DateTime.UtcNow;

        if (vm.Disetujui)
        {
            p.StatusPPIDID = StatusId.MenungguSuratIzin;
            p.UpdatedAt    = now;

            if (vm.DisposisiUnit == "BidangTerkait" &&
                !string.IsNullOrEmpty(vm.NamaBidangDisposisi))
                p.NamaBidang = vm.NamaBidangDisposisi;

            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguSuratIzin,
                $"Verifikasi disetujui. Disposisi: {vm.DisposisiUnit}. Catatan: {vm.CatatanVerifikasi}",
                CurrentUser);

            TempData["Success"] =
                $"Permohonan <strong>{vm.NoPermohonan}</strong> diverifikasi — siap surat izin.";
        }
        else
        {
            p.StatusPPIDID = StatusId.IdentifikasiAwal;
            p.UpdatedAt    = now;

            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.IdentifikasiAwal,
                $"Verifikasi DITOLAK. Alasan: {vm.AlasanDitolak}", CurrentUser);

            TempData["Error"] =
                $"Permohonan <strong>{vm.NoPermohonan}</strong> dikembalikan. Alasan: {vm.AlasanDitolak}";
        }

        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // ── Surat Izin ────────────────────────────────────────────────────────────
    [HttpGet("surat-izin/{id:guid}")]
    public async Task<IActionResult> SuratIzin(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (p.StatusPPIDID != StatusId.MenungguSuratIzin)
        {
            TempData["Error"] = "Surat izin hanya dapat diterbitkan pada status Menunggu Surat Izin.";
            return RedirectToAction(nameof(Index));
        }

        return View(new SuratIzinVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            Kategori         = p.KategoriPemohon ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
            IsObservasi      = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara      = p.IsWawancara,
            NamaBidangList   = [string.Empty],
            DisposisiUnits   = ["PSMDI"],
        });
    }

    [HttpPost("surat-izin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuratIzinPost(SuratIzinVm vm)
    {
        if (vm.FileSuratIzin == null || vm.FileSuratIzin.Length == 0)
        {
            ModelState.AddModelError(nameof(vm.FileSuratIzin),
                "File surat izin wajib diupload dalam format PDF.");
        }

        if (!ModelState.IsValid) return View("SuratIzin", vm);

        var now = DateTime.UtcNow;

        var error = await UploadDokumen(
            vm.PermohonanPPIDID, vm.FileSuratIzin,
            JenisDokumenId.SuratIzin, "Surat Izin", now);

        if (error is not null)
        {
            ModelState.AddModelError(nameof(vm.FileSuratIzin), error);
            return View("SuratIzin", vm);
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var statusAwal = p.StatusPPIDID;

        p.NoSuratPermohonan = vm.NoSuratIzin;
        p.IsObservasi        = vm.IsObservasi;
        p.IsPermintaanData   = vm.IsPermintaanData;
        p.IsWawancara        = vm.IsWawancara;
        p.StatusPPIDID       = StatusId.SuratIzinTerbit;
        p.UpdatedAt          = now;

        db.AddAuditLog(vm.PermohonanPPIDID, statusAwal, StatusId.SuratIzinTerbit,
            $"Surat izin diterbitkan: {vm.NoSuratIzin}.",
            CurrentUser);

        // ── Buat sub-tugas Obs/Waw secara paralel jika diperlukan ─────────────
        // Kepegawaian hanya membuat sub-tugas untuk Obs/Waw.
        // PermintaanData dibuat oleh KDI saat menerima disposisi.
        if (vm.IsObservasi || vm.IsWawancara)
        {
            var existingObsWaw = await db.SubTaskPPID
                .CountAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID
                              && (t.JenisTask == JenisTask.Observasi
                               || t.JenisTask == JenisTask.Wawancara));

            if (existingObsWaw == 0)
                db.CreateSubTasks(
                    vm.PermohonanPPIDID,
                    perluData: false,
                    perluObs:  vm.IsObservasi,
                    perluWaw:  vm.IsWawancara,
                    operatorName: CurrentUser);
        }

        // ── Routing status berdasarkan keperluan ──────────────────────────────
        int    statusTujuan;
        string routeKet;
        string successMsg;

        if (vm.IsPermintaanData)
        {
            // Ada komponen data → disposisi ke KDI (paralel dengan Obs/Waw jika ada)
            p.StatusPPIDID = StatusId.Didisposisi;
            p.NamaBidang   = vm.NamaBidangPrimary;
            statusTujuan   = StatusId.Didisposisi;
            routeKet = $"Permintaan Data → disposisi KDI (bidang: {p.NamaBidang ?? "PSMDI"})." +
                       (vm.IsObservasi || vm.IsWawancara
                           ? " Obs/Waw → dikelola Kepegawaian (paralel)."
                           : "");
            successMsg = $"Surat izin <strong>{vm.NoSuratIzin}</strong> terbit. " +
                         $"Permintaan Data diteruskan KDI ({p.NamaBidang ?? "PSMDI"})." +
                         (vm.IsObservasi || vm.IsWawancara
                             ? " Obs/Waw dikelola Kepegawaian secara paralel."
                             : "");
        }
        else
        {
            // Hanya Obs/Waw — Kepegawaian mengelola langsung, tidak perlu disposisi KDI
            p.StatusPPIDID = StatusId.DiProses;
            statusTujuan   = StatusId.DiProses;
            routeKet       = "Obs/Waw-only → dikelola Kepegawaian, tidak ada disposisi KDI.";
            successMsg     = $"Surat izin <strong>{vm.NoSuratIzin}</strong> terbit. " +
                             "Obs/Waw dikelola langsung oleh Kepegawaian.";
        }

        db.AddAuditLog(vm.PermohonanPPIDID, StatusId.SuratIzinTerbit, statusTujuan,
            routeKet, CurrentUser);

        await db.SaveChangesAsync();

        TempData["Success"] = successMsg;
        return RedirectToAction(nameof(Index));
    }

    // ════════════════════════════════════════════════════════════════════════════
    // SUB-TUGAS OBSERVASI + WAWANCARA
    // ════════════════════════════════════════════════════════════════════════════

    [HttpGet("subtasks/{id:guid}")]
    public async Task<IActionResult> SubTasks(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        // Kepegawaian hanya menampilkan dan mengelola Obs/Waw
        var tasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id &&
                        (t.JenisTask == JenisTask.Observasi || t.JenisTask == JenisTask.Wawancara))
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        // Dokumen laporan final pemohon (untuk ditampilkan di sisi Kepegawaian)
        var tugasDocs = await db.DokumenPPID
            .Where(d => d.PermohonanPPIDID == id
                     && d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        ViewData["TugasDocs"] = tugasDocs;

        return View(new ParallelTasksVm { Permohonan = p, SubTasks = tasks });
    }

    // ── Jadwal Observasi ──────────────────────────────────────────────────────
    [HttpGet("jadwal-observasi/{id:guid}")]
    public async Task<IActionResult> JadwalObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", new JadwalSubTaskVm
        {
            SubTaskID         = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan    ?? string.Empty,
            NamaPemohon       = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian   = p.JudulPenelitian ?? string.Empty,
            JenisTask         = JenisTask.Observasi,
            NamaBidangTerkait = p.NamaBidang,
            Tanggal           = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu             = sub?.WaktuJadwal   ?? new TimeOnly(9, 0),
            NamaPIC           = sub?.NamaPIC       ?? string.Empty,
            TeleponPIC        = sub?.TeleponPIC,
            LokasiJenis       = sub?.LokasiJenis   ?? "Offline",
            LokasiDetail      = sub?.LokasiDetail,
        });
    }

    [HttpPost("jadwal-observasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalObservasiPost(JadwalSubTaskVm vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", vm);

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
            sub.LokasiJenis   = vm.LokasiJenis;
            sub.LokasiDetail  = vm.LokasiDetail;
            sub.Operator      = CurrentUser;
            sub.UpdatedAt     = now;
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is not null)
        {
            var lama = p.StatusPPIDID;
            // Status hanya diubah ke ObservasiDijadwalkan jika ini observasi-only
            bool isObsOnly = p.IsObservasi && !p.IsPermintaanData && !p.IsWawancara;
            if (isObsOnly && p.StatusPPIDID == StatusId.DiProses)
                p.StatusPPIDID = StatusId.ObservasiDijadwalkan;

            p.UpdatedAt = now;
            db.AddAuditLog(vm.PermohonanPPIDID, lama, p.StatusPPIDID!.Value,
                $"Observasi dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, " +
                $"PIC: {vm.NamaPIC}" + (isObsOnly ? "" : " [paralel mode — KDI memproses data secara bersamaan]"),
                CurrentUser);
        }

        await db.SaveChangesAsync();
        TempData["Success"] =
            $"Jadwal observasi: <strong>{vm.Tanggal:dd MMM yyyy}</strong> pukul {vm.Waktu:HH:mm}";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Selesai Observasi ─────────────────────────────────────────────────────
    [HttpGet("selesai-observasi/{id:guid}")]
    public async Task<IActionResult> SelesaiObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/SelesaiSubTask.cshtml", new SelesaiSubTaskVm
        {
            SubTaskID        = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
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

        var lama     = p.StatusPPIDID;
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

        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, lama, lama ?? StatusId.DiProses,
            $"Sub-tugas Observasi selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        // Cek apakah semua sub-tugas (termasuk PermintaanData di KDI) sudah selesai
        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Observasi selesai. Semua tugas paralel selesai — status menjadi <strong>Data Siap</strong>!"
            : "Observasi selesai. Tugas paralel lain (KDI/Wawancara) masih berjalan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Jadwal Wawancara ──────────────────────────────────────────────────────
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
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", new JadwalSubTaskVm
        {
            SubTaskID         = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID  = id,
            NoPermohonan      = p.NoPermohonan    ?? string.Empty,
            NamaPemohon       = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian   = p.JudulPenelitian ?? string.Empty,
            JenisTask         = JenisTask.Wawancara,
            DetailKeperluan   = detailWaw?.DetailKeperluan,
            NamaBidangTerkait = p.NamaBidang ?? p.NamaProdusenData,
            Tanggal           = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu             = sub?.WaktuJadwal   ?? new TimeOnly(9, 0),
            NamaPIC           = sub?.NamaPIC       ?? string.Empty,
            TeleponPIC        = sub?.TeleponPIC,
            LokasiJenis       = sub?.LokasiJenis   ?? "Offline",
            LokasiDetail      = sub?.LokasiDetail,
        });
    }

    [HttpPost("jadwal-wawancara"), ValidateAntiForgeryToken]
    public async Task<IActionResult> JadwalWawancaraPost(JadwalSubTaskVm vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", vm);

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

        // Status hanya diubah ke WawancaraDijadwalkan jika wawancara-only
        bool isWawOnly = p.IsWawancara && !p.IsPermintaanData && !p.IsObservasi;
        if (isWawOnly && p.StatusPPIDID == StatusId.DiProses)
            p.StatusPPIDID = StatusId.WawancaraDijadwalkan;

        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, lama, p.StatusPPIDID!.Value,
            $"Wawancara dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, " +
            $"PIC: {vm.NamaPIC}" + (isWawOnly ? "" : " [paralel mode — KDI memproses data secara bersamaan]"),
            CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] =
            $"Jadwal wawancara: <strong>{vm.Tanggal:dd MMM yyyy}</strong> pukul {vm.Waktu:HH:mm}";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Selesai Wawancara ─────────────────────────────────────────────────────
    [HttpGet("selesai-wawancara/{id:guid}")]
    public async Task<IActionResult> SelesaiWawancara(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Wawancara);
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/SelesaiSubTask.cshtml", new SelesaiSubTaskVm
        {
            SubTaskID        = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
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

        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, lama, lama ?? StatusId.DiProses,
            $"Sub-tugas Wawancara selesai. File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Wawancara selesai. Semua tugas paralel selesai — status menjadi <strong>Data Siap</strong>!"
            : "Wawancara selesai. Tugas paralel lain (KDI/Observasi) masih berjalan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Reschedule ────────────────────────────────────────────────────────────
    [HttpGet("reschedule/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> Reschedule(Guid id, string jenisTask)
    {
        if (jenisTask == JenisTask.PermintaanData)
            return BadRequest("Permintaan Data tidak dapat dijadwalkan ulang melalui Kepegawaian.");

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
            TempData["Error"] = $"Sub-tugas {JenisTask.GetLabel(jenisTask)} sudah selesai/dibatalkan.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }
        if (sub.IsPending)
        {
            TempData["Error"] = "Buat jadwal terlebih dahulu sebelum melakukan reschedule.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/RescheduleSubTask.cshtml", new RescheduleSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan    ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
            JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
            JenisTask        = jenisTask,
            TanggalLama      = sub.TanggalJadwal,
            WaktuLama        = sub.WaktuJadwal,
            NamaPICLama      = sub.NamaPIC,
            RescheduleCount  = sub.RescheduleCount,
            TanggalBaru      = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            WaktuBaru        = sub.WaktuJadwal ?? new TimeOnly(9, 0),
            NamaPICBaru      = sub.NamaPIC     ?? string.Empty,
            TeleponPICBaru   = sub.TeleponPIC,
            LokasiJenisLama  = sub.LokasiJenis,
            LokasiDetailLama = sub.LokasiDetail,
        });
    }

    [HttpPost("reschedule"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReschedulePost(RescheduleSubTaskVm vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/KasubkelKdi/RescheduleSubTask.cshtml", vm);

        if (vm.TanggalBaru < DateOnly.FromDateTime(DateTime.Today))
        {
            ModelState.AddModelError(nameof(vm.TanggalBaru), "Tanggal baru tidak boleh di masa lalu.");
            return View("~/Views/KasubkelKdi/RescheduleSubTask.cshtml", vm);
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
            ? $" (reschedule ke-{vm.RescheduleCount + 1})" : "";

        TempData["Success"] =
            $"Jadwal {JenisTask.GetLabel(vm.JenisTask)} diperbarui{rescheduleKe}: " +
            $"<strong>{vm.TanggalBaru:dd MMM yyyy}</strong> pukul {vm.WaktuBaru:HH:mm}.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Batalkan SubTask ──────────────────────────────────────────────────────
    [HttpGet("batal-subtask/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> BatalSubTask(Guid id, string jenisTask)
    {
        if (jenisTask == JenisTask.PermintaanData)
            return BadRequest("Permintaan Data dikelola oleh KDI.");

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

        var allTasks    = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id
                     && (t.JenisTask == JenisTask.Observasi || t.JenisTask == JenisTask.Wawancara))
            .ToListAsync();
        var activeTasks = allTasks.Where(t => !t.IsDibatalkan && t.SubTaskID != sub.SubTaskID).ToList();
        bool allOtherDone = activeTasks.Count == 0 || activeTasks.All(t => t.IsSelesai);

        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/BatalSubTask.cshtml", new BatalSubTaskVm
        {
            PermohonanPPIDID   = id,
            NoPermohonan       = p.NoPermohonan  ?? string.Empty,
            NamaPemohon        = p.Pribadi?.Nama ?? string.Empty,
            JenisTask          = jenisTask,
            StatusSaatIni      = SubTaskStatus.GetLabel(sub.StatusTask),
            AkanAdvanceStatus  = allOtherDone && !sub.IsSelesai,
            AkanRollbackStatus = sub.IsSelesai && (
                p.StatusPPIDID == StatusId.DataSiap ||
                p.StatusPPIDID == StatusId.FeedbackPemohon ||
                p.StatusPPIDID == StatusId.Selesai),
        });
    }

    [HttpPost("batal-subtask"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BatalSubTaskPost(BatalSubTaskVm vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/KasubkelKdi/BatalSubTask.cshtml", vm);

        var subBefore = await db.GetSubTask(vm.PermohonanPPIDID, vm.JenisTask);
        bool wasSelesai = subBefore?.IsSelesai ?? false;

        var pBefore = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        bool statusWasTerminal = pBefore is not null && (
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

        bool advanced   = false;
        bool rolledBack = false;

        if (wasSelesai && statusWasTerminal)
        {
            var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
            if (p is not null)
            {
                var lama       = p.StatusPPIDID;
                p.StatusPPIDID = StatusId.DiProses;
                p.UpdatedAt    = DateTime.UtcNow;
                db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.DiProses,
                    $"Status di-rollback ke DiProses karena {JenisTask.GetLabel(vm.JenisTask)} " +
                    $"dibatalkan. Alasan: {vm.AlasanBatal}", CurrentUser);
                await db.SaveChangesAsync();
                rolledBack = true;
            }
        }
        else
        {
            advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
            await db.SaveChangesAsync();
        }

        string msg = rolledBack
            ? $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} dibatalkan. Status dimundurkan ke <strong>Sedang Diproses</strong>."
            : advanced
                ? $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} dibatalkan. Task lain selesai — status menjadi <strong>Data Siap</strong>."
                : $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} berhasil dibatalkan.";

        TempData["Success"] = msg;
        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Reopen SubTask ────────────────────────────────────────────────────────
    [HttpGet("reopen-subtask/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> ReopenSubTask(Guid id, string jenisTask)
    {
        if (jenisTask == JenisTask.PermintaanData)
            return BadRequest("Permintaan Data dikelola oleh KDI.");

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
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/ReopenSubTask.cshtml", new ReopenSubTaskVm
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

        if (!ModelState.IsValid)
            return View("~/Views/KasubkelKdi/ReopenSubTask.cshtml", vm);

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
                    $"Status di-rollback ke DiProses karena {JenisTask.GetLabel(vm.JenisTask)} " +
                    $"di-reopen. Alasan: {vm.AlasanReopen}", CurrentUser);
            }
        }

        await db.SaveChangesAsync();

        string msg = $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} berhasil di-reopen.";
        if (needsRollback) msg += " Status dimundurkan ke <strong>Sedang Diproses</strong>.";

        TempData["Success"] = msg;
        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Update PIC ────────────────────────────────────────────────────────────
    [HttpGet("update-pic/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> UpdatePIC(Guid id, string jenisTask)
    {
        if (jenisTask == JenisTask.PermintaanData)
            return BadRequest("Permintaan Data dikelola oleh KDI.");

        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, jenisTask);
        if (sub is null || sub.IsTerminal)
        {
            TempData["Error"] = "Sub-tugas tidak ditemukan atau sudah selesai.";
            return RedirectToAction(nameof(SubTasks), new { id });
        }
        ViewData["Prefix"] = "kasubkel-kepegawaian";
        return View("~/Views/KasubkelKdi/UpdatePIC.cshtml", new UpdatePICVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan  ?? string.Empty,
            NamaPemohon      = p.Pribadi?.Nama ?? string.Empty,
            JenisTask        = jenisTask,
            NamaPICSaatIni   = sub.NamaPIC,
            NamaPICBaru      = sub.NamaPIC     ?? string.Empty,
            TeleponPICBaru   = sub.TeleponPIC,
        });
    }

    [HttpPost("update-pic"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePICPost(UpdatePICVm vm)
    {
        if (!ModelState.IsValid)
            return View("~/Views/KasubkelKdi/UpdatePIC.cshtml", vm);

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

    // ── Hasil Feedback ────────────────────────────────────────────────────────
    [HttpGet("feedback/{id:guid}")]
    public async Task<IActionResult> HasilFeedback(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        var feedbacks = await db.FeedbackTaskPPID
            .Where(f => f.PermohonanPPIDID == id)
            .ToListAsync();

        var subTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        var tugasDocs = await db.DokumenPPID
            .Where(d => d.PermohonanPPIDID == id
                     && d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        ViewData["TugasDocs"] = tugasDocs;

        return View(new HasilFeedbackVm
        {
            Permohonan = p,
            Feedbacks  = feedbacks,
            SubTasks   = subTasks,
        });
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private static bool IsVerifikasiAllowed(int? statusId) =>
        statusId is StatusId.MenungguVerifikasi
                 or StatusId.IdentifikasiAwal
                 or StatusId.MenungguSuratIzin;

    private static VerifikasiVm BuildVerifikasiVm(PermohonanPPID p) => new()
    {
        PermohonanPPIDID = p.PermohonanPPIDID,
        NoPermohonan     = p.NoPermohonan    ?? string.Empty,
        NamaPemohon      = p.Pribadi?.Nama   ?? string.Empty,
        Kategori         = p.KategoriPemohon ?? string.Empty,
        JudulPenelitian  = p.JudulPenelitian ?? string.Empty,
        LatarBelakang    = p.LatarBelakang   ?? string.Empty,
        IsObservasi      = p.IsObservasi,
        IsPermintaanData = p.IsPermintaanData,
        IsWawancara      = p.IsWawancara,
        NamaBidang       = p.NamaBidang      ?? string.Empty,
    };
}
