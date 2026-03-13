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
        // Stats: SELALU dari keseluruhan data — tidak terpengaruh filter apapun.
        // Satu query ringan, hanya tarik kolom StatusPPIDID.
        var allStatuses = await db.PermohonanPPID
            .AsNoTracking()
            .Select(p => p.StatusPPIDID)
            .ToListAsync();

        SetStatsViewData(allStatuses);

        // Data tabel — boleh difilter
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
        ViewData["Q"] = q;
        ViewData["Status"] = status;

        return View(await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    [HttpGet("kepegawaian")]
    public async Task<IActionResult> LoketKepegawaian(string? q)
    {
        var allStatuses = await db.PermohonanPPID
            .AsNoTracking()
            .Select(p => p.StatusPPIDID)
            .ToListAsync();
        SetStatsViewData(allStatuses);

        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Kepegawaian || p.KategoriPemohon == "Mahasiswa")
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["LoketTitle"] = "Loket Kepegawaian — Mahasiswa / Peneliti";
        ViewData["LoketJenisAktif"] = LoketJenis.Kepegawaian;
        ViewData["Q"] = q;
        return View("Index", await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    [HttpGet("umum")]
    public async Task<IActionResult> LoketUmum(string? q)
    {
        var allStatuses = await db.PermohonanPPID
            .AsNoTracking()
            .Select(p => p.StatusPPIDID)
            .ToListAsync();
        SetStatsViewData(allStatuses);

        var query = db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.LoketJenis == LoketJenis.Umum || p.KategoriPemohon == "LSM")
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
            query = query.Where(p =>
                (p.NoPermohonan != null && p.NoPermohonan.Contains(q)) ||
                (p.Pribadi != null && p.Pribadi.Nama != null && p.Pribadi.Nama.Contains(q)));

        ViewData["LoketTitle"] = "Loket Umum — LSM / Organisasi";
        ViewData["LoketJenisAktif"] = LoketJenis.Umum;
        ViewData["Q"] = q;
        return View("Index", await query.OrderByDescending(p => p.CratedAt).ToListAsync());
    }

    /// <summary>
    /// Hitung 4 stat cards dari list status yang sudah di-fetch.
    /// Dipisah ke helper agar tidak duplikat kode di setiap action.
    /// </summary>
    private void SetStatsViewData(List<int?> statuses)
    {
        ViewData["StatTotal"] = statuses.Count;
        ViewData["StatTerdaftar"] = statuses.Count(s => s <= StatusId.IdentifikasiAwal);
        ViewData["StatDiproses"] = statuses.Count(s => s > StatusId.SuratIzinTerbit && s < StatusId.DataSiap);
        ViewData["StatSelesai"] = statuses.Count(s => s >= StatusId.DataSiap);
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
        if (!ModelState.IsValid) return View("DaftarPemohon", vm);

        var now = DateTime.UtcNow;

        // 1. Pribadi
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

        // 2. PribadiPPID
        var pribadiPPID = await db.PribadiPPID.FirstOrDefaultAsync(p => p.PribadiID == pribadi.PribadiID);
        if (pribadiPPID == null)
        {
            pribadiPPID = new PribadiPPID
            {
                PribadiID = pribadi.PribadiID,
                ProvinsiID = vm.ProvinsiID,
                NamaProvinsi = vm.NamaProvinsi,
                Lembaga = vm.Lembaga,
                Fakultas = vm.Fakultas,
                Jurusan = vm.Jurusan,
                Pekerjaan = vm.Pekerjaan,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.PribadiPPID.Add(pribadiPPID);
        }
        else
        {
            pribadiPPID.Lembaga = vm.Lembaga;
            pribadiPPID.Fakultas = vm.Fakultas;
            pribadiPPID.Jurusan = vm.Jurusan;
            pribadiPPID.Pekerjaan = vm.Pekerjaan;
            pribadiPPID.ProvinsiID = vm.ProvinsiID;
            pribadiPPID.NamaProvinsi = vm.NamaProvinsi;
            pribadiPPID.UpdatedAt = now;
        }

        // 3. PermohonanPPID
        //
        // BUG FIX: Versi lama memanggil GenerateNoPermohonan() yang hanya
        // mengembalikan string, lalu menghitung Sequance LAGI secara terpisah.
        // Akibatnya:
        //   (a) Race condition → dua request bisa mendapat NoPermohonan sama
        //       → duplicate key violation pada IX_PermohonanPPID_NoPermohonan.
        //   (b) Sequance yang disimpan ke DB bisa berbeda dengan yang ada di
        //       NoPermohonan jika ada insert lain di antara dua query.
        //
        // SOLUSI: GenerateNoPermohonan() kini mengembalikan (NoPermohonan, Sequence)
        // dari satu transaksi terkunci. Controller cukup destructure hasilnya.
        var (noPerm, nextSeq) = await db.GenerateNoPermohonan();

        var permohonan = new PermohonanPPID
        {
            PribadiID = pribadi.PribadiID,
            NoPermohonan = noPerm,
            KategoriPemohon = vm.Kategori,
            LoketJenis = vm.LoketJenis,
            NoSuratPermohonan = vm.NoSuratPermohonan,
            TanggalPermohonan = vm.TanggalPermohonan,
            JudulPenelitian = vm.JudulPenelitian,
            LatarBelakang = vm.LatarBelakang,
            TujuanPermohonan = vm.TujuanPermohonan,
            IsObservasi = vm.IsObservasi,
            IsWawancara = vm.IsWawancara,
            IsPermintaanData = vm.IsPermintaanData,
            BidangID = string.IsNullOrEmpty(vm.BidangID) ? null : Guid.Parse(vm.BidangID),
            NamaBidang = vm.NamaBidang,
            StatusPPIDID = StatusId.TerdaftarSistem,
            Sequance = nextSeq,          // ← dari GenerateNoPermohonan, bukan query terpisah
            TokenLacak = TokenGenerator.Generate(),
            CratedAt = now,
            UpdatedAt = now
        };
        db.PermohonanPPID.Add(permohonan);
        await db.SaveChangesAsync();

        // 4. Detail keperluan
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

        // 5. Upload dokumen
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileKTP, JenisDokumenId.KTP, "KTP", now);
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan", now);
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileProposal, JenisDokumenId.Proposal, "Proposal", now);
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris, JenisDokumenId.AktaNotaris, "Akta Notaris", now);

        // 6. Audit log
        db.AddAuditLog(permohonan.PermohonanPPIDID, null, StatusId.TerdaftarSistem,
            $"Permohonan didaftarkan oleh petugas loket. Keperluan: " +
            $"{(vm.IsObservasi ? "Observasi " : "")}{(vm.IsPermintaanData ? "Data " : "")}{(vm.IsWawancara ? "Wawancara" : "")}",
            CurrentUser);

        await db.SaveChangesAsync();

        TempData["Success"] = $"Permohonan berhasil didaftarkan dengan nomor <strong>{noPerm}</strong>";
        return RedirectToAction("InputIdentifikasi", new { id = permohonan.PermohonanPPIDID });
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

        var statusLama = p.StatusPPIDID;
        p.StatusPPIDID = StatusId.IdentifikasiAwal;
        p.UpdatedAt = DateTime.UtcNow;

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
            NoPermohonan = p.NoPermohonan!,
            NamaPemohon = p.Pribadi?.Nama ?? "",
            LoketJenis = p.LoketJenis ?? LoketJenis.Kepegawaian
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
            p.StatusPPIDID = StatusId.MenungguSuratIzin;
            p.UpdatedAt = now;
            db.AddAuditLog(vm.PermohonanPPIDID, statusLama, StatusId.MenungguSuratIzin,
                "Dokumen identifikasi TTD diupload, menunggu surat izin dari Kepegawaian.", CurrentUser);
        }
        await db.SaveChangesAsync();

        TempData["Success"] = "Dokumen berhasil diupload. Permohonan diteruskan ke Kepegawaian untuk penerbitan surat izin.";
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

    // ── HELPER UPLOAD ─────────────────────────────────────────────────────

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
            PermohonanPPIDID = permohonanId,
            NamaDokumenPPID = nama,
            UploadDokumenPPID = $"/uploads/{permohonanId}/{fileName}",
            JenisDokumenPPIDID = jenisDokId,
            NamaJenisDokumenPPID = nama,
            CreatedAt = now
        });
    }
}
