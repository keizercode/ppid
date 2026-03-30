using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("petugas-loket")]
[Authorize(Roles = "Loket,Admin")]
public class PetugasLoketController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? "Loket";

    // ── DASHBOARD ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? status)
    {
        // Stats untuk cards
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        var dashVm = new DashboardVm
        {
            Total   = allStatus.Count,
            Proses  = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };
        ViewData["DashVm"] = dashVm;

        // List permohonan (filtered)
        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.NIK != null && p.Pribadi.NIK.Contains(q)));

        if (status.HasValue)
            query = query.Where(p => p.StatusPPIDID == status.Value);

        ViewData["LoketTitle"] = "Semua Permohonan";
        ViewData["Q"]          = q;
        ViewData["Status"]     = status;

        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── SubMenu: Permintaan Data ──────────────────────────────────────────

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

        ViewData["Q"]          = q;
        ViewData["SubMenuTitle"] = "Permintaan Data";
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── SubMenu: Wawancara ────────────────────────────────────────────────

    [HttpGet("wawancara")]
    public async Task<IActionResult> Wawancara(string? q)
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

        ViewData["Q"]          = q;
        ViewData["SubMenuTitle"] = "Wawancara";
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── SubMenu: Observasi ────────────────────────────────────────────────

    [HttpGet("observasi")]
    public async Task<IActionResult> Observasi(string? q)
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

        ViewData["Q"]          = q;
        ViewData["SubMenuTitle"] = "Observasi";
        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    // ── IDENTIFIKASI ──────────────────────────────────────────────────────

    [HttpGet("identifikasi")]
    public IActionResult Identifikasi() => View(new IdentifikasiPemohonVm());

    [HttpPost("identifikasi"), ValidateAntiForgeryToken]
    public IActionResult IdentifikasiPost(IdentifikasiPemohonVm model)
    {
        if (!ModelState.IsValid) return View("Identifikasi", model);

        if (model.Kategori == "Umum")
        {
            TempData["InfoUmum"] = "true";
            return View("Identifikasi", model);
        }

        var loketJenis = model.Kategori == "Mahasiswa"
            ? LoketJenis.Kepegawaian
            : LoketJenis.Umum;

        return RedirectToAction("DaftarPemohon", new { kategori = model.Kategori, loketJenis });
    }

    // ── DAFTAR PEMOHON ────────────────────────────────────────────────────

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
                ModelState.AddModelError(nameof(vm.BidangID),
                    "Unit kerja yang dipilih tidak valid. Silakan pilih ulang dari daftar.");
            else
                bidangGuid = parsed;
        }

        if (!ModelState.IsValid)
            return View("DaftarPemohon", vm);

        Guid lastId = Guid.Empty;
        string noPerm = string.Empty;

        var strategy = db.Database.CreateExecutionStrategy();

        var tempDir = Path.Combine(Path.GetTempPath(), "ppid_upload_" + Guid.NewGuid());
        var movers = new List<(string Temp, string Final)>();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                movers.Clear();

                // GenerateNoPermohonan sudah menerima LoketJenis → format MHS/UMM
                var (generatedNoPerm, nextSeq) = await db.GenerateNoPermohonan(vm.LoketJenis);

                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.UtcNow;

                    // ── 1. Pribadi ────────────────────────────────────────
                    var pribadi = await db.Pribadi.FirstOrDefaultAsync(p => p.NIK == vm.NIK);
                    if (pribadi == null)
                    {
                        pribadi = new Pribadi
                        {
                            NIK          = vm.NIK,
                            Nama         = vm.Nama,
                            Email        = vm.Email,
                            Telepon      = vm.Telepon,
                            Alamat       = vm.Alamat,
                            RT           = vm.RT,
                            RW           = vm.RW,
                            KelurahanID  = vm.KelurahanID,
                            KecamatanID  = vm.KecamatanID,
                            KabupatenID  = vm.KabupatenID,
                            NamaKelurahan = vm.NamaKelurahan,
                            NamaKecamatan = vm.NamaKecamatan,
                            NamaKabupaten = vm.NamaKabupaten,
                            CreatedAt    = now,
                            UpdatedAt    = now
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

                    // ── 2. PribadiPPID ────────────────────────────────────
                    var pribadiPPID = await db.PribadiPPID
                        .FirstOrDefaultAsync(p => p.PribadiID == pribadi.PribadiID);

                    if (pribadiPPID == null)
                    {
                        db.PribadiPPID.Add(new PribadiPPID
                        {
                            PribadiID   = pribadi.PribadiID,
                            ProvinsiID  = vm.ProvinsiID,
                            NamaProvinsi = vm.NamaProvinsi,
                            NIM         = vm.NIM,
                            Lembaga     = vm.Lembaga,
                            Fakultas    = vm.Fakultas,
                            Jurusan     = vm.Jurusan,
                            Pekerjaan   = vm.Pekerjaan,
                            CreatedAt   = now,
                            UpdatedAt   = now
                        });
                    }
                    else
                    {
                        pribadiPPID.NIM         = vm.NIM;
                        pribadiPPID.Lembaga     = vm.Lembaga;
                        pribadiPPID.Fakultas    = vm.Fakultas;
                        pribadiPPID.Jurusan     = vm.Jurusan;
                        pribadiPPID.Pekerjaan   = vm.Pekerjaan;
                        pribadiPPID.ProvinsiID  = vm.ProvinsiID;
                        pribadiPPID.NamaProvinsi = vm.NamaProvinsi;
                        pribadiPPID.UpdatedAt   = now;
                    }

                    // ── 3. PermohonanPPID ─────────────────────────────────
                    var batasWaktu = AppDbContext.HitungBatasWaktu(vm.TanggalPermohonan);

                    var permohonan = new PermohonanPPID
                    {
                        PribadiID         = pribadi.PribadiID,
                        NoPermohonan      = generatedNoPerm,
                        KategoriPemohon   = vm.Kategori,
                        LoketJenis        = vm.LoketJenis,
                        NoSuratPermohonan = vm.NoSuratPermohonan,
                        TanggalPermohonan = vm.TanggalPermohonan,
                        BatasWaktu        = batasWaktu,
                        Pengampu          = vm.Pengampu,
                        JudulPenelitian   = vm.JudulPenelitian,
                        LatarBelakang     = vm.LatarBelakang,
                        TujuanPermohonan  = vm.TujuanPermohonan,
                        IsObservasi       = vm.IsObservasi,
                        IsWawancara       = vm.IsWawancara,
                        IsPermintaanData  = vm.IsPermintaanData,
                        BidangID          = bidangGuid,
                        NamaBidang        = vm.NamaBidang,
                        StatusPPIDID      = StatusId.TerdaftarSistem,
                        Sequance          = nextSeq,
                        CratedAt          = now,
                        UpdatedAt         = now
                    };
                    db.PermohonanPPID.Add(permohonan);
                    await db.SaveChangesAsync();

                    // ── 4. Detail keperluan ───────────────────────────────
                    if (vm.IsObservasi)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID      = KeperluanId.Observasi,
                            DetailKeperluan  = vm.DetailObservasi ?? "-",
                            CreatedAt        = now
                        });

                    if (vm.IsPermintaanData)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID      = KeperluanId.PermintaanData,
                            DetailKeperluan  = vm.DetailPermintaanData ?? "-",
                            CreatedAt        = now
                        });

                    if (vm.IsWawancara)
                        db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail
                        {
                            PermohonanPPIDID = permohonan.PermohonanPPIDID,
                            KeperluanID      = KeperluanId.Wawancara,
                            DetailKeperluan  = vm.DetailWawancara ?? "-",
                            CreatedAt        = now
                        });

                    // ── 5. Upload dokumen ke temp ─────────────────────────
                    Directory.CreateDirectory(tempDir);
                    await StageUploadDokumen(permohonan.PermohonanPPIDID, vm.FileKTP,             JenisDokumenId.KTP,             "KTP",             now, tempDir, movers);
                    await StageUploadDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan", now, tempDir, movers);
                    await StageUploadDokumen(permohonan.PermohonanPPIDID, vm.FileProposal,        JenisDokumenId.Proposal,        "Proposal",         now, tempDir, movers);
                    await StageUploadDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris,     JenisDokumenId.AktaNotaris,     "Akta Notaris",     now, tempDir, movers);

                    // ── 6. Audit log ──────────────────────────────────────
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

            foreach (var (temp, final) in movers)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(final)!);
                System.IO.File.Move(temp, final, overwrite: true);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }

        TempData["Success"] = $"Permohonan berhasil didaftarkan dengan nomor <strong>{noPerm}</strong>";
        return RedirectToAction("InputIdentifikasi", new { id = lastId });
    }

    // ── INPUT IDENTIFIKASI ────────────────────────────────────────────────

    [HttpGet("input-identifikasi/{id}")]
    public async Task<IActionResult> InputIdentifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(p);
    }

    [HttpPost("input-identifikasi/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> InputIdentifikasiPost(Guid id)
    {
        var p = await db.PermohonanPPID.FindAsync(id);
        if (p == null) return NotFound();

        var statusLama   = p.StatusPPIDID;
        p.StatusPPIDID   = StatusId.IdentifikasiAwal;
        p.UpdatedAt      = DateTime.UtcNow;

        db.AddAuditLog(id, statusLama, StatusId.IdentifikasiAwal,
            "Identifikasi awal diinput oleh petugas loket.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Identifikasi awal berhasil diinput. Cetak formulir dan minta pemohon menandatangani.";
        return RedirectToAction("CetakIdentifikasi", new { id });
    }

    // ── CETAK IDENTIFIKASI ────────────────────────────────────────────────

    [HttpGet("cetak-identifikasi/{id}")]
    public async Task<IActionResult> CetakIdentifikasi(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(p);
    }

    // ── UPLOAD TTD ────────────────────────────────────────────────────────

    [HttpGet("upload-ttd/{id}")]
    public async Task<IActionResult> UploadTTD(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        return View(new UploadTTDVm
        {
            PermohonanPPIDID = id,
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            LoketJenis       = p.LoketJenis ?? LoketJenis.Kepegawaian
        });
    }

    [HttpPost("upload-ttd"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTTDPost(UploadTTDVm vm)
    {
        if (!ModelState.IsValid) return View("UploadTTD", vm);

        var now = DateTime.UtcNow;
        await UploadDokumen(vm.PermohonanPPIDID, vm.FileDokumenTTD,
            JenisDokumenId.IdentifikasiSigned, "Identifikasi TTD", now);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null)
        {
            var statusLama = p.StatusPPIDID;
            // Setelah upload TTD → ke Menunggu Verifikasi Kasubkel
            p.StatusPPIDID = StatusId.MenungguVerifikasi;
            p.UpdatedAt    = now;
            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguVerifikasi,
                "Dokumen identifikasi TTD diupload, menunggu verifikasi Kasubkel Kepegawaian.", CurrentUser);
        }
        await db.SaveChangesAsync();

        TempData["Success"] = "Dokumen berhasil diupload. Permohonan diteruskan ke Kasubkel Kepegawaian untuk verifikasi.";
        return RedirectToAction("Index");
    }

    // ── DETAIL ────────────────────────────────────────────────────────────

    [HttpGet("detail/{id}")]
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
        return View(p);
    }

    // ── HELPER: Upload langsung ───────────────────────────────────────────

    private async Task UploadDokumen(Guid permohonanId, IFormFile? file,
        int jenisDokId, string nama, DateTime now)
    {
        if (file == null || file.Length == 0) return;
        var uploadDir = Path.Combine(UploadsRoot, permohonanId.ToString());
        Directory.CreateDirectory(uploadDir);
        var fileName = $"{jenisDokId}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID     = permohonanId,
            NamaDokumenPPID      = nama,
            UploadDokumenPPID    = $"/uploads/{permohonanId}/{fileName}",
            JenisDokumenPPIDID   = jenisDokId,
            NamaJenisDokumenPPID = nama,
            CreatedAt            = now
        });
    }

    private async Task StageUploadDokumen(Guid permohonanId, IFormFile? file,
        int jenisDokId, string nama, DateTime now, string tempDir, List<(string Temp, string Final)> movers)
    {
        if (file == null || file.Length == 0) return;
        var fileName  = $"{jenisDokId}_{Path.GetFileName(file.FileName)}";
        var tempPath  = Path.Combine(tempDir, fileName);
        var finalPath = Path.Combine(UploadsRoot, permohonanId.ToString(), fileName);
        await using var stream = new FileStream(tempPath, FileMode.Create);
        await file.CopyToAsync(stream);
        movers.Add((tempPath, finalPath));
        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID     = permohonanId,
            NamaDokumenPPID      = nama,
            UploadDokumenPPID    = $"/uploads/{permohonanId}/{fileName}",
            JenisDokumenPPIDID   = jenisDokId,
            NamaJenisDokumenPPID = nama,
            CreatedAt            = now
        });
    }

//  ── MENU DATA ─────────────────────────────────────────────────────────

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

        if (export == "csv")
            return ExportCsvKepegawaian(list);

        ViewData["Q"] = q;
        return View("~/Views/MenuData/Index.cshtml", new MenuDataVm
        {
            List       = list,
            LoketJenis = LoketJenis.Kepegawaian,
            Judul      = "Data Permohonan — Loket Kepegawaian"
        });
    }

    private FileContentResult ExportCsvKepegawaian(List<PermohonanPPID> list)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("No Permohonan,Nama Pemohon,Kategori,NIK,NIM,Lembaga,Fakultas,Jurusan,Tgl Permohonan,Batas Waktu,Status,Tgl Selesai,Pengampu,Keperluan");
        foreach (var p in list)
        {
            var keperluan = string.Join("|", new[] {
                p.IsObservasi      ? "Observasi"       : null,
                p.IsPermintaanData ? "Permintaan Data" : null,
                p.IsWawancara      ? "Wawancara"       : null
            }.Where(x => x != null));
            sb.AppendLine(string.Join(",", new[] {
                Q(p.NoPermohonan), Q(p.Pribadi?.Nama), Q(p.KategoriPemohon),
                Q(p.Pribadi?.NIK), Q(p.Pribadi?.PribadiPPID?.NIM),
                Q(p.Pribadi?.PribadiPPID?.Lembaga),
                Q(p.Pribadi?.PribadiPPID?.Fakultas),
                Q(p.Pribadi?.PribadiPPID?.Jurusan),
                p.TanggalPermohonan?.ToString("dd/MM/yyyy"),
                p.BatasWaktu?.ToString("dd/MM/yyyy"),
                Q(p.Status?.NamaStatusPPID),
                p.TanggalSelesai?.ToString("dd/MM/yyyy"),
                Q(p.Pengampu), Q(keperluan)
            }));
        }
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"data_loket_kepegawaian_{DateTime.Now:yyyyMMdd}.csv");
    }

    private static string Q(string? s) =>
        s == null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";

    // ── EDIT PERMOHONAN ───────────────────────────────────────────────────
    // Tambahkan 2 action ini ke PetugasLoketController:

    [HttpGet("edit/{id}")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
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
            NoPermohonan     = p.NoPermohonan ?? "",
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            LoketJenis       = p.LoketJenis ?? LoketJenis.Kepegawaian,
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
        if (!ModelState.IsValid) return View("Edit", vm);

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
            "Data permohonan diedit oleh petugas loket.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = $"Permohonan <strong>{vm.NoPermohonan}</strong> berhasil diperbarui.";
        return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
    }
}
