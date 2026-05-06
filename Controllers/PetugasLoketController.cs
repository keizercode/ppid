using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Loket Kepegawaian — mengelola pendaftaran pemohon DAN
/// sub-tugas Observasi + Wawancara secara paralel.
///
/// Obs/Waw dipindah dari KasubkelKepegawaian ke sini karena
/// petugas loket adalah ujung tombak penjadwalan lapangan.
///
/// Parallel flow:
///   Loket mengelola Obs/Waw       ──┐
///                                    ├─→ AdvanceIfAllSubTasksDone → DataSiap
///   KDI mengelola PermintaanData  ──┘
/// </summary>
[Route("petugas-loket")]
[Authorize(Roles = $"{AppRoles.Loket},{AppRoles.Admin}")]
public class PetugasLoketController(AppDbContext db, IWebHostEnvironment env)
    : LoketBaseController(db, env)
{
    private string CurrentUser => User.Identity?.Name ?? AppRoles.Loket;

    // Override OnActionExecuting — agar bell icon muncul di semua halaman Loket
    public override void OnActionExecuting(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        ViewData["NotifApiUrl"]  = "/petugas-loket/notifikasi-json";
        ViewData["NotifPageUrl"] = "/petugas-loket/notifikasi";
    }

        // ══════════════════════════════════════════════════════════════════════
        // NOTIFIKASI
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Halaman penuh rincian notifikasi.
        /// Notifikasi dirender client-side dari data JSON yang sama dengan endpoint
        /// di bawah — agar tidak ada duplikasi query DB.
        /// </summary>
        [HttpGet("notifikasi")]
        public IActionResult Notifikasi()
        {
            ViewData["Title"] = "Semua Notifikasi";
            return View("~/Views/Shared/Notifikasi.cshtml");
        }


/// <summary>
/// JSON endpoint — dipanggil oleh bell popup DAN halaman notifikasi.
/// </summary>
[HttpGet("notifikasi-json")]
public async Task<IActionResult> NotifikasiJson()
{
    var today   = DateOnly.FromDateTime(DateTime.Today);
    var in3Days = today.AddDays(3);

    // ── 1. Permohonan yang lewat batas waktu ─────────────────────────────
    var overdueList = await db.PermohonanPPID
        .Include(p => p.Pribadi)
        .Where(p =>
            p.BatasWaktu.HasValue &&
            p.BatasWaktu < today &&
            p.StatusPPIDID < StatusId.Selesai &&
            p.StatusPPIDID != StatusId.Dibatalkan)
        .OrderBy(p => p.BatasWaktu)
        .Take(30)
        .ToListAsync();

    // ── 2. Jadwal obs/waw dalam 3 hari ke depan ───────────────────────────
    var jadwalList = await db.PermohonanPPID
        .Include(p => p.Pribadi)
        .Include(p => p.Jadwal)
        .Where(p => p.Jadwal.Any(j =>
            (j.JenisJadwal == "Wawancara" || j.JenisJadwal == "Observasi") &&
            j.IsAktif &&
            j.Tanggal >= today &&
            j.Tanggal <= in3Days))
        .Take(30)
        .ToListAsync();

    // ── Value tuple bertipe kuat — menggantikan List<object> + .Cast<dynamic>()
    // yang menyebabkan InvalidOperationException karena Comparer<dynamic>.Default
    // tidak dapat membandingkan nilai secara reliable di runtime. ──────────────
    var sortable = new List<(int Priority, string DateKey, object Payload)>();

    foreach (var p in overdueList)
    {
        var hariLewat = (today.ToDateTime(TimeOnly.MinValue)
                       - p.BatasWaktu!.Value.ToDateTime(TimeOnly.MinValue)).Days;

        sortable.Add((
            Priority: 0,
            DateKey:  p.BatasWaktu?.ToString("yyyy-MM-dd") ?? "0000-00-00",
            Payload: new
            {
                id        = $"overdue_{p.PermohonanPPIDID}",
                type      = "overdue",
                icon      = "⏰",
                title     = "Batas Waktu Terlewat",
                message   = $"{p.Pribadi?.Nama ?? "—"} — {p.NoPermohonan}",
                detail    = $"Lewat {hariLewat} hari (batas: {p.BatasWaktu?.ToString("dd MMM yyyy")})",
                href      = $"/petugas-loket/detail/{p.PermohonanPPIDID}",
                dateIso   = p.BatasWaktu?.ToString("yyyy-MM-dd"),
                dateLabel = p.BatasWaktu?.ToString("dd MMM yyyy"),
                severity  = "danger",
                createdAt = p.BatasWaktu?.ToString("yyyy-MM-dd")
            }
        ));
    }

    foreach (var p in jadwalList)
    {
        var jadwals = p.Jadwal
            .Where(j =>
                (j.JenisJadwal == "Wawancara" || j.JenisJadwal == "Observasi") &&
                j.IsAktif &&
                j.Tanggal >= today &&
                j.Tanggal <= in3Days)
            .OrderBy(j => j.Tanggal);

        foreach (var j in jadwals)
        {
            var hariLagi = (j.Tanggal!.Value.ToDateTime(TimeOnly.MinValue)
                          - DateTime.Today).Days;
            var isWaw    = j.JenisJadwal == "Wawancara";
            var waktuStr = j.Waktu.HasValue ? j.Waktu.Value.ToString("HH:mm") : "—";
            var severity = hariLagi == 0 ? "danger" : "warning";

            sortable.Add((
                Priority: hariLagi == 0 ? 0 : 1,
                DateKey:  j.Tanggal?.ToString("yyyy-MM-dd") ?? "9999-99-99",
                Payload: new
                {
                    id        = $"jadwal_{p.PermohonanPPIDID}_{j.JenisJadwal}",
                    type      = isWaw ? "jadwal_wawancara" : "jadwal_observasi",
                    icon      = isWaw ? "🎤" : "🔍",
                    title     = $"{j.JenisJadwal} {(hariLagi == 0 ? "Hari Ini" : hariLagi == 1 ? "Besok" : $"dalam {hariLagi} hari")}",
                    message   = $"{p.Pribadi?.Nama ?? "—"} — {p.NoPermohonan}",
                    detail    = $"{j.Tanggal?.ToString("dd MMM yyyy")} pukul {waktuStr}" +
                                (j.NamaPIC != null ? $" · PIC: {j.NamaPIC}" : ""),
                    href      = $"/petugas-loket/subtasks/{p.PermohonanPPIDID}",
                    dateIso   = j.Tanggal?.ToString("yyyy-MM-dd"),
                    dateLabel = j.Tanggal?.ToString("dd MMM yyyy"),
                    severity,
                    createdAt = j.CreatedAt?.ToString("yyyy-MM-dd")
                }
            ));
        }
    }

    // Sorting dengan tipe yang eksplisit — tidak ada dynamic, tidak ada runtime crash
    var ordered = sortable
        .OrderBy(x => x.Priority)
        .ThenBy(x => x.DateKey)
        .Select(x => x.Payload)
        .ToList();

    return Json(ordered);
}


    // ══════════════════════════════════════════════════════════════════════
    // DASHBOARD
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? status)
    {
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        ViewData["DashVm"] = new DashboardVm
        {
            Total = allStatus.Count,
            Proses = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };

        // ── Hanya hitung antrian untuk badge navigasi, tidak load full list ──
        var obsWawSummary = await db.PermohonanPPID
            .AsNoTracking()
            .Where(p =>
                (p.IsObservasi || p.IsWawancara) &&
                (p.StatusPPIDID == StatusId.DiProses ||
                 p.StatusPPIDID == StatusId.Didisposisi ||
                 p.StatusPPIDID == StatusId.ObservasiDijadwalkan ||
                 p.StatusPPIDID == StatusId.WawancaraDijadwalkan))
            .Select(p => new { p.IsObservasi, p.IsWawancara })
            .ToListAsync();

        ViewData["ObsAntrian"] = obsWawSummary.Count(p => p.IsObservasi);
        ViewData["WawAntrian"] = obsWawSummary.Count(p => p.IsWawancara);

        // ── Daftar permohonan utama ───────────────────────────────────────
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Include(p => p.Jadwal)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.NIK != null && p.Pribadi.NIK.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["Q"] = q;
        ViewData["Status"] = status;

        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── Sub-menu: Permintaan Data ─────────────────────────────────────────

    [HttpGet("permintaan-data")]
    public async Task<IActionResult> PermintaanData(string? q)
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

        ViewData["Q"] = q;
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── Sub-menu: Wawancara ───────────────────────────────────────────────

    [HttpGet("wawancara")]
    public async Task<IActionResult> Wawancara(string? q, string? filterStatus)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Include(p => p.Jadwal)
            .Where(p => p.IsWawancara)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        // Filter cepat berdasarkan status sub-tugas
        var list = await query.OrderByDescending(p => p.CratedAt).ToListAsync();
        var ids = list.Select(p => p.PermohonanPPIDID).ToList();

        var subTasks = await db.SubTaskPPID
            .Where(t => ids.Contains(t.PermohonanPPIDID) && t.JenisTask == JenisTask.Wawancara)
            .ToListAsync();

        var subTaskMap = subTasks.ToDictionary(t => t.PermohonanPPIDID, t => t);

        // Filter by subtask status jika diminta
        if (filterStatus == "pending")
            list = list.Where(p => !subTaskMap.ContainsKey(p.PermohonanPPIDID) ||
                                   subTaskMap[p.PermohonanPPIDID].IsPending).ToList();
        else if (filterStatus == "dijadwalkan")
            list = list.Where(p => subTaskMap.TryGetValue(p.PermohonanPPIDID, out var st) && st.IsInProgress).ToList();
        else if (filterStatus == "selesai")
            list = list.Where(p => subTaskMap.TryGetValue(p.PermohonanPPIDID, out var st) && st.IsSelesai).ToList();

        ViewData["Q"] = q;
        ViewData["FilterStatus"] = filterStatus;
        ViewData["SubTaskMap"] = subTaskMap;
        return View(list);
    }

    [HttpGet("observasi")]
    public async Task<IActionResult> Observasi(string? q, string? filterStatus)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Include(p => p.Jadwal)
            .Where(p => p.IsObservasi)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        var list = await query.OrderByDescending(p => p.CratedAt).ToListAsync();
        var ids = list.Select(p => p.PermohonanPPIDID).ToList();

        var subTasks = await db.SubTaskPPID
            .Where(t => ids.Contains(t.PermohonanPPIDID) && t.JenisTask == JenisTask.Observasi)
            .ToListAsync();

        var subTaskMap = subTasks.ToDictionary(t => t.PermohonanPPIDID, t => t);

        if (filterStatus == "pending")
            list = list.Where(p => !subTaskMap.ContainsKey(p.PermohonanPPIDID) ||
                                   subTaskMap[p.PermohonanPPIDID].IsPending).ToList();
        else if (filterStatus == "dijadwalkan")
            list = list.Where(p => subTaskMap.TryGetValue(p.PermohonanPPIDID, out var st) && st.IsInProgress).ToList();
        else if (filterStatus == "selesai")
            list = list.Where(p => subTaskMap.TryGetValue(p.PermohonanPPIDID, out var st) && st.IsSelesai).ToList();

        ViewData["Q"] = q;
        ViewData["FilterStatus"] = filterStatus;
        ViewData["SubTaskMap"] = subTaskMap;
        return View(list);
    }

    // ══════════════════════════════════════════════════════════════════════
    // IDENTIFIKASI
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("identifikasi")]
    public IActionResult Identifikasi() => View(new IdentifikasiPemohonVm());

    [HttpPost("identifikasi"), ValidateAntiForgeryToken]
    public IActionResult IdentifikasiPost(IdentifikasiPemohonVm model)
    {
        if (!ModelState.IsValid) return View("Identifikasi", model);

        // Loket Kepegawaian HANYA menangani Mahasiswa.
        // LSM, Organisasi, Perusahaan, dan Umum → Loket Umum (/loket-umum/daftar).
        if (model.Kategori != "Mahasiswa")
        {
            TempData["InfoUmum"] = "true";
            return View("Identifikasi", model);
        }

        return RedirectToAction("DaftarPemohon",
            new { kategori = model.Kategori, loketJenis = LoketJenis.Kepegawaian });
    }

    // ══════════════════════════════════════════════════════════════════════
    // DAFTAR PEMOHON
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("daftar")]
    public IActionResult DaftarPemohon(string kategori, string loketJenis)
        => View(new DaftarPemohonVm { Kategori = kategori, LoketJenis = loketJenis });

    [HttpPost("daftar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DaftarPemohonPost(DaftarPemohonVm vm)
    {
        Guid? bidangGuid = null;
        if (!string.IsNullOrEmpty(vm.BidangID))
        {
            if (!Guid.TryParse(vm.BidangID, out var parsed))
            {
                ModelState.AddModelError(nameof(vm.BidangID),
                    "Unit kerja yang dipilih tidak valid. Silakan pilih ulang dari daftar.");
            }
            else
            {
                bidangGuid = parsed;
            }
        }

        if (!ModelState.IsValid) return View("DaftarPemohon", vm);

        Guid lastId = Guid.Empty;
        string noPerm = string.Empty;

        var strategy = db.Database.CreateExecutionStrategy();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ppid_upload_{Guid.NewGuid()}");
        var movers = new List<(string Temp, string Final)>();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                movers.Clear();
                var (generatedNoPerm, nextSeq) = await db.GenerateNoPermohonan(vm.LoketJenis);

                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.UtcNow;

                    var pribadi = await db.Pribadi.FirstOrDefaultAsync(p => p.NIK == vm.NIK);
                    if (pribadi == null)
                    {
                        pribadi = new Pribadi
                        {
                            NIK = vm.NIK,
                            Nama = vm.Nama,
                            Email = vm.Email,
                            Telepon = vm.Telepon,
                            Alamat = vm.Alamat,
                            RT = vm.RT,
                            RW = vm.RW,
                            KelurahanID = vm.KelurahanID,
                            KecamatanID = vm.KecamatanID,
                            KabupatenID = vm.KabupatenID,
                            NamaKelurahan = vm.NamaKelurahan,
                            NamaKecamatan = vm.NamaKecamatan,
                            NamaKabupaten = vm.NamaKabupaten,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        db.Pribadi.Add(pribadi);
                    }
                    else
                    {
                        pribadi.Nama = vm.Nama;
                        pribadi.Email = vm.Email;
                        pribadi.Telepon = vm.Telepon;
                        pribadi.UpdatedAt = now;
                    }
                    await db.SaveChangesAsync();

                    var pribadiPPID = await db.PribadiPPID
                        .FirstOrDefaultAsync(p => p.PribadiID == pribadi.PribadiID);

                    if (pribadiPPID == null)
                    {
                        db.PribadiPPID.Add(new PribadiPPID
                        {
                            PribadiID = pribadi.PribadiID,
                            ProvinsiID = vm.ProvinsiID,
                            NamaProvinsi = vm.NamaProvinsi,
                            NIM = vm.NIM,
                            Lembaga = vm.Lembaga,
                            Fakultas = vm.Fakultas,
                            Jurusan = vm.Jurusan,
                            Pekerjaan = vm.Pekerjaan,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                    else
                    {
                        pribadiPPID.NIM = vm.NIM;
                        pribadiPPID.Lembaga = vm.Lembaga;
                        pribadiPPID.Fakultas = vm.Fakultas;
                        pribadiPPID.Jurusan = vm.Jurusan;
                        pribadiPPID.Pekerjaan = vm.Pekerjaan;
                        pribadiPPID.ProvinsiID = vm.ProvinsiID;
                        pribadiPPID.NamaProvinsi = vm.NamaProvinsi;
                        pribadiPPID.UpdatedAt = now;
                    }

                    var permohonan = new PermohonanPPID
                    {
                        PribadiID = pribadi.PribadiID,
                        NoPermohonan = generatedNoPerm,
                        KategoriPemohon = vm.Kategori,
                        LoketJenis = vm.LoketJenis,
                        NoSuratPermohonan = vm.NoSuratPermohonan,
                        TanggalPermohonan = vm.TanggalPermohonan,
                        BatasWaktu = AppDbContext.HitungBatasWaktu(vm.TanggalPermohonan),
                        Pengampu = vm.Pengampu,
                        TeleponPengampu = vm.TeleponPengampu,
                        JudulPenelitian = vm.JudulPenelitian,
                        LatarBelakang = vm.LatarBelakang,
                        TujuanPermohonan = vm.TujuanPermohonan,
                        IsObservasi = vm.IsObservasi,
                        IsWawancara = vm.IsWawancara,
                        IsPermintaanData = vm.IsPermintaanData,
                        BidangID = bidangGuid,
                        NamaBidang = vm.NamaBidang,
                        StatusPPIDID = StatusId.TerdaftarSistem,
                        Sequance = nextSeq,
                        CratedAt = now,
                        UpdatedAt = now
                    };
                    db.PermohonanPPID.Add(permohonan);
                    await db.SaveChangesAsync();

                    if (vm.IsObservasi)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID = KeperluanId.Observasi,
                            DetailKeperluan = vm.DetailObservasi ?? "-",
                            CreatedAt = now
                        });

                    if (vm.IsPermintaanData)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID = KeperluanId.PermintaanData,
                            DetailKeperluan = vm.DetailPermintaanData ?? "-",
                            CreatedAt = now
                        });

                    if (vm.IsWawancara)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID = KeperluanId.Wawancara,
                            DetailKeperluan = vm.DetailWawancara ?? "-",
                            CreatedAt = now
                        });

                    Directory.CreateDirectory(tempDir);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileKTP, JenisDokumenId.KTP, "KTP", now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan", now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileProposal, JenisDokumenId.Proposal, "Proposal", now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris, JenisDokumenId.AktaNotaris, "Akta Notaris", now, tempDir, movers);

                    db.AddAuditLog(permohonan.PermohonanPPIDID, null, StatusId.TerdaftarSistem,
                        $"Permohonan didaftarkan loket [{vm.LoketJenis}]. Keperluan: " +
                        $"{(vm.IsObservasi ? "Observasi " : "")}" +
                        $"{(vm.IsPermintaanData ? "Data " : "")}" +
                        $"{(vm.IsWawancara ? "Wawancara" : "")}",
                        CurrentUser);

                    await db.SaveChangesAsync();
                    await tx.CommitAsync();

                    lastId = permohonan.PermohonanPPIDID;
                    noPerm = generatedNoPerm;
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            CommitStagedFiles(movers);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }

        TempData["Success"] = $"Permohonan berhasil didaftarkan dengan nomor <strong>{noPerm}</strong>";
        return RedirectToAction("InputIdentifikasi", new { id = lastId });
    }

    // ══════════════════════════════════════════════════════════════════════
    // INPUT IDENTIFIKASI
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("input-identifikasi/{id:guid}")]
    public async Task<IActionResult> InputIdentifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(p);
    }

    [HttpPost("input-identifikasi/{id:guid}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> InputIdentifikasiPost(Guid id)
    {
        var p = await db.PermohonanPPID.FindAsync(id);
        if (p == null) return NotFound();

        if (p.StatusPPIDID != StatusId.TerdaftarSistem)
        {
            TempData["Error"] = "Identifikasi awal sudah pernah diinput sebelumnya.";
            return RedirectToAction("Detail", new { id });
        }

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.IdentifikasiAwal;
        p.UpdatedAt = DateTime.UtcNow;

        db.AddAuditLog(id, statusLama, StatusId.IdentifikasiAwal,
            "Identifikasi awal diinput oleh petugas loket.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Identifikasi awal berhasil diinput. Cetak formulir dan minta pemohon menandatangani.";
        return RedirectToAction("CetakIdentifikasi", new { id });
    }

    // ── Cetak Identifikasi ────────────────────────────────────────────────

    [HttpGet("cetak-identifikasi/{id:guid}")]
    public async Task<IActionResult> CetakIdentifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        ViewData["Prefix"] = "petugas-loket";
        return View(p);
    }

    // ── Upload TTD ────────────────────────────────────────────────────────

    [HttpGet("upload-ttd/{id:guid}")]
    public async Task<IActionResult> UploadTTD(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        if (p.StatusPPIDID != StatusId.IdentifikasiAwal)
        {
            TempData["Error"] = p.StatusPPIDID < StatusId.IdentifikasiAwal
                ? "Input identifikasi awal terlebih dahulu sebelum upload TTD."
                : $"Upload TTD tidak dapat dilakukan — permohonan sudah berada di tahap " +
                  $"<strong>{p.Status?.NamaStatusPPID ?? "lebih lanjut"}</strong>. " +
                  "Tidak ada aksi yang diperlukan dari Loket pada tahap ini.";
            return RedirectToAction("Detail", new { id });
        }

        return View(new UploadTTDVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            LoketJenis = p.LoketJenis ?? LoketJenis.Kepegawaian
        });
    }

    [HttpPost("upload-ttd"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTTDPost(UploadTTDVm vm)
    {
        if (!ModelState.IsValid) return View("UploadTTD", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        if (p.StatusPPIDID != StatusId.IdentifikasiAwal)
        {
            TempData["Error"] = "Upload TTD tidak diizinkan — permohonan sudah berada di tahap " +
                                $"lebih lanjut (status: {p.StatusPPIDID}). Tidak ada aksi yang diperlukan.";
            return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
        }

        var now = DateTime.UtcNow;
        var error = await UploadDokumen(vm.PermohonanPPIDID, vm.FileDokumenTTD,
            JenisDokumenId.IdentifikasiSigned, "Identifikasi TTD", now);

        if (error != null)
        {
            ModelState.AddModelError(nameof(vm.FileDokumenTTD), error);
            return View("UploadTTD", vm);
        }

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.MenungguVerifikasi;
        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguVerifikasi,
            "Dokumen identifikasi TTD diupload, menunggu verifikasi Kasubkel Kepegawaian.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Dokumen berhasil diupload. Permohonan diteruskan ke Kasubkel Kepegawaian untuk verifikasi.";
        return RedirectToAction("Index");
    }

    // ── Surat Izin (dikelola Loket Kepegawaian) ───────────────────────────
    // Setelah Kasubkel memverifikasi, Loket yang menerbitkan surat izin.
    // Logic routing identik dengan versi Kasubkel — aktornya saja yang berubah.

    [HttpGet("surat-izin/{id:guid}")]
    public async Task<IActionResult> SuratIzin(Guid id, bool fromCetak = false)
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

        // Peringatan jika diakses langsung tanpa melalui cetak surat pemberian izin
        if (!fromCetak)
        {
            TempData["Warning"] =
                "Pastikan <strong>Surat Pemberian Izin</strong> sudah dicetak" +
                "sebelum mengupload surat izin.";
        }

        return View(new SuratIzinVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            Kategori = p.KategoriPemohon ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            IsObservasi = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara = p.IsWawancara,
            NamaBidangList = [string.Empty],
            DisposisiUnits = ["PSMDI"],
        });
    }

    [HttpPost("surat-izin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuratIzinPost(SuratIzinVm vm)
    {
        if (vm.FileSuratIzin == null || vm.FileSuratIzin.Length == 0)
            ModelState.AddModelError(nameof(vm.FileSuratIzin),
                "File surat izin wajib diupload dalam format PDF.");

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
        p.IsObservasi = vm.IsObservasi;
        p.IsPermintaanData = vm.IsPermintaanData;
        p.IsWawancara = vm.IsWawancara;
        p.StatusPPIDID = StatusId.SuratIzinTerbit;
        p.UpdatedAt = now;

        db.AddAuditLog(vm.PermohonanPPIDID, statusAwal, StatusId.SuratIzinTerbit,
            $"Surat izin diterbitkan oleh Loket Kepegawaian: {vm.NoSuratIzin}.",
            CurrentUser);

        // ── Buat SEMUA sub-tugas sekaligus saat SuratIzin terbit ─────────
        // KRITIS: PermintaanData HARUS dibuat di sini bersamaan dengan Obs/Waw
        // agar AdvanceIfAllSubTasksDone mengetahui ada 3 tugas yang harus
        // diselesaikan. Jika PermintaanData subtask belum ada saat Obs/Waw
        // selesai, method tersebut akan salah mengira semua sudah beres.
        {
            // Cek subtask yang sudah ada (idempotent — aman jika di-repost)
            var existingJenis = await db.SubTaskPPID
                .Where(t => t.PermohonanPPIDID == vm.PermohonanPPIDID)
                .Select(t => t.JenisTask)
                .ToListAsync();

            bool needData = vm.IsPermintaanData && !existingJenis.Contains(JenisTask.PermintaanData);
            bool needObs  = vm.IsObservasi      && !existingJenis.Contains(JenisTask.Observasi);
            bool needWaw  = vm.IsWawancara      && !existingJenis.Contains(JenisTask.Wawancara);

            if (needData || needObs || needWaw)
                db.CreateSubTasks(
                    vm.PermohonanPPIDID,
                    perluData: needData,
                    perluObs:  needObs,
                    perluWaw:  needWaw,
                    operatorName: CurrentUser);
        }

        // ── Routing status berdasarkan keperluan ──────────────────────────
        int statusTujuan;
        string routeKet;
        string successMsg;

        if (vm.IsPermintaanData)
        {
            p.StatusPPIDID = StatusId.Didisposisi;
            p.NamaBidang = vm.NamaBidangPrimary;
            statusTujuan = StatusId.Didisposisi;
            routeKet = $"Surat izin terbit (Loket Kepegawaian). " +
                       $"Permintaan Data → disposisi KDI (bidang: {p.NamaBidang ?? "PSMDI"})." +
                       (vm.IsObservasi || vm.IsWawancara
                           ? " Obs/Waw → dikelola Loket Kepegawaian (paralel)."
                           : "");
            successMsg = $"Surat izin <strong>{vm.NoSuratIzin}</strong> terbit. " +
                         $"Permintaan Data diteruskan KDI ({p.NamaBidang ?? "PSMDI"})." +
                         (vm.IsObservasi || vm.IsWawancara
                             ? " Obs/Waw dikelola Loket secara paralel."
                             : "");
        }
        else
        {
            // Hanya Obs/Waw → langsung DiProses
            p.StatusPPIDID = StatusId.DiProses;
            statusTujuan = StatusId.DiProses;
            routeKet = "Surat izin terbit (Loket Kepegawaian). Obs/Waw-only → dikelola Loket.";
            successMsg = $"Surat izin <strong>{vm.NoSuratIzin}</strong> terbit. " +
                             "Obs/Waw dikelola langsung oleh Loket Kepegawaian.";
        }

        db.AddAuditLog(vm.PermohonanPPIDID, StatusId.SuratIzinTerbit, statusTujuan,
            routeKet, CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = successMsg;
        return RedirectToAction(nameof(Index));
    }

    // ══════════════════════════════════════════════════════════════════════
    // SUB-TUGAS OBSERVASI + WAWANCARA (dikelola Loket Kepegawaian)
    // Parallel dengan PermintaanData yang dikelola KDI.
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("subtasks/{id:guid}")]
public async Task<IActionResult> SubTasks(Guid id)
{
    var p = await db.PermohonanPPID
        .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
        .Include(x => x.Status)
        .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

    if (p is null) return NotFound();

    // Loket mengelola Obs/Waw; tampilkan status PermintaanData dari KDI sebagai info
    var tasks = await db.SubTaskPPID
        .Where(t => t.PermohonanPPIDID == id &&
                    (t.JenisTask == JenisTask.Observasi || t.JenisTask == JenisTask.Wawancara))
        .OrderBy(t => t.JenisTask)
        .ToListAsync();

    // Laporan final pemohon (unggahan via portal publik)
    var tugasDocs = await db.DokumenPPID
        .Where(d => d.PermohonanPPIDID == id &&
                    d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
        .OrderByDescending(d => d.CreatedAt)
        .ToListAsync();

    // Info sub-tugas KDI (PermintaanData) — read-only untuk Loket
    var kdiTask = await db.SubTaskPPID
        .Where(t => t.PermohonanPPIDID == id && t.JenisTask == JenisTask.PermintaanData)
        .ToListAsync();

    // ── TAMBAHAN: Load feedback yang sudah diterima dari pemohon ──────────
    // Digunakan oleh panel "Status Feedback Pemohon" di SubTasks.cshtml
    // agar Loket dapat melihat progress feedback tanpa harus masuk ke
    // halaman HasilFeedback terpisah.
    var feedbacks = await db.FeedbackTaskPPID
        .AsNoTracking()
        .Where(f => f.PermohonanPPIDID == id)
        .ToListAsync();

    ViewData["FeedbackMap"] = feedbacks
        .GroupBy(f => f.JenisTask)
        .ToDictionary(g => g.Key, g => true);
    // ──────────────────────────────────────────────────────────────────────

    ViewData["TugasDocs"]  = tugasDocs;
    ViewData["KdiTask"]    = kdiTask;
    ViewData["Prefix"]     = "petugas-loket";

    return View(new ParallelTasksVm { Permohonan = p, SubTasks = tasks });
}

    // ── Jadwal Observasi ──────────────────────────────────────────────────

    [HttpGet("jadwal-observasi/{id:guid}")]
    public async Task<IActionResult> JadwalObservasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p is null) return NotFound();

        var sub = await db.GetSubTask(id, JenisTask.Observasi);
        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", new JadwalSubTaskVm
        {
            SubTaskID = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            JenisTask = JenisTask.Observasi,
            NamaBidangTerkait = p.NamaBidang,
            Tanggal = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu = sub?.WaktuJadwal ?? new TimeOnly(9, 0),
            NamaPIC = sub?.NamaPIC ?? string.Empty,
            TeleponPIC = sub?.TeleponPIC,
            LokasiJenis = sub?.LokasiJenis ?? "Offline",
            LokasiDetail = sub?.LokasiDetail,
        });
    }

    [HttpPost("jadwal-observasi"), ValidateAntiForgeryToken]
public async Task<IActionResult> JadwalObservasiPost(JadwalSubTaskVm vm)
{
    if (!ModelState.IsValid)
        return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", vm);

    var now = DateTime.UtcNow;

    // ── Deaktivasi jadwal observasi lama yang masih aktif ────────────────
    // Wajib dilakukan agar jadwal stale sebelum Reopen tidak override jadwal baru.
    var jadwalLama = await db.JadwalPPID
        .Where(j => j.PermohonanPPIDID == vm.PermohonanPPIDID
                 && j.JenisJadwal == "Observasi"
                 && j.IsAktif)
        .ToListAsync();
    foreach (var jl in jadwalLama)
        jl.IsAktif = false;

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
        IsAktif          = true,   // ← FIX UTAMA
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
        var lama       = p.StatusPPIDID;
        bool isObsOnly = p.IsObservasi && !p.IsPermintaanData && !p.IsWawancara;
        if (isObsOnly && p.StatusPPIDID == StatusId.DiProses)
            p.StatusPPIDID = StatusId.ObservasiDijadwalkan;

        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, lama, p.StatusPPIDID!.Value,
            $"Observasi dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, " +
            $"PIC: {vm.NamaPIC}. Dikelola Loket Kepegawaian." +
            (isObsOnly ? "" : " [paralel mode — KDI memproses data secara bersamaan]"),
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
        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/SelesaiSubTask.cshtml", new SelesaiSubTaskVm
        {
            SubTaskID = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            JenisTask = JenisTask.Observasi,
            TanggalJadwal = sub?.TanggalJadwal,
            WaktuJadwal = sub?.WaktuJadwal,
            NamaPIC = sub?.NamaPIC,
            TeleponPIC = sub?.TeleponPIC,
        });
    }

    [HttpPost("selesai-observasi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiObservasiPost(SelesaiSubTaskVm vm)
    {
        var now = DateTime.UtcNow;
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var lama = p.StatusPPIDID;
        string? fp = null;
        string? nama = null;

        if (vm.FileHasil?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"observasi_{Path.GetFileName(vm.FileHasil.FileName)}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileHasil.CopyToAsync(s);
            fp = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
            nama = vm.FileHasil.FileName;

            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                NamaDokumenPPID = "Hasil Observasi",
                UploadDokumenPPID = fp,
                JenisDokumenPPIDID = JenisDokumenId.DataHasilObservasi,
                CreatedAt = now
            });
        }

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Observasi);

        if (sub is not null)
        {
            sub.StatusTask = SubTaskStatus.Selesai;
            sub.FilePath = fp;
            sub.NamaFile = nama;
            sub.Catatan = vm.Catatan;
            sub.Operator = CurrentUser;
            sub.SelesaiAt = now;
            sub.UpdatedAt = now;
        }

        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, lama, lama ?? StatusId.DiProses,
            $"Sub-tugas Observasi selesai (Loket). File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Observasi selesai. Semua tugas paralel selesai — status menjadi <strong>Data Siap</strong>!"
            : "Observasi selesai. Tugas paralel lain (KDI/Wawancara) masih berjalan.";

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

        var sub = await db.GetSubTask(id, JenisTask.Wawancara);
        var detailWaw = p.Detail.FirstOrDefault(d => d.KeperluanID == KeperluanId.Wawancara);
        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", new JadwalSubTaskVm
        {
            SubTaskID = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            JenisTask = JenisTask.Wawancara,
            DetailKeperluan = detailWaw?.DetailKeperluan,
            NamaBidangTerkait = p.NamaBidang ?? p.NamaProdusenData,
            Tanggal = sub?.TanggalJadwal ?? DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Waktu = sub?.WaktuJadwal ?? new TimeOnly(9, 0),
            NamaPIC = sub?.NamaPIC ?? string.Empty,
            TeleponPIC = sub?.TeleponPIC,
            LokasiJenis = sub?.LokasiJenis ?? "Offline",
            LokasiDetail = sub?.LokasiDetail,
        });
    }

    [HttpPost("jadwal-wawancara"), ValidateAntiForgeryToken]
public async Task<IActionResult> JadwalWawancaraPost(JadwalSubTaskVm vm)
{
    if (!ModelState.IsValid)
        return View("~/Views/KasubkelKdi/JadwalSubTask.cshtml", vm);

    var p   = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
    if (p is null) return NotFound();

    var now  = DateTime.UtcNow;
    var lama = p.StatusPPIDID;

    // ── Deaktivasi jadwal wawancara lama yang masih aktif ────────────────
    var jadwalLama = await db.JadwalPPID
        .Where(j => j.PermohonanPPIDID == vm.PermohonanPPIDID
                 && j.JenisJadwal == "Wawancara"
                 && j.IsAktif)
        .ToListAsync();
    foreach (var jl in jadwalLama)
        jl.IsAktif = false;

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
        IsAktif          = true,   // ← FIX UTAMA
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

    bool isWawOnly = p.IsWawancara && !p.IsPermintaanData && !p.IsObservasi;
    if (isWawOnly && p.StatusPPIDID == StatusId.DiProses)
        p.StatusPPIDID = StatusId.WawancaraDijadwalkan;

    p.UpdatedAt = now;
    db.AddAuditLog(vm.PermohonanPPIDID, lama, p.StatusPPIDID!.Value,
        $"Wawancara dijadwalkan {vm.Tanggal:dd MMM yyyy} pukul {vm.Waktu:HH:mm}, " +
        $"PIC: {vm.NamaPIC}. Dikelola Loket Kepegawaian." +
        (isWawOnly ? "" : " [paralel mode — KDI memproses data secara bersamaan]"),
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
        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/SelesaiSubTask.cshtml", new SelesaiSubTaskVm
        {
            SubTaskID = sub?.SubTaskID ?? Guid.Empty,
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            JenisTask = JenisTask.Wawancara,
            TanggalJadwal = sub?.TanggalJadwal,
            WaktuJadwal = sub?.WaktuJadwal,
            NamaPIC = sub?.NamaPIC,
            TeleponPIC = sub?.TeleponPIC,
        });
    }

    [HttpPost("selesai-wawancara"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SelesaiWawancaraPost(SelesaiSubTaskVm vm)
    {
        var now = DateTime.UtcNow;
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p is null) return NotFound();

        var lama = p.StatusPPIDID;
        string? fp = null;
        string? nama = null;

        if (vm.FileHasil?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"wawancara_{Path.GetFileName(vm.FileHasil.FileName)}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileHasil.CopyToAsync(s);
            fp = $"/uploads/{vm.PermohonanPPIDID}/{fn}";
            nama = vm.FileHasil.FileName;

            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID = vm.PermohonanPPIDID,
                NamaDokumenPPID = "Hasil Wawancara",
                UploadDokumenPPID = fp,
                JenisDokumenPPIDID = JenisDokumenId.DataHasilWawancara,
                CreatedAt = now
            });
        }

        var sub = vm.SubTaskID != Guid.Empty
            ? await db.SubTaskPPID.FindAsync(vm.SubTaskID)
            : await db.GetSubTask(vm.PermohonanPPIDID, JenisTask.Wawancara);

        if (sub is not null)
        {
            sub.StatusTask = SubTaskStatus.Selesai;
            sub.FilePath = fp;
            sub.NamaFile = nama;
            sub.Catatan = vm.Catatan;
            sub.Operator = CurrentUser;
            sub.SelesaiAt = now;
            sub.UpdatedAt = now;
        }

        p.UpdatedAt = now;
        db.AddAuditLog(vm.PermohonanPPIDID, lama, lama ?? StatusId.DiProses,
            $"Sub-tugas Wawancara selesai (Loket). File: {nama ?? "tidak ada"}. Catatan: {vm.Catatan}",
            CurrentUser);

        await db.SaveChangesAsync();

        var advanced = await db.AdvanceIfAllSubTasksDone(vm.PermohonanPPIDID, CurrentUser);
        await db.SaveChangesAsync();

        TempData["Success"] = advanced
            ? "Wawancara selesai. Semua tugas paralel selesai — status menjadi <strong>Data Siap</strong>!"
            : "Wawancara selesai. Tugas paralel lain (KDI/Observasi) masih berjalan.";

        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Reschedule ────────────────────────────────────────────────────────

    [HttpGet("reschedule/{id:guid}/{jenisTask}")]
    public async Task<IActionResult> Reschedule(Guid id, string jenisTask)
    {
        if (jenisTask == JenisTask.PermintaanData)
            return BadRequest("Permintaan Data tidak dikelola oleh Loket.");

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

        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/RescheduleSubTask.cshtml", new RescheduleSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            JenisTask = jenisTask,
            TanggalLama = sub.TanggalJadwal,
            WaktuLama = sub.WaktuJadwal,
            NamaPICLama = sub.NamaPIC,
            RescheduleCount = sub.RescheduleCount,
            TanggalBaru = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            WaktuBaru = sub.WaktuJadwal ?? new TimeOnly(9, 0),
            NamaPICBaru = sub.NamaPIC ?? string.Empty,
            TeleponPICBaru = sub.TeleponPIC,
            LokasiJenis      = sub.LokasiJenis ?? "Offline",   // ← FIX BUG
            LokasiDetail     = sub.LokasiDetail,               // ← FIX BUG
            LokasiJenisLama  = sub.LokasiJenis,
            LokasiDetailLama = sub.LokasiDetail,
        });
    }

    [HttpPost("reschedule"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReschedulePost(RescheduleSubTaskVm vm)
    {
        ViewData["Prefix"] = "petugas-loket";
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

    // ── Batalkan SubTask ──────────────────────────────────────────────────

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

        var allTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id &&
                        (t.JenisTask == JenisTask.Observasi || t.JenisTask == JenisTask.Wawancara))
            .ToListAsync();
        var activeTasks = allTasks.Where(t => !t.IsDibatalkan && t.SubTaskID != sub.SubTaskID).ToList();
        bool allOtherDone = activeTasks.Count == 0 || activeTasks.All(t => t.IsSelesai);

        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/BatalSubTask.cshtml", new BatalSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JenisTask = jenisTask,
            StatusSaatIni = SubTaskStatus.GetLabel(sub.StatusTask),
            AkanAdvanceStatus = allOtherDone && !sub.IsSelesai,
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

        bool advanced = false;
        bool rolledBack = false;

        if (wasSelesai && statusWasTerminal)
        {
            var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
            if (p is not null)
            {
                var lama = p.StatusPPIDID;
                p.StatusPPIDID = StatusId.DiProses;
                p.UpdatedAt = DateTime.UtcNow;
                db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.DiProses,
                    $"Status di-rollback ke DiProses karena {JenisTask.GetLabel(vm.JenisTask)} " +
                    $"dibatalkan (Loket). Alasan: {vm.AlasanBatal}", CurrentUser);
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

    // ── Reopen SubTask ────────────────────────────────────────────────────

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

        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/ReopenSubTask.cshtml", new ReopenSubTaskVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JenisTask = jenisTask,
            StatusSaatIni = p.StatusPPIDID ?? StatusId.TerdaftarSistem,
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
                var lama = p.StatusPPIDID;
                p.StatusPPIDID = StatusId.DiProses;
                p.UpdatedAt = DateTime.UtcNow;
                db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.DiProses,
                    $"Status di-rollback ke DiProses karena {JenisTask.GetLabel(vm.JenisTask)} " +
                    $"di-reopen (Loket). Alasan: {vm.AlasanReopen}", CurrentUser);
            }
        }

        await db.SaveChangesAsync();

        string msg = $"Sub-tugas {JenisTask.GetLabel(vm.JenisTask)} berhasil di-reopen.";
        if (needsRollback) msg += " Status dimundurkan ke <strong>Sedang Diproses</strong>.";

        TempData["Success"] = msg;
        return RedirectToAction(nameof(SubTasks), new { id = vm.PermohonanPPIDID });
    }

    // ── Update PIC ────────────────────────────────────────────────────────

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

        ViewData["Prefix"] = "petugas-loket";
        return View("~/Views/KasubkelKdi/UpdatePIC.cshtml", new UpdatePICVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            JenisTask = jenisTask,
            NamaPICSaatIni = sub.NamaPIC,
            NamaPICBaru = sub.NamaPIC ?? string.Empty,
            TeleponPICBaru = sub.TeleponPIC,
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

    // ── Hasil Feedback (view-only untuk Loket) ────────────────────────────

    [HttpGet("feedback/{id:guid}")]
public async Task<IActionResult> HasilFeedback(Guid id)
{
    var p = await db.PermohonanPPID
        .Include(x => x.Pribadi)
        .Include(x => x.Status)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

    if (p is null) return NotFound();

    // Load semua feedback task yang sudah diterima dari pemohon
    var feedbacks = await db.FeedbackTaskPPID
        .AsNoTracking()
        .Where(f => f.PermohonanPPIDID == id)
        .ToListAsync();

    // Load semua sub-task (Obs, Waw, PermintaanData) untuk menentukan
    // task mana yang wajib punya feedback
    var subTasks = await db.SubTaskPPID
        .Where(t => t.PermohonanPPIDID == id)
        .OrderBy(t => t.JenisTask)
        .ToListAsync();

    // Load laporan / tugas final yang diunggah pemohon via portal publik
    var tugasDocs = await db.DokumenPPID
        .Where(d => d.PermohonanPPIDID == id &&
                    d.JenisDokumenPPIDID == JenisDokumenId.TugasFinal)
        .OrderByDescending(d => d.CreatedAt)
        .ToListAsync();

    ViewData["TugasDocs"] = tugasDocs;

    // Gunakan view lokal PetugasLoket/HasilFeedback.cshtml
    return View(new HasilFeedbackVm
    {
        Permohonan = p,
        Feedbacks  = feedbacks,
        SubTasks   = subTasks,
    });
}

    // ── Minta Feedback (dikelola Loket Kepegawaian) ───────────────────────────
// Memindahkan status DataSiap → FeedbackPemohon agar pemohon dapat mengisi
// feedback di portal publik. Loket adalah aktor yang berinteraksi langsung
// dengan pemohon sehingga mereka yang "membuka" akses feedback.

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
    ViewData["HasData"] = p.Dokumen.Any(d =>
    d.JenisDokumenPPIDID == JenisDokumenId.DataHasil            ||
    d.JenisDokumenPPIDID == JenisDokumenId.DataHasilObservasi   ||
    d.JenisDokumenPPIDID == JenisDokumenId.DataHasilWawancara   ||
    d.JenisDokumenPPIDID == JenisDokumenId.DataHasilPermintaan);

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
        "Data siap. Loket Kepegawaian meminta pemohon mengisi feedback via portal publik.",
        CurrentUser);

    await db.SaveChangesAsync();

    TempData["Success"] =
        "Status diperbarui ke <strong>Feedback Pemohon</strong>. " +
        "Pemohon dapat mengisi feedback di portal publik menggunakan nomor permohonan mereka.";

    return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
}

[HttpPost("tandai-selesai-feedback"), ValidateAntiForgeryToken]
public async Task<IActionResult> TandaiSelesaiFeedback([FromForm] Guid permohonanId)
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
        "Permohonan ditandai selesai secara manual oleh Loket Kepegawaian (pemohon tidak merespons).",
        CurrentUser);

    await db.SaveChangesAsync();

    TempData["Success"] = "Permohonan berhasil ditandai <strong>Selesai</strong>.";
    return RedirectToAction(nameof(SubTasks), new { id = permohonanId });
}

    // ══════════════════════════════════════════════════════════════════════
    // DETAIL + EDIT + BATALKAN
    // ══════════════════════════════════════════════════════════════════════

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
        if (p == null) return NotFound();

        ViewData["SubTasks"] = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id)
            .OrderBy(t => t.JenisTask)
            .ToListAsync();

        return View(p);
    }

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        if (p.StatusPPIDID > StatusId.MenungguSuratIzin)
        {
            TempData["Error"] = "Permohonan ini tidak dapat diedit pada status ini.";
            return RedirectToAction("Detail", new { id });
        }

        return View(new EditPermohonanVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? "",
            NamaPemohon = p.Pribadi?.Nama ?? "",
            LoketJenis = p.LoketJenis ?? LoketJenis.Kepegawaian,
            JudulPenelitian = p.JudulPenelitian ?? "",
            LatarBelakang = p.LatarBelakang ?? "",
            TujuanPermohonan = p.TujuanPermohonan ?? "",
            Pengampu = p.Pengampu,
            TeleponPengampu = p.TeleponPengampu,
            BatasWaktu = p.BatasWaktu,
            IsObservasi = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara = p.IsWawancara,
        });
    }

    [HttpPost("edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPost(EditPermohonanVm vm)
    {
        if (!ModelState.IsValid) return View("Edit", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        p.JudulPenelitian = vm.JudulPenelitian;
        p.LatarBelakang = vm.LatarBelakang;
        p.TujuanPermohonan = vm.TujuanPermohonan;
        p.Pengampu = vm.Pengampu;
        p.TeleponPengampu = vm.TeleponPengampu;
        p.BatasWaktu = vm.BatasWaktu;
        p.IsObservasi = vm.IsObservasi;
        p.IsPermintaanData = vm.IsPermintaanData;
        p.IsWawancara = vm.IsWawancara;
        p.UpdatedAt = DateTime.UtcNow;

        db.AddAuditLog(vm.PermohonanPPIDID, p.StatusPPIDID,
            p.StatusPPIDID ?? StatusId.TerdaftarSistem,
            "Data permohonan diedit oleh petugas loket.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = $"Permohonan <strong>{vm.NoPermohonan}</strong> berhasil diperbarui.";
        return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
    }

    [HttpGet("batalkan/{id:guid}")]
    public async Task<IActionResult> Batalkan(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (!StatusId.IsBatalkanAllowed(p.StatusPPIDID))
        {
            TempData["Error"] =
                $"Permohonan <strong>{p.NoPermohonan}</strong> tidak dapat dibatalkan dari loket. " +
                $"Status saat ini: <strong>{p.Status?.NamaStatusPPID}</strong>. " +
                "Pembatalan hanya tersedia sebelum surat izin terbit.";
            return RedirectToAction("Detail", new { id });
        }

        var subTasks = await db.SubTaskPPID
            .Where(t => t.PermohonanPPIDID == id && t.StatusTask != SubTaskStatus.Dibatalkan)
            .ToListAsync();

        return View(new BatalkanPermohonanVm
        {
            PermohonanPPIDID = id,
            NoPermohonan = p.NoPermohonan ?? string.Empty,
            NamaPemohon = p.Pribadi?.Nama ?? string.Empty,
            Kategori = p.KategoriPemohon ?? string.Empty,
            JudulPenelitian = p.JudulPenelitian ?? string.Empty,
            StatusPPIDID = p.StatusPPIDID ?? StatusId.TerdaftarSistem,
            StatusLabel = p.Status?.NamaStatusPPID ?? "—",
            AdaSubTasks = subTasks.Count > 0,
            JumlahSubTasks = subTasks.Count,
        });
    }

    [HttpPost("batalkan"), ValidateAntiForgeryToken]
    public async Task<IActionResult> BatalkanPost(BatalkanPermohonanVm vm)
    {
        if (!ModelState.IsValid)
        {
            var pReload = await db.PermohonanPPID
                .Include(x => x.Pribadi)
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.PermohonanPPIDID == vm.PermohonanPPIDID);

            if (pReload is not null)
            {
                vm.StatusLabel = pReload.Status?.NamaStatusPPID ?? "—";
                vm.JudulPenelitian = pReload.JudulPenelitian ?? string.Empty;
                vm.Kategori = pReload.KategoriPemohon ?? string.Empty;

                var subCount = await db.SubTaskPPID
                    .CountAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID
                                  && t.StatusTask != SubTaskStatus.Dibatalkan);
                vm.AdaSubTasks = subCount > 0;
                vm.JumlahSubTasks = subCount;
            }
            return View("Batalkan", vm);
        }

        var (success, errorMsg) = await db.BatalkanPermohonan(
            vm.PermohonanPPIDID, vm.AlasanBatal, CurrentUser);

        if (!success)
        {
            TempData["Error"] = errorMsg ?? "Pembatalan gagal. Refresh dan coba kembali.";
            return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            var inner = dbEx.InnerException?.Message ?? dbEx.Message;
            TempData["Error"] =
                $"Terjadi kesalahan database saat membatalkan permohonan. " +
                $"Ref: {DateTime.UtcNow:yyyyMMddHHmmss}";
            return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
        }

        TempData["Success"] =
            $"Permohonan <strong>{vm.NoPermohonan}</strong> berhasil dibatalkan. " +
            "Dokumen dan riwayat tetap tersimpan untuk keperluan audit.";

        return RedirectToAction("Index");
    }

    // ── Menu Data ─────────────────────────────────────────────────────────

    [HttpGet("data")]
    public async Task<IActionResult> MenuData(string? q, string? export)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(p => p.Status)
            .Include(p => p.Detail).ThenInclude(d => d.Keperluan)
            .Where(p => p.LoketJenis == LoketJenis.Kepegawaian || p.KategoriPemohon == "Mahasiswa")
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        var list = await query.OrderByDescending(p => p.CratedAt).ToListAsync();

        if (export == "csv") return BuildCsvKepegawaian(list);

        ViewData["Q"] = q;
        return View("~/Views/MenuData/Index.cshtml", new MenuDataVm
        {
            List = list,
            LoketJenis = LoketJenis.Kepegawaian,
            Judul = "Data Permohonan — Loket Kepegawaian"
        });
    }

    private FileContentResult BuildCsvKepegawaian(List<PermohonanPPID> list)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("No Permohonan,Nama Pemohon,Kategori,NIK,NIM,Lembaga,Fakultas,Jurusan,Tgl Permohonan,Batas Waktu,Status,Tgl Selesai,Pengampu,Keperluan");

        foreach (var p in list)
        {
            var keperluan = string.Join("|", new[]
            {
                p.IsObservasi      ? "Observasi"       : null,
                p.IsPermintaanData ? "Permintaan Data" : null,
                p.IsWawancara      ? "Wawancara"       : null
            }.Where(x => x != null));

            sb.AppendLine(string.Join(",",
                CsvQuote(p.NoPermohonan),
                CsvQuote(p.Pribadi?.Nama),
                CsvQuote(p.KategoriPemohon),
                CsvQuote(p.Pribadi?.NIK),
                CsvQuote(p.Pribadi?.PribadiPPID?.NIM),
                CsvQuote(p.Pribadi?.PribadiPPID?.Lembaga),
                CsvQuote(p.Pribadi?.PribadiPPID?.Fakultas),
                CsvQuote(p.Pribadi?.PribadiPPID?.Jurusan),
                p.TanggalPermohonan?.ToString("dd/MM/yyyy"),
                p.BatasWaktu?.ToString("dd/MM/yyyy"),
                CsvQuote(p.Status?.NamaStatusPPID),
                p.TanggalSelesai?.ToString("dd/MM/yyyy"),
                CsvQuote(p.Pengampu),
                CsvQuote(keperluan)));
        }

        return ToCsvFile(sb.ToString(), "data_loket_kepegawaian");
    }

    // ═══════════════════════════════════════════════════════════════════════════
// PATCH: tambahkan dua action berikut ke PetugasLoketController.cs
// Letakkan setelah region "DETAIL + EDIT + BATALKAN" atau di akhir class,
// sebelum closing brace `}` terakhir.
// ═══════════════════════════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════════════════════
    // SURAT PEMBERIAN IZIN — INPUT & CETAK
    // ══════════════════════════════════════════════════════════════════════

    [HttpGet("surat-pemberian-izin/{id:guid}")]
    public async Task<IActionResult> SuratPemberianIzin(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p is null) return NotFound();

        if (p.StatusPPIDID < StatusId.MenungguSuratIzin || p.StatusPPIDID == StatusId.Dibatalkan)
        {
            TempData["Error"] =
                "Surat pemberian izin baru dapat dibuat saat permohonan berstatus " +
                "<strong>Menunggu Surat Izin</strong>. " +
                "Pastikan identifikasi awal dan verifikasi Kasubkel sudah selesai.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        // Bidang tujuan dari disposisi Kasubkel (pipe-separated)
        var bidangList = string.IsNullOrWhiteSpace(p.NamaBidang)
            ? new List<string>()
            : p.NamaBidang
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

        var jenisKegiatan = (p.IsPermintaanData && p.IsWawancara && p.IsObservasi)
            ? "Permintaan Data, Wawancara, dan Observasi"
            : (p.IsPermintaanData && p.IsWawancara)
            ? "Permintaan Data dan Wawancara"
            : (p.IsPermintaanData && p.IsObservasi)
            ? "Permintaan Data dan Observasi"
            : p.IsObservasi
            ? "Observasi"
            : "Permintaan Data";

        var vm = new SuratPemberianIzinVm
        {
            PermohonanPPIDID       = id,
            NoPermohonan           = p.NoPermohonan ?? string.Empty,
            TanggalSurat           = DateOnly.FromDateTime(DateTime.Today),
            IsKelompok             = false,
            JenisKegiatan          = jenisKegiatan,
            JenisKarya             = "Skripsi",
            NamaPemohon            = p.Pribadi?.Nama ?? string.Empty,
            NIMPemohon             = p.Pribadi?.PribadiPPID?.NIM,
            ProdiPemohon           = p.Pribadi?.PribadiPPID?.Jurusan,
            NamaPerwakilan         = p.Pribadi?.Nama ?? string.Empty,
            JudulPenelitian        = p.JudulPenelitian ?? string.Empty,
            NomorSuratPermohonan   = p.NoSuratPermohonan,
            TanggalSuratPermohonan = p.TanggalPermohonan,
            PerihalSuratPermohonan = jenisKegiatan.Contains("Wawancara")
                ? "Permohonan Izin Riset"
                : "Permohonan Izin Survey Data Awal",
            NamaInstansiPengirim   = p.Pribadi?.PribadiPPID?.Lembaga ?? string.Empty,
            JabatanPengirim        = "Dekan",
            TembusanInstansi       = p.Pribadi?.PribadiPPID?.Lembaga ?? string.Empty,
            BidangTujuan           = bidangList,
            // Satu anggota kosong sebagai placeholder untuk mode kelompok
            Anggota                = [new AnggotaKelompokVm
            {
                Nama  = p.Pribadi?.Nama ?? string.Empty,
                NIM   = p.Pribadi?.PribadiPPID?.NIM ?? string.Empty,
                Prodi = p.Pribadi?.PribadiPPID?.Jurusan ?? string.Empty,
            }],
        };

        return View(vm);
    }

    /// <summary>
    /// POST: render preview cetak surat pemberian izin.
    /// Tidak menyimpan ke DB — hanya render HTML cetak.
    /// </summary>
    [HttpPost("surat-pemberian-izin/cetak"), ValidateAntiForgeryToken]
    public IActionResult SuratPemberianIzinCetak(SuratPemberianIzinVm vm)
    {
        // Bersihkan anggota kosong sebelum validasi
        vm.Anggota = vm.Anggota
            .Where(a => !string.IsNullOrWhiteSpace(a.Nama))
            .ToList();

        // Bersihkan bidang kosong
        vm.BidangTujuan = vm.BidangTujuan
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        // Validasi mode kelompok: minimal 1 anggota dengan nama terisi
        if (vm.IsKelompok && vm.Anggota.Count == 0)
            ModelState.AddModelError(string.Empty, "Mode kelompok memerlukan minimal satu anggota.");

        // Validasi mode kelompok: nama perwakilan wajib
        if (vm.IsKelompok && string.IsNullOrWhiteSpace(vm.NamaPerwakilan))
            ModelState.AddModelError(nameof(vm.NamaPerwakilan), "Nama perwakilan kelompok wajib diisi.");

        // Validasi mode perorangan: nama pemohon wajib
        if (!vm.IsKelompok && string.IsNullOrWhiteSpace(vm.NamaPemohon))
            ModelState.AddModelError(nameof(vm.NamaPemohon), "Nama pemohon wajib diisi.");

        if (!ModelState.IsValid)
            return View("SuratPemberianIzin", vm);

        return View("SuratPemberianIzinCetak", vm);
    }

}
