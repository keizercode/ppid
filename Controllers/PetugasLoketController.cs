using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("petugas-loket")]
[Authorize(Roles = $"{AppRoles.Loket},{AppRoles.Admin}")]
public class PetugasLoketController(AppDbContext db, IWebHostEnvironment env)
    : LoketBaseController(db, env)
{
    private string CurrentUser => User.Identity?.Name ?? AppRoles.Loket;

    // ── DASHBOARD ─────────────────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? status)
    {
        var allStatus = await db.PermohonanPPID
            .AsNoTracking()
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        ViewData["DashVm"] = new DashboardVm
        {
            Total        = allStatus.Count,
            Proses       = allStatus.Count(s => StatusId.IsProses(s)),
            Selesai      = allStatus.Count(s => StatusId.IsSelesai(s)),
            MonthlyStats = await db.GetMonthlyStats()
        };

        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
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
            .Where(p => p.IsObservasi)
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

        Guid   lastId = Guid.Empty;
        string noPerm = string.Empty;

        var strategy = db.Database.CreateExecutionStrategy();
        var tempDir  = Path.Combine(Path.GetTempPath(), $"ppid_upload_{Guid.NewGuid()}");
        var movers   = new List<(string Temp, string Final)>();

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
                            NIK           = vm.NIK,   Nama     = vm.Nama,
                            Email         = vm.Email,  Telepon  = vm.Telepon,
                            Alamat        = vm.Alamat, RT       = vm.RT,      RW = vm.RW,
                            KelurahanID   = vm.KelurahanID,  KecamatanID  = vm.KecamatanID,
                            KabupatenID   = vm.KabupatenID,  NamaKelurahan = vm.NamaKelurahan,
                            NamaKecamatan = vm.NamaKecamatan, NamaKabupaten = vm.NamaKabupaten,
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

                    var pribadiPPID = await db.PribadiPPID
                        .FirstOrDefaultAsync(p => p.PribadiID == pribadi.PribadiID);

                    if (pribadiPPID == null)
                    {
                        db.PribadiPPID.Add(new PribadiPPID
                        {
                            PribadiID    = pribadi.PribadiID,
                            ProvinsiID   = vm.ProvinsiID,  NamaProvinsi = vm.NamaProvinsi,
                            NIM          = vm.NIM,         Lembaga      = vm.Lembaga,
                            Fakultas     = vm.Fakultas,    Jurusan      = vm.Jurusan,
                            Pekerjaan    = vm.Pekerjaan,
                            CreatedAt    = now, UpdatedAt = now
                        });
                    }
                    else
                    {
                        pribadiPPID.NIM          = vm.NIM;
                        pribadiPPID.Lembaga      = vm.Lembaga;
                        pribadiPPID.Fakultas     = vm.Fakultas;
                        pribadiPPID.Jurusan      = vm.Jurusan;
                        pribadiPPID.Pekerjaan    = vm.Pekerjaan;
                        pribadiPPID.ProvinsiID   = vm.ProvinsiID;
                        pribadiPPID.NamaProvinsi = vm.NamaProvinsi;
                        pribadiPPID.UpdatedAt    = now;
                    }

                    var permohonan = new PermohonanPPID
                    {
                        PribadiID         = pribadi.PribadiID,
                        NoPermohonan      = generatedNoPerm,
                        KategoriPemohon   = vm.Kategori,
                        LoketJenis        = vm.LoketJenis,
                        NoSuratPermohonan = vm.NoSuratPermohonan,
                        TanggalPermohonan = vm.TanggalPermohonan,
                        BatasWaktu        = AppDbContext.HitungBatasWaktu(vm.TanggalPermohonan),
                        Pengampu          = vm.Pengampu,
                        TeleponPengampu   = vm.TeleponPengampu,
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
                        CratedAt          = now, UpdatedAt = now
                    };
                    db.PermohonanPPID.Add(permohonan);
                    await db.SaveChangesAsync();

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

                    Directory.CreateDirectory(tempDir);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileKTP,             JenisDokumenId.KTP,             "KTP",             now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan", now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileProposal,        JenisDokumenId.Proposal,        "Proposal",         now, tempDir, movers);
                    await StageDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris,     JenisDokumenId.AktaNotaris,     "Akta Notaris",     now, tempDir, movers);

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

        // Guard: hanya dari TerdaftarSistem yang bisa input identifikasi
        if (p.StatusPPIDID != StatusId.TerdaftarSistem)
        {
            TempData["Error"] = "Identifikasi awal sudah pernah diinput sebelumnya.";
            return RedirectToAction("Detail", new { id });
        }

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.IdentifikasiAwal;
        p.UpdatedAt    = DateTime.UtcNow;

        db.AddAuditLog(id, statusLama, StatusId.IdentifikasiAwal,
            "Identifikasi awal diinput oleh petugas loket.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Identifikasi awal berhasil diinput. Cetak formulir dan minta pemohon menandatangani.";
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
        return View(p);
    }

    // ── UPLOAD TTD ────────────────────────────────────────────────────────
    //
    // FIX: Tambah guard status agar tidak terjadi looping.
    // Upload TTD hanya diizinkan dari status IdentifikasiAwal (3).
    // Setelah TTD diupload → MenungguVerifikasi (14) → tidak bisa upload lagi.
    // Kasubkel memverifikasi → MenungguSuratIzin (4) → alur berlanjut maju.
    // Tanpa guard ini, operator bisa navigasi langsung ke URL dan mereset status
    // mundur ke MenungguVerifikasi, merusak progress verifikasi Kasubkel.

    [HttpGet("upload-ttd/{id:guid}")]
    public async Task<IActionResult> UploadTTD(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();

        // ── Guard: cegah loop — hanya IdentifikasiAwal yang boleh upload TTD ──
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
            NoPermohonan     = p.NoPermohonan!,
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            LoketJenis       = p.LoketJenis ?? LoketJenis.Kepegawaian
        });
    }

    [HttpPost("upload-ttd"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTTDPost(UploadTTDVm vm)
    {
        if (!ModelState.IsValid) return View("UploadTTD", vm);

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        // ── Guard idempoten: cegah looping status ──────────────────────────
        // Tanpa guard ini, POST langsung ke URL bisa mereset status dari
        // MenungguVerifikasi / MenungguSuratIzin kembali ke MenungguVerifikasi,
        // menghapus progress verifikasi Kasubkel Kepegawaian.
        if (p.StatusPPIDID != StatusId.IdentifikasiAwal)
        {
            TempData["Error"] = "Upload TTD tidak diizinkan — permohonan sudah berada di tahap " +
                                $"lebih lanjut (status: {p.StatusPPIDID}). Tidak ada aksi yang diperlukan.";
            return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
        }

        var now   = DateTime.UtcNow;
        var error = await UploadDokumen(vm.PermohonanPPIDID, vm.FileDokumenTTD,
            JenisDokumenId.IdentifikasiSigned, "Identifikasi TTD", now);

        if (error != null)
        {
            ModelState.AddModelError(nameof(vm.FileDokumenTTD), error);
            return View("UploadTTD", vm);
        }

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.MenungguVerifikasi;
        p.UpdatedAt    = now;
        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguVerifikasi,
            "Dokumen identifikasi TTD diupload, menunggu verifikasi Kasubkel Kepegawaian.", CurrentUser);

        await db.SaveChangesAsync();
        TempData["Success"] = "Dokumen berhasil diupload. Permohonan diteruskan ke Kasubkel Kepegawaian untuk verifikasi.";
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

        return View(p);
    }

    // ── EDIT ──────────────────────────────────────────────────────────────

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
            NoPermohonan     = p.NoPermohonan ?? "",
            NamaPemohon      = p.Pribadi?.Nama ?? "",
            LoketJenis       = p.LoketJenis ?? LoketJenis.Kepegawaian,
            JudulPenelitian  = p.JudulPenelitian ?? "",
            LatarBelakang    = p.LatarBelakang ?? "",
            TujuanPermohonan = p.TujuanPermohonan ?? "",
            Pengampu         = p.Pengampu,
            TeleponPengampu  = p.TeleponPengampu,
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
        p.TeleponPengampu  = vm.TeleponPengampu;
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

    // ── BATALKAN PERMOHONAN ───────────────────────────────────────────────

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
            .Where(t => t.PermohonanPPIDID == id
                     && t.StatusTask != SubTaskStatus.Dibatalkan)
            .ToListAsync();

        return View(new BatalkanPermohonanVm
        {
            PermohonanPPIDID   = id,
            NoPermohonan       = p.NoPermohonan    ?? string.Empty,
            NamaPemohon        = p.Pribadi?.Nama   ?? string.Empty,
            Kategori           = p.KategoriPemohon ?? string.Empty,
            JudulPenelitian    = p.JudulPenelitian ?? string.Empty,
            StatusPPIDID       = p.StatusPPIDID    ?? StatusId.TerdaftarSistem,
            StatusLabel        = p.Status?.NamaStatusPPID ?? "—",
            AdaSubTasks        = subTasks.Count > 0,
            JumlahSubTasks     = subTasks.Count,
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
                vm.StatusLabel     = pReload.Status?.NamaStatusPPID ?? "—";
                vm.JudulPenelitian = pReload.JudulPenelitian ?? string.Empty;
                vm.Kategori        = pReload.KategoriPemohon ?? string.Empty;

                var subCount = await db.SubTaskPPID
                    .CountAsync(t => t.PermohonanPPIDID == vm.PermohonanPPIDID
                                  && t.StatusTask != SubTaskStatus.Dibatalkan);
                vm.AdaSubTasks    = subCount > 0;
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

        // ── Wrap SaveChanges dalam try-catch ──────────────────────────────
        // Tangkap kemungkinan DbUpdateException (FK violation, missing column
        // akibat migration belum diapply, dsb.) agar tidak jatuh ke 500 error page.
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            // Log detail untuk debugging admin
            var inner = dbEx.InnerException?.Message ?? dbEx.Message;
            TempData["Error"] =
                $"Terjadi kesalahan database saat membatalkan permohonan. " +
                $"Pastikan migrasi <em>AddBatalkanPermohonan</em> sudah dijalankan (<code>dotnet ef database update</code>). " +
                $"Ref: {DateTime.UtcNow:yyyyMMddHHmmss}";
            return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
        }
        catch (Exception)
        {
            TempData["Error"] =
                $"Terjadi kesalahan tidak terduga saat menyimpan pembatalan. " +
                $"Ref: {DateTime.UtcNow:yyyyMMddHHmmss}";
            return RedirectToAction("Detail", new { id = vm.PermohonanPPIDID });
        }

        TempData["Success"] =
            $"Permohonan <strong>{vm.NoPermohonan}</strong> berhasil dibatalkan. " +
            "Dokumen dan riwayat tetap tersimpan untuk keperluan audit.";

        return RedirectToAction("Index");
    }

    // ── MENU DATA ─────────────────────────────────────────────────────────

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
            List       = list,
            LoketJenis = LoketJenis.Kepegawaian,
            Judul      = "Data Permohonan — Loket Kepegawaian"
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
}
