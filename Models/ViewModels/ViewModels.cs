using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PermintaanData.Models;
using PermintaanData.Data;

namespace PermintaanData.Models.ViewModels;

// ── Dashboard ─────────────────────────────────────────────────────────────────

public class DashboardVm
{
    public int Total   { get; set; }
    public int Proses  { get; set; }
    public int Selesai { get; set; }

    public List<MonthlyStatRow> MonthlyStats { get; set; } = new();

    // JSON strings untuk Chart.js
    public string LabelsJson    => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Label));
    public string TotalJson     => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Total));
    public string ProsesJson    => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Proses));
    public string SelesaiJson   => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Selesai));
}

// ── PUBLIC: Lacak ─────────────────────────────────────────────────────────────

public class LacakViewModel
{
    [Required(ErrorMessage = "Nomor permohonan wajib diisi")]
    [Display(Name = "Nomor Permohonan")]
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
    public int    StatusId      { get; set; }
    public string Label         { get; set; } = string.Empty;
    public bool   Selesai       { get; set; }
    public bool   AktifSekarang { get; set; }
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

// ── PETUGAS LOKET ─────────────────────────────────────────────────────────────

public class IdentifikasiPemohonVm
{
    [Required(ErrorMessage = "Kategori wajib dipilih")]
    public string Kategori   { get; set; } = string.Empty;
    public string LoketJenis { get; set; } = string.Empty;
}

public class DaftarPemohonVm
{
    public string Kategori   { get; set; } = "Mahasiswa";
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

    // ── Alamat ────────────────────────────────────────────────────────────
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

    [Display(Name = "RT")] public string? RT { get; set; }
    [Display(Name = "RW")] public string? RW { get; set; }
    [Display(Name = "Alamat Lengkap")] public string? Alamat { get; set; }

    // ── Data Institusi ────────────────────────────────────────────────────
    [Display(Name = "NIM")]
    public string? NIM { get; set; }

    [Display(Name = "Lembaga / Universitas")]
    public string? Lembaga { get; set; }

    [Display(Name = "Fakultas")]
    public string? Fakultas { get; set; }

    [Display(Name = "Program Studi / Jurusan")]
    public string? Jurusan { get; set; }

    [Display(Name = "Pekerjaan")]
    public string? Pekerjaan { get; set; }

    // ── Data Permohonan ───────────────────────────────────────────────────
    [Required(ErrorMessage = "No. Surat Permohonan wajib diisi")]
    [Display(Name = "No. Surat Permohonan")]
    public string NoSuratPermohonan { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tanggal permohonan wajib diisi")]
    [Display(Name = "Tanggal Permohonan")]
    public DateOnly TanggalPermohonan { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Pengampu / PIC")]
    public string? Pengampu { get; set; }

    [Required(ErrorMessage = "Judul penelitian wajib diisi")]
    [Display(Name = "Judul Penelitian")]
    public string JudulPenelitian { get; set; } = string.Empty;

    [Required(ErrorMessage = "Latar belakang wajib diisi")]
    [Display(Name = "Latar Belakang Penelitian")]
    public string LatarBelakang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tujuan permohonan wajib diisi")]
    [Display(Name = "Tujuan Permohonan")]
    public string TujuanPermohonan { get; set; } = string.Empty;

    // ── Keperluan ─────────────────────────────────────────────────────────
    [Display(Name = "Observasi")]       public bool IsObservasi      { get; set; }
    [Display(Name = "Permintaan Data")] public bool IsPermintaanData { get; set; }
    [Display(Name = "Wawancara")]       public bool IsWawancara      { get; set; }

    [Display(Name = "Deskripsi Observasi")]    public string? DetailObservasi     { get; set; }
    [Display(Name = "Data yang Diperlukan")]   public string? DetailPermintaanData{ get; set; }
    [Display(Name = "Topik / Materi Wawancara")] public string? DetailWawancara  { get; set; }

    // ── Unit Kerja ────────────────────────────────────────────────────────
    [Display(Name = "Unit Kerja / Bidang")]
    public string? BidangID   { get; set; }
    public string? NamaBidang { get; set; }

    // ── Upload Dokumen ────────────────────────────────────────────────────
    [Display(Name = "KTP")]              public IFormFile? FileKTP              { get; set; }
    [Display(Name = "Surat Permohonan")] public IFormFile? FileSuratPermohonan  { get; set; }
    [Display(Name = "Proposal")]         public IFormFile? FileProposal         { get; set; }
    [Display(Name = "Akta Notaris")]     public IFormFile? FileAktaNotaris      { get; set; }
}

// ── PETUGAS LOKET: Upload TTD ─────────────────────────────────────────────────

public class UploadTTDVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string LoketJenis       { get; set; } = Models.LoketJenis.Kepegawaian;

    [Required(ErrorMessage = "File wajib diupload")]
    [Display(Name = "Dokumen Identifikasi (Sudah TTD Pemohon)")]
    public IFormFile? FileDokumenTTD { get; set; }
}

// ── KASUBKEL KEPEGAWAIAN: Verifikasi ─────────────────────────────────────────

public class VerifikasiVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string Kategori         { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;
    public string LatarBelakang    { get; set; } = string.Empty;
    public bool   IsObservasi      { get; set; }
    public bool   IsPermintaanData { get; set; }
    public bool   IsWawancara      { get; set; }
    public string NamaBidang       { get; set; } = string.Empty;

    [Display(Name = "Catatan Verifikasi")]
    public string? CatatanVerifikasi { get; set; }

    [Required(ErrorMessage = "Disposisi unit wajib diisi")]
    [Display(Name = "Disposisi ke Unit")]
    public string DisposisiUnit { get; set; } = "PSMDI";

    [Display(Name = "Nama Bidang (jika bukan PSMDI)")]
    public string? NamaBidangDisposisi { get; set; }

    public bool Disetujui { get; set; } = true;
    public string? AlasanDitolak { get; set; }
}

// ── KEPEGAWAIAN: Surat Izin ───────────────────────────────────────────────────

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

    public bool IsObservasi      { get; set; }
    public bool IsPermintaanData { get; set; }
    public bool IsWawancara      { get; set; }

    [Display(Name = "Disposisi Ke (KDI)")]
    public string DisposisiKe { get; set; } = "PSMDI";

    [Display(Name = "Nama Bidang Terkait")]
    public string? NamaBidangTerkait { get; set; }

    [Display(Name = "Catatan Disposisi")]
    public string? CatatanDisposisi { get; set; }

    [Display(Name = "Unit Produsen Data (Wawancara)")]
    public string? NamaProdusenData { get; set; }

    public bool HasKdiRoute   => IsPermintaanData || IsObservasi;
    public bool IsWawancaraOnly => IsWawancara && !IsPermintaanData && !IsObservasi;
}

// ── KDI: Terima Disposisi ─────────────────────────────────────────────────────

public class TerimaDisposisiVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;
    public string LatarBelakang    { get; set; } = string.Empty;
    public string? CatatanDisposisi { get; set; }
    public bool   PerluObservasi   { get; set; }
    public bool   PerluWawancara   { get; set; }
    public string? Catatan         { get; set; }
}

// ── KDI: Jadwal Observasi ─────────────────────────────────────────────────────

public class JadwalObservasiVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;

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
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;

    [Display(Name = "Catatan Hasil Observasi")]
    public string? Catatan { get; set; }
}

// ── Jadwal Wawancara ──────────────────────────────────────────────────────────

public class JadwalWawancaraVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;
    public string DetailWawancara  { get; set; } = string.Empty;
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

    public bool JadwalSudahAda { get; set; }
}

// ── KDI: Upload Data ─────────────────────────────────────────────────────────

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

// ── Produsen Data: Selesai Wawancara ─────────────────────────────────────────

public class SelesaiWawancaraVm
{
    public Guid      PermohonanPPIDID { get; set; }
    public string    NoPermohonan     { get; set; } = string.Empty;
    public string    NamaPemohon      { get; set; } = string.Empty;
    public string    JudulPenelitian  { get; set; } = string.Empty;
    public DateOnly? TanggalWawancara { get; set; }
    public TimeOnly? WaktuWawancara   { get; set; }
    public string?   NamaPIC          { get; set; }

    [Display(Name = "Dokumen Hasil (Opsional)")]
    public IFormFile? FileHasil { get; set; }

    [Display(Name = "Catatan")]
    public string? Catatan { get; set; }
}

// ── Kuesioner ─────────────────────────────────────────────────────────────────

public class KuesionerVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;

    [Required]
    [Range(1, 5)]
    public int NilaiKepuasan { get; set; }

    public string? Catatan { get; set; }
}

// ── Menu Data ──────────────────────────────────────────────────────────────────
/// ViewModel untuk Menu Data — menampilkan rekap & export semua permohonan
/// pada loket tertentu.
public class MenuDataVm
{
    public List<PermohonanPPID> List        { get; set; } = new();
    public string               LoketJenis  { get; set; } = Models.LoketJenis.Kepegawaian;
    public string               Judul       { get; set; } = "Menu Data";

    // ── Stat ringkasan ────────────────────────────────────────────────────
    public int Total    => List.Count;
    public int Proses   => List.Count(p => StatusId.IsProses(p.StatusPPIDID));
    public int Selesai  => List.Count(p => StatusId.IsSelesai(p.StatusPPIDID));
    public int Overdue  => List.Count(p => p.IsOverdue);

    // ── Breakdown keperluan ───────────────────────────────────────────────
    public int JmlObservasi      => List.Count(p => p.IsObservasi);
    public int JmlPermintaanData => List.Count(p => p.IsPermintaanData);
    public int JmlWawancara      => List.Count(p => p.IsWawancara);

    // ── Breakdown kategori pemohon ────────────────────────────────────────
    public int JmlMahasiswa => List.Count(p => p.KategoriPemohon == "Mahasiswa");
    public int JmlLSM       => List.Count(p => p.KategoriPemohon != "Mahasiswa");
}


/// ViewModel untuk Edit permohonan oleh Loket Kepegawaian dan Loket Umum.
/// Hanya field yang boleh diubah setelah pendaftaran.

public class EditPermohonanVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string LoketJenis       { get; set; } = Models.LoketJenis.Kepegawaian;

    [Required(ErrorMessage = "Judul wajib diisi")]
    [Display(Name = "Judul Penelitian / Tujuan")]
    public string JudulPenelitian { get; set; } = string.Empty;

    [Required(ErrorMessage = "Latar belakang wajib diisi")]
    [Display(Name = "Latar Belakang")]
    public string LatarBelakang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tujuan wajib diisi")]
    [Display(Name = "Tujuan Permohonan")]
    public string TujuanPermohonan { get; set; } = string.Empty;

    [Display(Name = "Pengampu / PIC")]
    public string? Pengampu { get; set; }

    [Display(Name = "Batas Waktu")]
    public DateOnly? BatasWaktu { get; set; }

    [Display(Name = "Observasi")]       public bool IsObservasi      { get; set; }
    [Display(Name = "Permintaan Data")] public bool IsPermintaanData { get; set; }
    [Display(Name = "Wawancara")]       public bool IsWawancara      { get; set; }
}

// ── Parallel Tasks ────────────────────────────────────────────────────────────

///
/// ViewModel untuk halaman manajemen tugas paralel KDI.
/// Menampilkan progress semua sub-tugas dalam satu permohonan.
/// <
public class ParallelTasksVm
{
    public PermohonanPPID   Permohonan { get; set; } = null!;
    public List<SubTaskPPID> SubTasks  { get; set; } = new();

    // ── Computed ──────────────────────────────────────────────────────────
    public int  TotalTasks    => SubTasks.Count;
    public int  DoneTasks     => SubTasks.Count(t => t.IsSelesai);
    public bool AllDone       => TotalTasks > 0 && DoneTasks == TotalTasks;
    public int  ProgressPct   => TotalTasks > 0 ? (int)Math.Round(DoneTasks * 100.0 / TotalTasks) : 0;

    public SubTaskPPID? GetTask(string jenisTask)
        => SubTasks.FirstOrDefault(t => t.JenisTask == jenisTask);
}

// ── Upload Data Sub-Task ──────────────────────────────────────────────────────

///
/// VM untuk upload file data pada sub-task PermintaanData.
/// <
public class UploadDataSubTaskVm
{
    public Guid   SubTaskID        { get; set; }
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;

    [Required(ErrorMessage = "File data wajib diupload")]
    [Display(Name = "File Data")]
    public IFormFile? FileData { get; set; }

    [Display(Name = "Catatan untuk Pemohon")]
    public string? Catatan { get; set; }
}

// ── Jadwal SubTask (Observasi / Wawancara) ────────────────────────────────────

///
/// VM untuk membuat / memperbarui jadwal pada sub-task Observasi atau Wawancara.
/// Digunakan bersama untuk kedua jenis jadwal.
/// <
public class JadwalSubTaskVm
{
    public Guid   SubTaskID        { get; set; }
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;
    public string JenisTask        { get; set; } = string.Empty;  // "Observasi" | "Wawancara"
    public string? DetailKeperluan { get; set; }
    public string? NamaBidangTerkait { get; set; }

    [Required]
    [Display(Name = "Tanggal")]
    public DateOnly Tanggal { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    [Required]
    [Display(Name = "Jam")]
    public TimeOnly Waktu { get; set; } = new TimeOnly(9, 0);

    [Required]
    [Display(Name = "Nama PIC / Narasumber")]
    public string NamaPIC { get; set; } = string.Empty;

    [Display(Name = "Lokasi / Platform")]
    public string? Lokasi { get; set; }
}

// ── Selesai SubTask ───────────────────────────────────────────────────────────

public class SelesaiSubTaskVm
{
    public Guid   SubTaskID        { get; set; }
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;
    public string NamaPemohon      { get; set; } = string.Empty;
    public string JudulPenelitian  { get; set; } = string.Empty;
    public string JenisTask        { get; set; } = string.Empty;
    public DateOnly? TanggalJadwal { get; set; }
    public TimeOnly? WaktuJadwal   { get; set; }
    public string?   NamaPIC       { get; set; }

    [Display(Name = "Catatan Hasil")]
    public string? Catatan { get; set; }

    ///Opsional — upload berkas hasil wawancara
    [Display(Name = "Dokumen Hasil (Opsional)")]
    public IFormFile? FileHasil { get; set; }
}

