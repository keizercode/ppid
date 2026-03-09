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
    public PermohonanPPID Permohonan    { get; set; } = null!;
    public Pribadi        Pribadi       { get; set; } = null!;
    public PribadiPPID?   PribadiPPID  { get; set; }
    public List<PermohonanPPIDDetail> Detail  { get; set; } = new();
    public List<JadwalPPID>           Jadwal  { get; set; } = new();
    public List<RiwayatStatusVm>      Riwayat { get; set; } = new();
}

public class RiwayatStatusVm
{
    public int     StatusId      { get; set; }
    public string  Label         { get; set; } = string.Empty;
    public bool    Selesai       { get; set; }
    public bool    AktifSekarang { get; set; }
}

// ── PETUGAS LOKET: Identifikasi ───────────────────────────────────────────────

public class IdentifikasiPemohonVm
{
    [Required(ErrorMessage = "Kategori wajib dipilih")]
    public string Kategori { get; set; } = string.Empty; // "Mahasiswa" | "LSM"
}

// ── PETUGAS LOKET: Daftar Pemohon (FORMULIR UTAMA) ────────────────────────────

public class DaftarPemohonVm
{
    // ── Meta ──────────────────────────────────────────────────────────────────
    public string Kategori { get; set; } = "Mahasiswa";

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
    public string? ProvinsiID   { get; set; }
    public string? NamaProvinsi { get; set; }

    [Display(Name = "Kabupaten / Kota")]
    public string? KabupatenID   { get; set; }
    public string? NamaKabupaten { get; set; }

    [Display(Name = "Kecamatan")]
    public string? KecamatanID   { get; set; }
    public string? NamaKecamatan { get; set; }

    [Display(Name = "Kelurahan")]
    public string? KelurahanID   { get; set; }
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

    // ── Keperluan (checkboxes) ────────────────────────────────────────────────
    [Display(Name = "Observasi")]
    public bool IsObservasi { get; set; }

    [Display(Name = "Permintaan Data")]
    public bool IsPermintaanData { get; set; }

    [Display(Name = "Wawancara")]
    public bool IsWawancara { get; set; }

    [Display(Name = "Deskripsi Observasi")]
    public string? DetailObservasi { get; set; }

    [Display(Name = "Data yang Diperlukan")]
    public string? DetailPermintaanData { get; set; }

    [Display(Name = "Materi Wawancara")]
    public string? DetailWawancara { get; set; }

    // ── Bidang ────────────────────────────────────────────────────────────────
    [Display(Name = "Unit Kerja / Bidang")]
    public string? BidangID   { get; set; }
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
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;

    [Required(ErrorMessage = "File wajib diupload")]
    [Display(Name = "Dokumen Identifikasi (Sudah TTD Pemohon)")]
    public IFormFile? FileDokumenTTD { get; set; }
}

// ── KEPEGAWAIAN: Surat Izin & Disposisi ───────────────────────────────────────

public class SuratIzinVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string Kategori         { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;

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

    [Required(ErrorMessage = "Disposisi ke wajib dipilih")]
    [Display(Name = "Disposisi Ke")]
    public string DisposisiKe { get; set; } = string.Empty; // "PSMDI" | "BidangTerkait"

    [Display(Name = "Nama Bidang Terkait")]
    public string? NamaBidangTerkait { get; set; }

    [Display(Name = "Catatan Disposisi")]
    public string? CatatanDisposisi { get; set; }

    [Display(Name = "Perlu Observasi / Wawancara")]
    public bool PerluObservasi { get; set; }
}

// ── KDI: Terima Disposisi ─────────────────────────────────────────────────────

public class TerimaDisposisiVm
{
    public Guid   PermohonanPPIDID  { get; set; }
    public string NoPermohonan      { get; set; } = string.Empty;
    public string NamaPemohon       { get; set; } = string.Empty;
    public string JudulPenelitian   { get; set; } = string.Empty;
    public string LatarBelakang     { get; set; } = string.Empty;
    public string? CatatanDisposisi { get; set; }
    public bool   PerluObservasi    { get; set; }
    public string? Catatan          { get; set; }
}

// ── KDI: Jadwal Observasi ─────────────────────────────────────────────────────

public class JadwalObservasiVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;

    [Required] [Display(Name = "Tanggal")]
    public DateOnly TanggalObservasi { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    [Required] [Display(Name = "Jam")]
    public TimeOnly WaktuObservasi { get; set; } = new TimeOnly(9, 0);

    [Required] [Display(Name = "Nama PIC")]
    public string NamaPIC { get; set; } = string.Empty;
}

// ── KDI: Upload Data ──────────────────────────────────────────────────────────

public class UploadDataVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;

    [Required(ErrorMessage = "File wajib diupload")]
    [Display(Name = "File Data / Dokumen Hasil")]
    public IFormFile? FileData { get; set; }

    [Display(Name = "Catatan untuk Pemohon")]
    public string? Catatan { get; set; }
}

// ── KUESIONER ─────────────────────────────────────────────────────────────────

public class KuesionerVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;

    [Required][Range(1,5)]
    public int NilaiKepuasan { get; set; }

    public string? Catatan { get; set; }
}
