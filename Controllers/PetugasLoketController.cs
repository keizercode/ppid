using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("petugas-loket")]
public class PetugasLoketController(AppDbContext db, IWebHostEnvironment env) : Controller
{
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

    [HttpGet("daftar")]
    public IActionResult DaftarPemohon(string kategori)
        => View(new DaftarPemohonVm { Kategori = kategori });

    [HttpPost("daftar"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DaftarPemohonPost(DaftarPemohonVm vm)
    {
        if (!ModelState.IsValid) return View("DaftarPemohon", vm);

        var now = DateTime.UtcNow;

        // 1. Cek NIK
        var pribadi = await db.Pribadi.FirstOrDefaultAsync(p => p.NIK == vm.NIK);
        if (pribadi == null)
        {
            pribadi = new Pribadi
            {
                NIK           = vm.NIK,
                Nama          = vm.Nama,
                Email         = vm.Email,
                Telepon       = vm.Telepon,
                Alamat        = vm.Alamat,
                RT            = vm.RT,
                RW            = vm.RW,
                KelurahanID   = vm.KelurahanID,
                KecamatanID   = vm.KecamatanID,
                KabupatenID   = vm.KabupatenID,
                NamaKelurahan = vm.NamaKelurahan,
                NamaKecamatan = vm.NamaKecamatan,
                NamaKabupaten = vm.NamaKabupaten,
                CreatedAt     = now,
                UpdatedAt     = now
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

        // Simpan Pribadi dulu agar PribadiID tersedia untuk FK
        await db.SaveChangesAsync();

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
                CreatedAt    = now,
                UpdatedAt    = now
            };
            db.PribadiPPID.Add(pribadiPPID);
        }
        else
        {
            pribadiPPID.Lembaga      = vm.Lembaga;
            pribadiPPID.Fakultas     = vm.Fakultas;
            pribadiPPID.Jurusan      = vm.Jurusan;
            pribadiPPID.Pekerjaan    = vm.Pekerjaan;
            pribadiPPID.ProvinsiID   = vm.ProvinsiID;
            pribadiPPID.NamaProvinsi = vm.NamaProvinsi;
            pribadiPPID.UpdatedAt    = now;
        }

        // 3. PermohonanPPID
        var noPerm = await db.GenerateNoPermohonan();
        var permohonan = new PermohonanPPID
        {
            PribadiID         = pribadi.PribadiID,
            NoPermohonan      = noPerm,
            KategoriPemohon   = vm.Kategori,
            NoSuratPermohonan = vm.NoSuratPermohonan,
            TanggalPermohonan = vm.TanggalPermohonan,
            JudulPenelitian   = vm.JudulPenelitian,
            LatarBelakang     = vm.LatarBelakang,
            TujuanPermohonan  = vm.TujuanPermohonan,
            IsObservasi       = vm.IsObservasi,
            IsWawancara       = vm.IsWawancara,
            IsPermintaanData  = vm.IsPermintaanData,
            BidangID          = string.IsNullOrEmpty(vm.BidangID) ? null : Guid.Parse(vm.BidangID),
            NamaBidang        = vm.NamaBidang,
            StatusPPIDID      = StatusId.TerdaftarSistem,
            CratedAt          = now,
            UpdatedAt         = now
        };
        db.PermohonanPPID.Add(permohonan);
        await db.SaveChangesAsync();

        // 4. Detail keperluan
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

        // 5. Upload dokumen
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileKTP,             JenisDokumenId.KTP,             "KTP",              now);
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileSuratPermohonan, JenisDokumenId.SuratPermohonan, "Surat Permohonan", now);
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileProposal,        JenisDokumenId.Proposal,        "Proposal",         now);
        await UploadDokumen(permohonan.PermohonanPPIDID, vm.FileAktaNotaris,     JenisDokumenId.AktaNotaris,     "Akta Notaris",     now);

        await db.SaveChangesAsync();

        TempData["Success"] = $"Permohonan berhasil didaftarkan dengan nomor <strong>{noPerm}</strong>";
        return RedirectToAction("InputIdentifikasi", new { id = permohonan.PermohonanPPIDID });
    }

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
        p.StatusPPIDID = StatusId.IdentifikasiAwal;
        p.UpdatedAt    = DateTime.UtcNow;
        await db.SaveChangesAsync();
        TempData["Success"] = "Identifikasi awal berhasil diinput. Cetak formulir dan minta pemohon menandatangani.";
        return RedirectToAction("CetakIdentifikasi", new { id });
    }

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
        await UploadDokumen(vm.PermohonanPPIDID, vm.FileDokumenTTD, JenisDokumenId.IdentifikasiSigned, "Identifikasi TTD", DateTime.UtcNow);
        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p != null) { p.StatusPPIDID = StatusId.MenungguSuratIzin; p.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        TempData["Success"] = "Dokumen berhasil diupload. Permohonan diteruskan ke Kepegawaian.";
        return RedirectToAction("Index");
    }

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

    private async Task UploadDokumen(Guid permohonanId, IFormFile? file, int jenisDokId, string nama, DateTime now)
    {
        if (file == null || file.Length == 0) return;
        var uploadDir = Path.Combine(env.WebRootPath, "uploads", permohonanId.ToString());
        Directory.CreateDirectory(uploadDir);
        var fileName = $"{jenisDokId}_{Path.GetFileName(file.FileName)}";
        using var stream = new FileStream(Path.Combine(uploadDir, fileName), FileMode.Create);
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
}
