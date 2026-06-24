using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using PermintaanData.Helpers;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>
/// Pengajuan online mandiri oleh pemohon (Mahasiswa).
/// LSM diarahkan ke portal PPID Jakarta.
/// </summary>
[Route("daftar-online")]
public class PublicRegistrasiController(AppDbContext db, IWebHostEnvironment env)
    : LoketBaseController(db, env)
{
    private const string OperatorOnline = "Pemohon (Online)";

    [HttpGet("")]
    public IActionResult Index(string? kategori)
    {
        if (string.Equals(kategori, "LSM", StringComparison.OrdinalIgnoreCase)
         || string.Equals(kategori, "Organisasi", StringComparison.OrdinalIgnoreCase))
            return Redirect(PermohonanRules.PpidLsmOnlineUrl);

        var vm = new DaftarPemohonVm
        {
            Kategori              = "Mahasiswa",
            LoketJenis            = LoketJenis.Kepegawaian,
            IsOnlineRegistration  = true,
            IsPermintaanData      = true,
        };

        ViewData["IsPublicOnline"] = true;
        ViewData["Title"]          = "Pendaftaran Online — Mahasiswa";
        return View("~/Views/PetugasLoket/DaftarPemohon.cshtml", vm);
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexPost(DaftarPemohonVm vm)
    {
        if (PermohonanRules.IsLsm(vm.Kategori, vm.LoketJenis))
            return Redirect(PermohonanRules.PpidLsmOnlineUrl);

        vm.Kategori              = "Mahasiswa";
        vm.LoketJenis            = LoketJenis.Kepegawaian;
        vm.IsOnlineRegistration  = true;

        if (!vm.IsObservasi && !vm.IsPermintaanData && !vm.IsWawancara)
            ModelState.AddModelError(string.Empty,
                "Pilih minimal satu keperluan: Observasi, Permintaan Data, atau Wawancara.");

        if (!ModelState.IsValid)
        {
            ViewData["IsPublicOnline"] = true;
            ViewData["Title"]          = "Pendaftaran Online — Mahasiswa";
            return View("~/Views/PetugasLoket/DaftarPemohon.cshtml", vm);
        }

        Guid? bidangGuid = null;
        if (!string.IsNullOrEmpty(vm.BidangID) && Guid.TryParse(vm.BidangID, out var parsed))
            bidangGuid = parsed;

        Guid lastId = Guid.Empty;
        string noPerm = string.Empty;

        var strategy = db.Database.CreateExecutionStrategy();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ppid_online_{Guid.NewGuid()}");
        var movers = new List<(string Temp, string Final)>();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                movers.Clear();
                var (generatedNoPerm, nextSeq) = await db.GenerateNoPermohonan(LoketJenis.Kepegawaian);

                await using var tx = await db.Database.BeginTransactionAsync();
                try
                {
                    var now = DateTime.UtcNow;

                    var pribadi = await db.Pribadi.FirstOrDefaultAsync(p => p.NIK == vm.NIK);
                    if (pribadi == null)
                    {
                        pribadi = new Pribadi
                        {
                            NIK = vm.NIK, Nama = vm.Nama, Email = vm.Email, Telepon = vm.Telepon,
                            Alamat = vm.Alamat, RT = vm.RT, RW = vm.RW,
                            KelurahanID = vm.KelurahanID, KecamatanID = vm.KecamatanID,
                            KabupatenID = vm.KabupatenID,
                            NamaKelurahan = vm.NamaKelurahan, NamaKecamatan = vm.NamaKecamatan,
                            NamaKabupaten = vm.NamaKabupaten,
                            CreatedAt = now, UpdatedAt = now
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
                            ProvinsiID = vm.ProvinsiID, NamaProvinsi = vm.NamaProvinsi,
                            NIM = vm.NIM, Lembaga = vm.Lembaga, Fakultas = vm.Fakultas,
                            Jurusan = vm.Jurusan, Pekerjaan = vm.Pekerjaan,
                            CreatedAt = now, UpdatedAt = now
                        });
                    }
                    else
                    {
                        pribadiPPID.NIM = vm.NIM;
                        pribadiPPID.Lembaga = vm.Lembaga;
                        pribadiPPID.Fakultas = vm.Fakultas;
                        pribadiPPID.Jurusan = vm.Jurusan;
                        pribadiPPID.ProvinsiID = vm.ProvinsiID;
                        pribadiPPID.NamaProvinsi = vm.NamaProvinsi;
                        pribadiPPID.UpdatedAt = now;
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
                        SumberRegistrasi  = SumberRegistrasi.Online,
                        Sequance          = nextSeq,
                        CratedAt          = now,
                        UpdatedAt         = now
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
                        $"[Online] Permohonan didaftarkan pemohon. Keperluan: " +
                        $"{(vm.IsObservasi ? "Observasi " : "")}" +
                        $"{(vm.IsPermintaanData ? "Data " : "")}" +
                        $"{(vm.IsWawancara ? "Wawancara" : "")}",
                        OperatorOnline);

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

        return RedirectToAction(nameof(Sukses), new { no = noPerm });
    }

    [HttpGet("sukses")]
    public IActionResult Sukses(string no)
    {
        ViewData["Title"] = "Pendaftaran Berhasil";
        ViewData["NoPermohonan"] = no;
        return View("~/Views/Public/Sukses.cshtml");
    }
}
