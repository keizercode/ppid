using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

[Route("kepegawaian")]
[Authorize(Roles = $"{AppRoles.Kepegawaian},{AppRoles.Admin}")]
public class KepegawaianController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    private string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    private string CurrentUser => User.Identity?.Name ?? AppRoles.Kepegawaian;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await db.PermohonanPPID
            .Include(p => p.Pribadi)
            .Include(p => p.Status)
            .Where(p => p.StatusPPIDID == StatusId.MenungguSuratIzin)
            .OrderByDescending(p => p.CratedAt)
            .ToListAsync();

        return View(list);
    }

    [HttpGet("surat-izin/{id:guid}")]
    public async Task<IActionResult> SuratIzin(Guid id)
    {
        var p = await db.PermohonanPPID
            .Include(x => x.Pribadi)
            .Include(x => x.Detail)
            .FirstOrDefaultAsync(x => x.PermohonanPPIDID == id);

        if (p == null) return NotFound();

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
        });
    }

    [HttpPost("surat-izin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SuratIzinPost(SuratIzinVm vm)
    {
        if (!ModelState.IsValid) return View("SuratIzin", vm);

        var now = DateTime.UtcNow;

        if (vm.FileSuratIzin?.Length > 0)
        {
            var dir = Path.Combine(UploadsRoot, vm.PermohonanPPIDID.ToString());
            Directory.CreateDirectory(dir);
            var fn = $"surat_izin_{vm.FileSuratIzin.FileName}";
            await using var s = new FileStream(Path.Combine(dir, fn), FileMode.Create);
            await vm.FileSuratIzin.CopyToAsync(s);

            db.DokumenPPID.Add(new DokumenPPID
            {
                PermohonanPPIDID   = vm.PermohonanPPIDID,
                NamaDokumenPPID    = "Surat Izin",
                UploadDokumenPPID  = $"/uploads/{vm.PermohonanPPIDID}/{fn}",
                JenisDokumenPPIDID = JenisDokumenId.SuratIzin,
                CreatedAt          = now
            });
        }

        var p = await db.PermohonanPPID.FindAsync(vm.PermohonanPPIDID);
        if (p == null) return NotFound();

        var statusLama = p.StatusPPIDID;

        p.NoSuratPermohonan = vm.NoSuratIzin;
        p.UpdatedAt         = now;
        p.IsObservasi       = vm.IsObservasi;
        p.IsPermintaanData  = vm.IsPermintaanData;
        p.IsWawancara       = vm.IsWawancara;

        if (vm.IsWawancaraOnly)
        {
            p.StatusPPIDID     = StatusId.WawancaraDijadwalkan;
            p.NamaProdusenData = vm.NamaProdusenData ?? vm.NamaBidangTerkait;
        }
        else
        {
            p.StatusPPIDID = StatusId.Didisposisi;
            p.NamaBidang   = vm.DisposisiKe == "BidangTerkait" && !string.IsNullOrEmpty(vm.NamaBidangTerkait)
                ? vm.NamaBidangTerkait
                : null;
        }

        var tujuan = vm.IsWawancaraOnly
            ? $"Surat izin {vm.NoSuratIzin} diterbitkan, diteruskan ke Produsen Data"
            : $"Surat izin {vm.NoSuratIzin} diterbitkan, didisposisi ke {p.NamaBidang ?? "PSMDI"}";

        db.AddAuditLog(vm.PermohonanPPIDID, statusLama, p.StatusPPIDID!.Value, tujuan, CurrentUser);

        await db.SaveChangesAsync();

        var keterangan = vm.IsWawancaraOnly
            ? "dan diteruskan ke Produsen Data untuk penjadwalan wawancara"
            : $"dan didisposisi ke {(p.NamaBidang == null ? "PSMDI" : p.NamaBidang)}";

        TempData["Success"] = $"Surat izin <strong>{vm.NoSuratIzin}</strong> diterbitkan {keterangan}.";
        return RedirectToAction(nameof(Index));
    }
}
