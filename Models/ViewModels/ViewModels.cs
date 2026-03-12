using System.ComponentModel.DataAnnotations;
using PermintaanData.Models;

namespace PermintaanData.Models.ViewModels;

// ── PUBLIC: Lacak ─────────────────────────────────────────────────────────────

public class LacakViewModel
{
    [Required(ErrorMessage = "Nomor permohonan wajib diisi")]
    public string NoPermohonan { get; set; } = string.Empty;
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

// ── PETUGAS LOKET: Identifikasi ───────────────────────────────────────────────

public class IdentifikasiPemohonVm
{
    [Required(ErrorMessage = "Kategori wajib dipilih")]
    public string Kategori { get; set; } = string.Empty; // "Mahasiswa" | "LSM"

    /// <summary>Ditentukan otomatis dari kategori: Mahasiswa→Kepegawaian, LSM→Umum</summary>
    public string LoketJenis { get; set; } = string.Empty;
}

// ── PETUGAS LOKET: Daftar Pemohon (FORMULIR UTAMA) ────────────────────────────

public class DaftarPemohonVm
{
    // ── Meta ──────────────────────────────────────────────────────────────────
    public string Kategori { get; set; } = "Mahasiswa";
    public string LoketJenis { get; set; } = Models.LoketJenis.Kepegawaian;

    // ── Data Pribadi ─────────────────────────────────────────────────────────
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

    // ── Alamat ────────────────────────────────────────────────────────────────
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

    [Display(Name = "RT")]
    public string? RT { get; set; }

    [Display(Name = "RW")]
    public string? RW { get; set; }

    [Display(Name = "Alamat Lengkap")]
    public string? Alamat { get; set; }

    // ── Data Lembaga/Institusi ────────────────────────────────────────────────
    [Display(Name = "Lembaga / Universitas")]
    public string? Lembaga { get; set; }

    [Display(Name = "Fakultas")]
    public string? Fakultas { get; set; }

    [Display(Name = "Program Studi / Jurusan")]
    public string? Jurusan { get; set; }

    [Display(Name = "Pekerjaan")]
    public string? Pekerjaan { get; set; }

    // ── Data Permohonan ───────────────────────────────────────────────────────
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

    // ── Keperluan (checkboxes — dapat lebih dari 1) ───────────────────────────
    [Display(Name = "Observasi")]
    public bool IsObservasi { get; set; }

    [Display(Name = "Permintaan Data")]
    public bool IsPermintaanData { get; set; }

    /// <summary>
    /// Wawancara/Interview → langsung ke produsen data.
    /// Tersedia di kedua loket, namun routing-nya berbeda setelah disposisi.
    /// </summary>
    [Display(Name = "Wawancara / Interview")]
    public bool IsWawancara { get; set; }

    [Display(Name = "Deskripsi Observasi")]
    public string? DetailObservasi { get; set; }

    [Display(Name = "Data yang Diperlukan")]
    public string? DetailPermintaanData { get; set; }

    [Display(Name = "Topik / Materi Wawancara")]
    public string? DetailWawancara { get; set; }

    // ── Bidang ────────────────────────────────────────────────────────────────
    [Display(Name = "Unit Kerja / Bidang")]
    public string? BidangID { get; set; }
    public string? NamaBidang { get; set; }

    // ── Dokumen Upload ────────────────────────────────────────────────────────
    [Display(Name = "KTP")]
    public IFormFile? FileKTP { get; set; }

    [Display(Name = "Surat Permohonan")]
    public IFormFile? FileSuratPermohonan { get; set; }

    [Display(Name = "Proposal Penelitian")]
    public IFormFile? FileProposal { get; set; }

    [Display(Name = "Akta Notaris (LSM)")]
    public IFormFile? FileAktaNotaris { get; set; }
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

// ── KEPEGAWAIAN: Surat Izin & Disposisi ───────────────────────────────────────

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

    // ── Keperluan (di-verify ulang oleh Kepegawaian) ─────────────────────────
    public bool IsObservasi { get; set; }
    public bool IsPermintaanData { get; set; }
    public bool IsWawancara { get; set; }

    // ── Routing Disposisi ─────────────────────────────────────────────────────
    /// <summary>
    /// Hanya aktif ketika HasPermintaanData || HasObservasi.
    /// Pilihan: "PSMDI" | "BidangTerkait"
    /// </summary>
    [Display(Name = "Disposisi Ke (KDI)")]
    public string DisposisiKe { get; set; } = "PSMDI";

    [Display(Name = "Nama Bidang Terkait")]
    public string? NamaBidangTerkait { get; set; }

    [Display(Name = "Catatan Disposisi")]
    public string? CatatanDisposisi { get; set; }

    /// <summary>
    /// Untuk keperluan Wawancara-only: nama unit/bidang yang menjadi
    /// produsen data dan akan melayani wawancara langsung.
    /// </summary>
    [Display(Name = "Unit Produsen Data (Wawancara)")]
    public string? NamaProdusenData { get; set; }

    // ── Computed helpers ──────────────────────────────────────────────────────
    /// <summary>Ada keperluan Permintaan Data atau Observasi → routing ke KDI.</summary>
    public bool HasKdiRoute => IsPermintaanData || IsObservasi;

    /// <summary>Wawancara tanpa keperluan lain → routing langsung ke Produsen Data.</summary>
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

    /// <summary>
    /// TRUE hanya jika IsObservasi = true.
    /// Wawancara TIDAK termasuk di sini — wawancara sudah dirouting ke Produsen Data
    /// atau akan dijadwalkan secara terpisah oleh KDI.
    /// </summary>
    public bool PerluObservasi { get; set; }

    /// <summary>
    /// TRUE jika IsWawancara = true DAN permohonan ini diproses oleh KDI
    /// (kombinasi wawancara + data/observasi).
    /// </summary>
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

// ── PRODUSEN DATA: Jadwal Wawancara ──────────────────────────────────────────

/// <summary>
/// Digunakan oleh Produsen Data untuk menjadwalkan wawancara langsung
/// dengan pemohon (hanya untuk permohonan wawancara-only).
/// </summary>
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
}

// ── KDI: Upload Data ──────────────────────────────────────────────────────────

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
