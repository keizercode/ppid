using System.ComponentModel.DataAnnotations;
using PermintaanData.Models;

namespace PermintaanData.Models.ViewModels;

// ── PUBLIC: Lacak ─────────────────────────────────────────────────────────────

public class LacakViewModel
{
    [Required(ErrorMessage = "Nomor permohonan wajib diisi")]
    [Display(Name = "Nomor Permohonan")]
    public string NoPermohonan { get; set; } = string.Empty;

    /// <summary>
    /// Kode verifikasi 6-karakter yang dicetak di formulir pemohon.
    /// Wajib diisi bersamaan dengan NoPermohonan untuk mencegah enumerasi.
    /// </summary>
    [Required(ErrorMessage = "Kode verifikasi wajib diisi")]
    [Display(Name = "Kode Verifikasi")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Kode verifikasi harus 6 karakter")]
    public string TokenLacak { get; set; } = string.Empty;
}

public class DetailLacakViewModel
{
    public PermohonanPPID Permohonan { get; set; } = null!;
    public Pribadi Pribadi { get; set; } = null!;
    public PribadiPPID? PribadiPPID { get; set; }
    public List<PermohonanPPIDDetail> Detail { get; set; } = new();
    public List<JadwalPPID> Jadwal { get; set; } = new();
    public List<RiwayatStatusVm> Riwayat { get; set; } = new();
}

public class RiwayatStatusVm
{
    public int StatusId { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool Selesai { get; set; }
    public bool AktifSekarang { get; set; }
}

// ── AUTH ──────────────────────────────────────────────────────────────────────

public class LoginVm
{
    [Required(ErrorMessage = "Username wajib diisi")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ingat saya")]
    public bool RememberMe { get; set; }
}

// ── PETUGAS LOKET: Identifikasi ───────────────────────────────────────────────

public class IdentifikasiPemohonVm
{
    [Required(ErrorMessage = "Kategori wajib dipilih")]
    public string Kategori { get; set; } = string.Empty;
    public string LoketJenis { get; set; } = string.Empty;
}

// ── PETUGAS LOKET: Daftar Pemohon ─────────────────────────────────────────────

public class DaftarPemohonVm
{
    public string Kategori { get; set; } = "Mahasiswa";
    public string LoketJenis { get; set; } = Models.LoketJenis.Kepegawaian;

    [Required(ErrorMessage = "NIK wajib diisi")]
    [StringLength(16, MinimumLength = 16, ErrorMessage = "NIK harus 16 digit")]
    [Display(Name = "NIK")]
    public string NIK { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nama wajib diisi")]
    [Display(Name = "Nama Lengkap")]
    public string Nama { get; set; } = string.Empty;

    [Display(Name = "No. Telepon")]
    public string? Telepon { get; set; }

    [Display(Name = "Email")]
    [EmailAddress(ErrorMessage = "Format email tidak valid")]
    public string? Email { get; set; }

    [Display(Name = "Provinsi")]
    public string? ProvinsiID { get; set; }
    public string? NamaProvinsi { get; set; }

    [Display(Name = "Kabupaten / Kota")]
    public string? KabupatenID { get; set; }
    public string? NamaKabupaten { get; set; }

    [Display(Name = "Kecamatan")]
    public string? KecamatanID { get; set; }
    public string? NamaKecamatan { get; set; }

    [Display(Name = "Kelurahan")]
    public string? KelurahanID { get; set; }
    public string? NamaKelurahan { get; set; }

    [Display(Name = "RT")] public string? RT { get; set; }
    [Display(Name = "RW")] public string? RW { get; set; }
    [Display(Name = "Alamat Lengkap")] public string? Alamat { get; set; }

    [Display(Name = "Lembaga / Universitas")] public string? Lembaga { get; set; }
    [Display(Name = "Fakultas")] public string? Fakultas { get; set; }
    [Display(Name = "Program Studi / Jurusan")] public string? Jurusan { get; set; }
    [Display(Name = "Pekerjaan")] public string? Pekerjaan { get; set; }

    [Required(ErrorMessage = "No. Surat Permohonan wajib diisi")]
    [Display(Name = "No. Surat Permohonan")]
    public string NoSuratPermohonan { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tanggal permohonan wajib diisi")]
    [Display(Name = "Tanggal Permohonan")]
    public DateOnly TanggalPermohonan { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required(ErrorMessage = "Judul penelitian wajib diisi")]
    [Display(Name = "Judul Penelitian")]
    public string JudulPenelitian { get; set; } = string.Empty;

    [Required(ErrorMessage = "Latar belakang wajib diisi")]
    [Display(Name = "Latar Belakang Penelitian")]
    public string LatarBelakang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tujuan permohonan wajib diisi")]
    [Display(Name = "Tujuan Permohonan")]
    public string TujuanPermohonan { get; set; } = string.Empty;

    [Display(Name = "Observasi")] public bool IsObservasi { get; set; }
    [Display(Name = "Permintaan Data")] public bool IsPermintaanData { get; set; }
    [Display(Name = "Wawancara / Interview")] public bool IsWawancara { get; set; }

    [Display(Name = "Deskripsi Observasi")] public string? DetailObservasi { get; set; }
    [Display(Name = "Data yang Diperlukan")] public string? DetailPermintaanData { get; set; }
    [Display(Name = "Topik / Materi Wawancara")] public string? DetailWawancara { get; set; }

    [Display(Name = "Unit Kerja / Bidang")]
    public string? BidangID { get; set; }
    public string? NamaBidang { get; set; }

    [Display(Name = "KTP")] public IFormFile? FileKTP { get; set; }
    [Display(Name = "Surat Permohonan")] public IFormFile? FileSuratPermohonan { get; set; }
    [Display(Name = "Proposal Penelitian")] public IFormFile? FileProposal { get; set; }
    [Display(Name = "Akta Notaris (LSM)")] public IFormFile? FileAktaNotaris { get; set; }
}

// ── PETUGAS LOKET: Upload TTD ─────────────────────────────────────────────────

public class UploadTTDVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;
    public string LoketJenis { get; set; } = Models.LoketJenis.Kepegawaian;

    [Required(ErrorMessage = "File wajib diupload")]
    [Display(Name = "Dokumen Identifikasi (Sudah TTD Pemohon)")]
    public IFormFile? FileDokumenTTD { get; set; }
}

// ── KEPEGAWAIAN: Surat Izin ───────────────────────────────────────────────────

public class SuratIzinVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;
    public string Kategori { get; set; } = string.Empty;
    public string JudulPenelitian { get; set; } = string.Empty;

    [Required(ErrorMessage = "No. surat wajib diisi")]
    [Display(Name = "No. Surat Izin")]
    public string NoSuratIzin { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tanggal surat wajib diisi")]
    [Display(Name = "Tanggal Surat")]
    public DateOnly TanggalSurat { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Catatan")]
    public string? Catatan { get; set; }

    [Display(Name = "File Surat Izin (PDF)")]
    public IFormFile? FileSuratIzin { get; set; }

    public bool IsObservasi { get; set; }
    public bool IsPermintaanData { get; set; }
    public bool IsWawancara { get; set; }

    [Display(Name = "Disposisi Ke (KDI)")]
    public string DisposisiKe { get; set; } = "PSMDI";

    [Display(Name = "Nama Bidang Terkait")]
    public string? NamaBidangTerkait { get; set; }

    [Display(Name = "Catatan Disposisi")]
    public string? CatatanDisposisi { get; set; }

    [Display(Name = "Unit Produsen Data (Wawancara)")]
    public string? NamaProdusenData { get; set; }

    public bool HasKdiRoute => IsPermintaanData || IsObservasi;
    public bool IsWawancaraOnly => IsWawancara && !IsPermintaanData && !IsObservasi;
}

// ── KDI: Terima Disposisi ─────────────────────────────────────────────────────

public class TerimaDisposisiVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;
    public string JudulPenelitian { get; set; } = string.Empty;
    public string LatarBelakang { get; set; } = string.Empty;
    public string? CatatanDisposisi { get; set; }
    public bool PerluObservasi { get; set; }
    public bool PerluWawancara { get; set; }
    public string? Catatan { get; set; }
}

// ── KDI: Jadwal Observasi ─────────────────────────────────────────────────────

public class JadwalObservasiVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Tanggal")]
    public DateOnly TanggalObservasi { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    [Required]
    [Display(Name = "Jam")]
    public TimeOnly WaktuObservasi { get; set; } = new TimeOnly(9, 0);

    [Required]
    [Display(Name = "Nama PIC")]
    public string NamaPIC { get; set; } = string.Empty;
}

// ── KDI: Selesai Observasi ────────────────────────────────────────────────────

public class SelesaiObservasiVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;
    public string JudulPenelitian { get; set; } = string.Empty;

    [Display(Name = "Catatan Hasil Observasi")]
    public string? Catatan { get; set; }
}

// ── Jadwal Wawancara ──────────────────────────────────────────────────────────

public class JadwalWawancaraVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;
    public string JudulPenelitian { get; set; } = string.Empty;
    public string DetailWawancara { get; set; } = string.Empty;
    public string? NamaProdusenData { get; set; }

    [Required]
    [Display(Name = "Tanggal Wawancara")]
    public DateOnly TanggalWawancara { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    [Required]
    [Display(Name = "Jam Wawancara")]
    public TimeOnly WaktuWawancara { get; set; } = new TimeOnly(9, 0);

    [Required]
    [Display(Name = "Nama Narasumber / PIC")]
    public string NamaPIC { get; set; } = string.Empty;

    [Display(Name = "Lokasi / Platform")]
    public string? Lokasi { get; set; }
    /// <summary>True jika jadwal sudah dibuat oleh KDI — form tampil read-only.</summary>
    public bool JadwalSudahAda { get; set; }
}

// ── KDI / Produsen Data: Upload Data ─────────────────────────────────────────

public class UploadDataVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;
    public string NamaPemohon { get; set; } = string.Empty;
    public string JudulPenelitian { get; set; } = string.Empty;

    [Required(ErrorMessage = "File wajib diupload")]
    [Display(Name = "File Data / Dokumen Hasil")]
    public IFormFile? FileData { get; set; }

    [Display(Name = "Catatan untuk Pemohon")]
    public string? Catatan { get; set; }
}

// ── KUESIONER ─────────────────────────────────────────────────────────────────

public class KuesionerVm
{
    public Guid PermohonanPPIDID { get; set; }
    public string NoPermohonan { get; set; } = string.Empty;

    [Required]
    [Range(1, 5)]
    public int NilaiKepuasan { get; set; }

    public string? Catatan { get; set; }
}
