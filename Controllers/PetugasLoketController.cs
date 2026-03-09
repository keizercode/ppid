using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("petugas-loket")]
public class PetugasLoketController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    // GET /petugas-loket
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();
        return View(list);
    }

    // ── IDENTIFIKASI ──────────────────────────────────────────────────────────
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
        return RedirectToAction("DaftarPemohon", new { kategori = model.Kategori });
    }

    // ── DAFTAR PEMOHON ────────────────────────────────────────────────────────
    [HttpGet("daftar")]
    public IActionResult DaftarPemohon(string kategori)
        => View(new DaftarPemohonVm { Kategori = kategori });

    [HttpPost("daftar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DaftarPemohonPost(DaftarPemohonVm vm)
    {
        if (!ModelState.IsValid) return View("DaftarPemohon", vm);

        // 1. Cek apakah NIK sudah ada
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
                NamaKelurahan= vm.NamaKelurahan,
                NamaKecamatan= vm.NamaKecamatan,
                NamaKabupaten= vm.NamaKabupaten,
                CreatedAt    = DateTime.Now,
                UpdatedAt    = DateTime.Now
            };
            db.Pribadi.Add(pribadi);
        }
        else
        {
            // Update data terbaru
            pribadi.Nama     = vm.Nama;
            pribadi.Email    = vm.Email;
            pribadi.Telepon  = vm.Telepon;
            pribadi.UpdatedAt = DateTime.Now;
        }

        // 2. PribadiPPID
        var pribadiPPID = await db.PribadiPPID.FirstOrDefaultAsync(p => p.PribadiID == pribadi.PribadiID);
        if (pribadiPPID == null)
        {
            pribadiPPID = new PribadiPPID
            {
                PribadiID    = pribadi.PribadiID,
                ProvinsiID   = vm.ProvinsiID,
                NamaProvinsi = vm.NamaProvinsi,
                Lembaga      = vm.Lembaga,
                Fakultas     = vm.Fakultas,
                Jurusan      = vm.Jurusan,
                Pekerjaan    = vm.Pekerjaan,
                CreatedAt    = DateTime.Now,
                UpdatedAt    = DateTime.Now
            };
            db.PribadiPPID.Add(pribadiPPID);
        }
        else
        {
            pribadiPPID.Lembaga   = vm.Lembaga;
            pribadiPPID.Fakultas  = vm.Fakultas;
            pribadiPPID.Jurusan   = vm.Jurusan;
            pribadiPPID.UpdatedAt = DateTime.Now;
        }

        // 3. PermohonanPPID
        var noPerm = await db.GenerateNoPermohonan();
        var permohonan = new PermohonanPPID
        {
            PribadiID          = pribadi.PribadiID,
            NoPermohonan       = noPerm,
            KategoriPemohon    = vm.Kategori,
            NoSuratPermohonan  = vm.NoSuratPermohonan,
            TanggalPermohonan  = vm.TanggalPermohonan,
            JudulPenelitian    = vm.JudulPenelitian,
            LatarBelakang      = vm.LatarBelakang,
            TujuanPermohonan   = vm.TujuanPermohonan,
            IsObservasi        = vm.IsObservasi,
            IsWawancara        = vm.IsWawancara,
            IsPermintaanData   = vm.IsPermintaanData,
            BidangID           = string.IsNullOrEmpty(vm.BidangID) ? null : Guid.Parse(vm.BidangID),
            NamaBidang         = vm.NamaBidang,
            StatusPPIDID       = StatusId.TerdaftarSistem,
            CratedAt           = DateTime.Now,
            UpdatedAt          = DateTime.Now
        };
        db.PermohonanPPID.Add(permohonan);
        await db.SaveChangesAsync();

        // 4. Detail keperluan
        if (vm.IsObservasi && !string.IsNullOrEmpty(vm.DetailObservasi))
            db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail { PermohonanPPIDID = permohonan.PermohonanPPIDID, KeperluanID = KeperluanId.Observasi,      DetailKeperluan = vm.DetailObservasi,      CreatedAt = DateTime.Now });

        if (vm.IsPermintaanData && !string.IsNullOrEmpty(vm.DetailPermintaanData))
            db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail { PermohonanPPIDID = permohonan.PermohonanPPIDID, KeperluanID = KeperluanId.PermintaanData, DetailKeperluan = vm.DetailPermintaanData, CreatedAt = DateTime.Now });

        if (vm.IsWawancara && !string.IsNullOrEmpty(vm.DetailWawancara))
            db.PermohonanPPIDDetail.Add(new PermohonanPPIDDetail { PermohonanPPIDID = permohonan.PermohonanPPIDID, KeperluanID = KeperluanId.Wawancara,      DetailKeperluan = vm.DetailWawancara,      CreatedAt = DateTime.Now });

        // 5. Upload dokumen
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileKTP,            JenisDokumenId.KTP,           "KTP");
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan");
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileProposal,        JenisDokumenId.Proposal,       "Proposal");
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris,     JenisDokumenId.AktaNotaris,    "Akta Notaris");

        await db.SaveChangesAsync();

        TempData["Success"] = $"Permohonan berhasil didaftarkan dengan nomor <strong>{noPerm}</strong>";
        return RedirectToAction("InputIdentifikasi", new { id = permohonan.PermohonanPPIDID });
    }

    // ── INPUT IDENTIFIKASI AWAL ───────────────────────────────────────────────
    [HttpGet("input-identifikasi/{id}")]
    public async Task<IActionResult> InputIdentifikasi(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi).FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(p);
    }

    [HttpPost("input-identifikasi/{id}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> InputIdentifikasiPost(Guid id)
    {
        var p = await db.PermohonanPPID.FindAsync(id);
        if (p == null) return NotFound();
        p.StatusPPIDID = StatusId.IdentifikasiAwal;
        p.UpdatedAt    = DateTime.Now;
        await db.SaveChangesAsync();
        TempData["Success"] = "Identifikasi awal berhasil diinput. Cetak dokumen dan minta pemohon menandatangani.";
        return RedirectToAction("UploadTTD", new { id });
    }

    // ── UPLOAD TTD ────────────────────────────────────────────────────────────
    [HttpGet("upload-ttd/{id}")]
    public async Task<IActionResult> UploadTTD(Guid id)
    {
        var p = await db.PermohonanPPID.Include(x => x.Pribadi).FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(new UploadTTDVm { PermohonanPPIDID = id, NoPermohonan = p.NoPermohonan!, NamaPemohon = p.Pribadi?.Nama ?? "" });
    }

    [HttpPost("upload-ttd"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTTDPost(UploadTTDVm vm)
    {
        if (!ModelState.IsValid) return View("UploadTTD", vm);
        await UploadDokumen(vm.PermohonanPPIDID, vm.FileDokumenTTD, JenisDokumenId.IdentifikasiSigned, "Identifikasi TTD");
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.MenungguSuratIzin; p.UpdatedAt = DateTime.Now; }
        await db.SaveChangesAsync();
        TempData["Success"] = "Dokumen berhasil diupload. Permohonan diteruskan ke Kepegawaian.";
        return RedirectToAction("Index");
    }

    // ── DETAIL ────────────────────────────────────────────────────────────────
    [HttpGet("detail/{id}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi).ThenInclude(pr => pr!.PribadiPPID)
            .Include(x => x.Status)
            .Include(x => x.Detail).ThenInclude(d => d.Keperluan)
            .Include(x => x.Dokumen).ThenInclude(d => d.JenisDokumen)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);
        if (p == null) return NotFound();
        return View(p);
    }

    // ── HELPER: Upload file ───────────────────────────────────────────────────
    private async Task UploadDokumen(Guid permohonanId, IFormFile? file, int jenisDokId, string nama)
    {
        if (file == null || file.Length == 0) return;
        var uploadDir = Path.Combine(env.WebRootPath, "uploads", permohonanId.ToString());
        Directory.CreateDirectory(uploadDir);
        var fileName = $"{jenisDokId}_{Path.GetFileName(file.FileName)}";
        var filePath = Path.Combine(uploadDir, fileName);
        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID     = permohonanId,
            NamaDokumenPPID      = nama,
            UploadDokumenPPID    = $"/uploads/{permohonanId}/{fileName}",
            JenisDokumenPPIDID   = jenisDokId,
            NamaJenisDokumenPPID = nama,
            CreatedAt            = DateTime.Now
        });
    }
}
