using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Loket Umum — melayani pemohon non-mahasiswa (LSM, Organisasi, Perusahaan).
/// Scope: permohonan dengan LoketJenis = "Umum" (prefix UMM).
/// </summary>
[Route("loket-umum")]
[Authorize(Roles = $"{AppRoles.LoketUmum},{AppRoles.Admin}")]
public class LoketUmumController(AppDbContext db, IWebHostEnvironment env)
    : LoketBaseController(db, env)
{
    private string CurrentUser => User.Identity?.Name ?? AppRoles.LoketUmum;

    // ── DASHBOARD ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? status)
    {
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Where(p => p.LoketJenis == LoketJenis.Umum)
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        ViewData["DashVm"] = new DashboardVm
        {
            Total        = allStatus.Count,
            Proses       = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai      = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await GetMonthlyStatsUmum()
        };

        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Umum)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.NIK  != null && p.Pribadi.NIK.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["Q"]      = q;
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
            .Where(p => p.LoketJenis == LoketJenis.Umum && p.IsPermintaanData)
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
    public async Task<IActionResult> Wawancara(string? q)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Include(p => p.Jadwal)
            .Where(p => p.LoketJenis == LoketJenis.Umum && p.IsWawancara)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["Q"] = q;
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── Sub-menu: Observasi ───────────────────────────────────────────────

    [HttpGet("observasi")]
    public async Task<IActionResult> Observasi(string? q)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Include(p => p.Jadwal)
            .Where(p => p.LoketJenis == LoketJenis.Umum && p.IsObservasi)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["Q"] = q;
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── IDENTIFIKASI ──────────────────────────────────────────────────────

    [HttpGet("identifikasi")]
    public IActionResult Identifikasi() => View(new IdentifikasiPemohonVm());

    [HttpPost("identifikasi"), ValidateAntiForgeryToken]
    public IActionResult IdentifikasiPost(IdentifikasiPemohonVm model)
    {
        if (!ModelState.IsValid) return View("Identifikasi", model);
        return RedirectToAction("DaftarPemohon",
            new { kategori = model.Kategori, loketJenis = LoketJenis.Umum });
    }

    // ── DAFTAR PEMOHON ────────────────────────────────────────────────────

    [HttpGet("daftar")]
    public IActionResult DaftarPemohon(string kategori = "LSM")
        => View(new DaftarPemohonVm { Kategori = kategori, LoketJenis = LoketJenis.Umum });

    [HttpPost("daftar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DaftarPemohonPost(DaftarPemohonVm vm)
    {
        // Loket Umum selalu menggunakan prefix UMM
        vm.LoketJenis = LoketJenis.Umum;

        Guid? bidangGuid = null;
        if (!string.IsNullOrEmpty(vm.BidangID) && Guid.TryParse(vm.BidangID, out var parsed))
            bidangGuid = parsed;

        if (!ModelState.IsValid) return View("DaftarPemohon", vm);

        Guid   lastId = Guid.Empty;
        string noPerm = string.Empty;

        var strategy = db.Database.CreateExecutionStrategy();
        var tempDir  = Path.Combine(Path.GetTempPath(), $"ppid_umum_{Guid.NewGuid()}");
        var movers   = new List<(string Temp, string Final)>();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                movers.Clear();
                var (generatedNoPerm, nextSeq) = await db.GenerateNoPermohonan(LoketJenis.Umum);

                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.UtcNow;

                    // 1. Pribadi (upsert by NIK)
                    var pribadi = await db.Pribadi.FirstOrDefaultAsync(p => p.NIK == vm.NIK);
                    if (pribadi == null)
                    {
                        pribadi = new Pribadi
                        {
                            NIK           = vm.NIK,   Nama     = vm.Nama,
                            Email         = vm.Email,  Telepon  = vm.Telepon,
                            Alamat        = vm.Alamat, RT       = vm.RT,      RW = vm.RW,
                            KelurahanID   = vm.KelurahanID,   KecamatanID   = vm.KecamatanID,
                            KabupatenID   = vm.KabupatenID,   NamaKelurahan  = vm.NamaKelurahan,
                            NamaKecamatan = vm.NamaKecamatan, NamaKabupaten  = vm.NamaKabupaten,
                            CreatedAt     = now, UpdatedAt = now
                        };
                        db.Pribadi.Add(pribadi);
                    }
                    else
                    {
                        pribadi.Nama      = vm.Nama;
                        pribadi.Email     = vm.Email;
                        pribadi.Telepon   = vm.Telepon;
                        pribadi.UpdatedAt = now;
                    }
                    await db.SaveChangesAsync();

                    // 2. PribadiPPID (upsert)
                    var ppid = await db.PribadiPPID
                        .FirstOrDefaultAsync(p => p.PribadiID == pribadi.PribadiID);
                    if (ppid == null)
                    {
                        db.PribadiPPID.Add(new PribadiPPID
                        {
                            PribadiID    = pribadi.PribadiID,
                            Lembaga      = vm.Lembaga,   Pekerjaan    = vm.Pekerjaan,
                            ProvinsiID   = vm.ProvinsiID, NamaProvinsi = vm.NamaProvinsi,
                            CreatedAt    = now, UpdatedAt = now
                        });
                    }
                    else
                    {
                        ppid.Lembaga      = vm.Lembaga;
                        ppid.Pekerjaan    = vm.Pekerjaan;
                        ppid.ProvinsiID   = vm.ProvinsiID;
                        ppid.NamaProvinsi = vm.NamaProvinsi;
                        ppid.UpdatedAt    = now;
                    }

                    // 3. PermohonanPPID
                    var permohonan = new PermohonanPPID
                    {
                        PribadiID         = pribadi.PribadiID,
                        NoPermohonan      = generatedNoPerm,
                        KategoriPemohon   = vm.Kategori,
                        LoketJenis        = LoketJenis.Umum,
                        NoSuratPermohonan = vm.NoSuratPermohonan,
                        TanggalPermohonan = vm.TanggalPermohonan,
                        BatasWaktu        = AppDbContext.HitungBatasWaktu(vm.TanggalPermohonan),
                        Pengampu          = vm.Pengampu,
                        JudulPenelitian   = vm.JudulPenelitian,
                        LatarBelakang     = vm.LatarBelakang,
                        TujuanPermohonan  = vm.TujuanPermohonan,
                        IsObservasi       = vm.IsObservasi,
                        IsWawancara       = vm.IsWawancara,
                        IsPermintaanData  = vm.IsPermintaanData,
                        BidangID          = bidangGuid, NamaBidang = vm.NamaBidang,
                        StatusPPIDID      = StatusId.TerdaftarSistem,
                        Sequance          = nextSeq, CratedAt = now, UpdatedAt = now
                    };
                    db.PermohonanPPID.Add(permohonan);
                    await db.SaveChangesAsync();

                    // 4. Detail keperluan
                    if (vm.IsObservasi)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID = KeperluanId.Observasi,
                            DetailKeperluan = vm.DetailObservasi ?? "-", CreatedAt = now
                        });
                    if (vm.IsPermintaanData)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID = KeperluanId.PermintaanData,
                            DetailKeperluan = vm.DetailPermintaanData ?? "-", CreatedAt = now
                        });
                    if (vm.IsWawancara)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID = KeperluanId.Wawancara,
                            DetailKeperluan = vm.DetailWawancara ?? "-", CreatedAt = now
                        });

                    // 5. Dokumen ke temp
                    Directory.CreateDirectory(tempDir);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileKTP,             JenisDokumenId.KTP,             "KTP",              now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan", now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris,     JenisDokumenId.AktaNotaris,     "Akta Notaris",     now, tempDir, movers);

                    db.AddAuditLog(permohonan.PermohonanPPIDID, null, StatusId.TerdaftarSistem,
                        $"Permohonan didaftarkan Loket Umum (UMM). Keperluan: " +
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
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }

        TempData["Success"] = $"Permohonan berhasil didaftarkan dengan nomor <strong>{noPerm}</strong>";
        return RedirectToAction("InputIdentifikasi", new { id = lastId });
    }

    // ── INPUT IDENTIFIKASI ────────────────────────────────────────────────

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

        var lama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.IdentifikasiAwal;
        p.UpdatedAt    = DateTime.UtcNow;

        db.AddAuditLog(id, lama, StatusId.IdentifikasiAwal,
            "Identifikasi awal diinput Loket Umum.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Identifikasi awal diinput. Cetak formulir dan minta pemohon menandatangani.";
        return RedirectToAction("CetakIdentifikasi", new { id });
    }

    // ── CETAK IDENTIFIKASI ────────────────────────────────────────────────

    [HttpGet("cetak-identifikasi/{id:guid}")]
    public async Task<IActionResult> CetakIdentifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View("~/Views/PetugasLoket/CetakIdentifikasi.cshtml", p);
    }

    // ── UPLOAD TTD ────────────────────────────────────────────────────────

    [HttpGet("upload-ttd/{id:guid}")]
    public async Task<IActionResult> UploadTTD(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        return View("~/Views/PetugasLoket/UploadTTD.cshtml", new UploadTTDVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            LoketJenis       = LoketJenis.Umum
        });
    }

    [HttpPost("upload-ttd"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTTDPost(UploadTTDVm vm)
    {
        if (!ModelState.IsValid) return View("~/Views/PetugasLoket/UploadTTD.cshtml", vm);

        var now   = DateTime.UtcNow;
        var error = await UploadDokumen(vm.PermohonanPPIDID, vm.FileDokumenTTD,
            JenisDokumenId.IdentifikasiSigned, "Identifikasi TTD", now);

        if (error != null)
        {
            ModelState.AddModelError(nameof(vm.FileDokumenTTD), error);
            return View("~/Views/PetugasLoket/UploadTTD.cshtml", vm);
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null)
        {
            var lama = p.StatusPPIDID;
            p.StatusPPIDID = StatusId.MenungguVerifikasi;
            p.UpdatedAt    = now;
            db.AddAuditLog(vm.PermohonanPPIDID, lama, StatusId.MenungguVerifikasi,
                "Dokumen TTD diupload Loket Umum, menunggu verifikasi Kasubkel Umum.", CurrentUser);
        }

        await db.SaveChangesAsync();
        TempData["Success"] = "Dokumen diupload. Diteruskan ke Kasubkel Umum untuk verifikasi.";
        return RedirectToAction("Index");
    }

    // ── DETAIL ────────────────────────────────────────────────────────────

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

        return View("~/Views/PetugasLoket/Detail.cshtml", p);
    }

    // ── EDIT ──────────────────────────────────────────────────────────────

    [HttpGet("edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id && x.LoketJenis == LoketJenis.Umum);
        if (p == null) return NotFound();

        if (p.StatusPPIDID > StatusId.MenungguSuratIzin)
        {
            TempData["Error"] = "Permohonan tidak dapat diedit pada status ini.";
            return RedirectToAction("Detail", new { id });
        }

        return View("~/Views/PetugasLoket/Edit.cshtml", new EditPermohonanVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan ?? "",
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            LoketJenis       = LoketJenis.Umum,
            JudulPenelitian  = p.JudulPenelitian ?? "",
            LatarBelakang    = p.LatarBelakang ?? "",
            TujuanPermohonan = p.TujuanPermohonan ?? "",
            Pengampu         = p.Pengampu,
            BatasWaktu       = p.BatasWaktu,
            IsObservasi      = p.IsObservasi,
            IsPermintaanData = p.IsPermintaanData,
            IsWawancara      = p.IsWawancara,
        });
    }

    [HttpPost("edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPost(EditPermohonanVm vm)
    {
        if (!ModelState.IsValid) return View("~/Views/PetugasLoket/Edit.cshtml", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        p.JudulPenelitian  = vm.JudulPenelitian;
        p.LatarBelakang    = vm.LatarBelakang;
        p.TujuanPermohonan = vm.TujuanPermohonan;
        p.Pengampu         = vm.Pengampu;
        p.BatasWaktu       = vm.BatasWaktu;
        p.IsObservasi      = vm.IsObservasi;
        p.IsPermintaanData = vm.IsPermintaanData;
        p.IsWawancara      = vm.IsWawancara;
        p.UpdatedAt        = DateTime.UtcNow;

        db.AddAuditLog(vm.PermohonanPPIDID, p.StatusPPIDID,
            p.StatusPPIDID ?? StatusId.TerdaftarSistem,
            "Data permohonan diedit oleh petugas Loket Umum.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = $"Data permohonan <strong>{vm.NoPermohonan}</strong> berhasil diperbarui.";
        return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
    }

    // ── MENU DATA ─────────────────────────────────────────────────────────

    [HttpGet("data")]
    public async Task<IActionResult> MenuData(string? q, string? export)
    {
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(p => p.Status)
            .Include(p => p.Detail).ThenInclude(d => d.Keperluan)
            .Where(p => p.LoketJenis == LoketJenis.Umum)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        var list = await query.OrderByDescending(p => p.CratedAt).ToListAsync();

        if (export == "csv") return BuildCsvUmum(list);

        ViewData["Q"] = q;
        return View("~/Views/MenuData/Index.cshtml", new MenuDataVm
        {
            List       = list,
            LoketJenis = LoketJenis.Umum,
            Judul      = "Data Permohonan — Loket Umum"
        });
    }

    private FileContentResult BuildCsvUmum(List<PermohonanPPID> list)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("No Permohonan,Nama Pemohon,Kategori,NIK,Lembaga,Tgl Permohonan,Batas Waktu,Status,Tgl Selesai,Pengampu,Keperluan");

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
                CsvQuote(p.Pribadi?.PribadiPPID?.Lembaga),
                p.TanggalPermohonan?.ToString("dd/MM/yyyy"),
                p.BatasWaktu?.ToString("dd/MM/yyyy"),
                CsvQuote(p.Status?.NamaStatusPPID),
                p.TanggalSelesai?.ToString("dd/MM/yyyy"),
                CsvQuote(p.Pengampu),
                CsvQuote(keperluan)));
        }

        return ToCsvFile(sb.ToString(), "data_loket_umum");
    }

    // ── Private: Monthly Stats Loket Umum ────────────────────────────────

    private async Task<List<MonthlyStatRow>> GetMonthlyStatsUmum()
    {
        var from     = DateTime.UtcNow.AddMonths(-11);
        var fromDate = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var raw = await db.PermohonanPPID.AsNoTracking()
            .Where(p => p.LoketJenis == LoketJenis.Umum && p.CratedAt >= fromDate)
            .Select(p => new { p.CratedAt, p.StatusPPIDID })
            .ToListAsync();

        return raw
            .GroupBy(p => new { p.CratedAt!.Value.Year, p.CratedAt!.Value.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyStatRow
            {
                Label   = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yy"),
                Total   = g.Count(),
                Proses  = g.Count(p => StatusId.IsProses(p.StatusPPIDID)),
                Selesai = g.Count(p => StatusId.IsSelesai(p.StatusPPIDID))
            }).ToList();
    }
}
