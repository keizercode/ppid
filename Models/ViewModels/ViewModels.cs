using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using PermintaanData.Models;
using PermintaanData.Data;

namespace PermintaanData.Models.ViewModels;

// ═══════════════════════════════════════════════════════════════════════════
// DASHBOARD
// ═══════════════════════════════════════════════════════════════════════════

public class DashboardVm
{
    public int Total   { get; set; }
    public int Proses  { get; set; }
    public int Selesai { get; set; }

    public List<MonthlyStatRow> MonthlyStats { get; set; } = [];

    // Serialized JSON untuk Chart.js — lazy-computed
    public string LabelsJson  => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Label));
    public string TotalJson   => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Total));
    public string ProsesJson  => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Proses));
    public string SelesaiJson => JsonSerializer.Serialize(MonthlyStats.Select(m => m.Selesai));
}

// ═══════════════════════════════════════════════════════════════════════════
// PUBLIC: LACAK
// ═══════════════════════════════════════════════════════════════════════════

public class LacakViewModel
{
    [Required(ErrorMessage = "Nomor permohonan wajib diisi")]
    [Display(Name = "Nomor Permohonan")]
    public string NoPermohonan { get; set; } = string.Empty;
}

public class DetailLacakViewModel
{
    public PermohonanPPID             Permohonan  { get; set; } = null!;
    public Pribadi                    Pribadi     { get; set; } = null!;
    public PribadiPPID?               PribadiPPID { get; set; }
    public List<PermohonanPPIDDetail> Detail      { get; set; } = [];
    public List<JadwalPPID>           Jadwal      { get; set; } = [];
    public List<RiwayatStatusVm>      Riwayat     { get; set; } = [];
}

public class RiwayatStatusVm
{
    public int    StatusId      { get; set; }
    public string Label         { get; set; } = string.Empty;
    public bool   Selesai       { get; set; }
    public bool   AktifSekarang { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// AUTH
// ═══════════════════════════════════════════════════════════════════════════

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

// ═══════════════════════════════════════════════════════════════════════════
// LOKET — PENDAFTARAN PEMOHON
// ═══════════════════════════════════════════════════════════════════════════

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

    // Data Pribadi
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

    // Alamat
    public string? ProvinsiID    { get; set; }
    public string? NamaProvinsi  { get; set; }
    public string? KabupatenID   { get; set; }
    public string? NamaKabupaten { get; set; }
    public string? KecamatanID   { get; set; }
    public string? NamaKecamatan { get; set; }
    public string? KelurahanID   { get; set; }
    public string? NamaKelurahan { get; set; }
    public string? RT            { get; set; }
    public string? RW            { get; set; }
    public string? Alamat        { get; set; }

    // Data Institusi (Kepegawaian: mahasiswa; Umum: organisasi)
    public string? NIM      { get; set; }
    public string? Lembaga  { get; set; }
    public string? Fakultas { get; set; }
    public string? Jurusan  { get; set; }
    public string? Pekerjaan { get; set; }

    // Data Permohonan
    [Required(ErrorMessage = "No. Surat Permohonan wajib diisi")]
    [Display(Name = "No. Surat Permohonan")]
    public string NoSuratPermohonan { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tanggal permohonan wajib diisi")]
    [Display(Name = "Tanggal Permohonan")]
    public DateOnly TanggalPermohonan { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Display(Name = "Pengampu / PIC")]
    public string? Pengampu { get; set; }

    [Display(Name = "No. Telepon Pengampu / PIC")]
    [Phone(ErrorMessage = "Format nomor telepon tidak valid")]
    public string? TeleponPengampu { get; set; }

    [Required(ErrorMessage = "Judul penelitian wajib diisi")]
    [Display(Name = "Judul Penelitian")]
    public string JudulPenelitian { get; set; } = string.Empty;

    [Required(ErrorMessage = "Latar belakang wajib diisi")]
    [Display(Name = "Latar Belakang Penelitian")]
    public string LatarBelakang { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tujuan permohonan wajib diisi")]
    [Display(Name = "Tujuan Permohonan")]
    public string TujuanPermohonan { get; set; } = string.Empty;

    // Keperluan
    [Display(Name = "Observasi")]       public bool IsObservasi      { get; set; }
    [Display(Name = "Permintaan Data")] public bool IsPermintaanData { get; set; }
    [Display(Name = "Wawancara")]       public bool IsWawancara      { get; set; }

    public string? DetailObservasi      { get; set; }
    public string? DetailPermintaanData { get; set; }
    public string? DetailWawancara      { get; set; }

    // Unit Kerja (opsional — disposisi final dilakukan Kepegawaian)
    public string? BidangID   { get; set; }
    public string? NamaBidang { get; set; }

    // Upload Dokumen
    [Display(Name = "KTP")]              public IFormFile? FileKTP              { get; set; }
    [Display(Name = "Surat Permohonan")] public IFormFile? FileSuratPermohonan  { get; set; }
    [Display(Name = "Proposal")]         public IFormFile? FileProposal         { get; set; }
    [Display(Name = "Akta Notaris")]     public IFormFile? FileAktaNotaris      { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// LOKET — UPLOAD TTD
// ═══════════════════════════════════════════════════════════════════════════

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

// ═══════════════════════════════════════════════════════════════════════════
// KASUBKEL — VERIFIKASI
// ═══════════════════════════════════════════════════════════════════════════

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

    public bool    Disetujui     { get; set; } = true;
    public string? AlasanDitolak { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// KEPEGAWAIAN — SURAT IZIN
// ═══════════════════════════════════════════════════════════════════════════

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

    [Display(Name = "File Surat Izin (PDF)")]
    public IFormFile? FileSuratIzin { get; set; }

    public bool IsObservasi      { get; set; }
    public bool IsPermintaanData { get; set; }
    public bool IsWawancara      { get; set; }

    // ── Disposisi multi-unit (dari form checkbox + hidden) ────────────────
    /// <summary>
    /// Nilai yang dicentang di form: "PSMDI" dan/atau "BidangTerkait".
    /// Dikirim sebagai array dari checkbox dengan name="DisposisiUnits".
    /// </summary>
    public List<string> DisposisiUnits  { get; set; } = [];

    /// <summary>
    /// Nama bidang aktual yang dipilih per baris dropdown bidang.
    /// Dikirim sebagai array dari hidden input dengan name="NamaBidangList".
    /// </summary>
    public List<string> NamaBidangList  { get; set; } = [];

    [Display(Name = "Unit / Nama Produsen Data (Wawancara)")]
    public string? NamaProdusenData { get; set; }

    // ── Computed helpers ─────────────────────────────────────────────────

    /// <summary>True jika keperluan hanya wawancara (tanpa data atau observasi).</summary>
    public bool IsWawancaraOnly => IsWawancara && !IsPermintaanData && !IsObservasi;

    /// <summary>True jika ada keperluan yang memerlukan routing ke KDI.</summary>
    public bool HasKdiRoute => IsPermintaanData || IsObservasi;

    /// <summary>
    /// Nama bidang utama untuk kolom NamaBidang permohonan.
    /// Null berarti disposisi ke PSMDI (default KDI).
    /// Diambil dari NamaBidangList pertama yang tidak kosong.
    /// </summary>
    public string? NamaBidangPrimary =>
        NamaBidangList.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
}

// ═══════════════════════════════════════════════════════════════════════════
// KDI — TERIMA DISPOSISI & SUBTASK
// ═══════════════════════════════════════════════════════════════════════════

public class TerimaDisposisiVm
{
    public Guid    PermohonanPPIDID  { get; set; }
    public string  NoPermohonan      { get; set; } = string.Empty;
    public string  NamaPemohon       { get; set; } = string.Empty;
    public string  JudulPenelitian   { get; set; } = string.Empty;
    public string  LatarBelakang     { get; set; } = string.Empty;
    public string? CatatanDisposisi  { get; set; }
    public bool    PerluObservasi    { get; set; }
    public bool    PerluWawancara    { get; set; }
    public string? Catatan           { get; set; }
}

public class ParallelTasksVm
{
    public PermohonanPPID    Permohonan { get; set; } = null!;
    public List<SubTaskPPID> SubTasks   { get; set; } = [];

    public int  TotalTasks  => SubTasks.Count;
    public int  DoneTasks   => SubTasks.Count(t => t.IsSelesai);
    public bool AllDone     => TotalTasks > 0 && DoneTasks == TotalTasks;
    public int  ProgressPct => TotalTasks > 0 ? (int)Math.Round(DoneTasks * 100.0 / TotalTasks) : 0;

    public SubTaskPPID? GetTask(string jenisTask) =>
        SubTasks.FirstOrDefault(t => t.JenisTask == jenisTask);
}

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

public class JadwalSubTaskVm
{
    public Guid    SubTaskID         { get; set; }
    public Guid    PermohonanPPIDID  { get; set; }
    public string  NoPermohonan      { get; set; } = string.Empty;
    public string  NamaPemohon       { get; set; } = string.Empty;
    public string  JudulPenelitian   { get; set; } = string.Empty;
    public string  JenisTask         { get; set; } = string.Empty;
    public string? DetailKeperluan   { get; set; }
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

    [Display(Name = "No. Telepon PIC / Narasumber")]
    [Phone(ErrorMessage = "Format nomor telepon tidak valid")]
    public string? TeleponPIC { get; set; }

    [Display(Name = "Lokasi / Platform")]
    public string? Lokasi { get; set; }
}

public class SelesaiSubTaskVm
{
    public Guid      SubTaskID        { get; set; }
    public Guid      PermohonanPPIDID { get; set; }
    public string    NoPermohonan     { get; set; } = string.Empty;
    public string    NamaPemohon      { get; set; } = string.Empty;
    public string    JudulPenelitian  { get; set; } = string.Empty;
    public string    JenisTask        { get; set; } = string.Empty;
    public DateOnly? TanggalJadwal    { get; set; }
    public TimeOnly? WaktuJadwal      { get; set; }
    public string?   NamaPIC          { get; set; }
    public string?   TeleponPIC       { get; set; }

    [Display(Name = "Catatan Hasil")]
    public string? Catatan { get; set; }

    [Display(Name = "Dokumen Hasil (Opsional)")]
    public IFormFile? FileHasil { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// JADWAL WAWANCARA — PRODUSEN DATA
// ═══════════════════════════════════════════════════════════════════════════

public class JadwalWawancaraVm
{
    public Guid    PermohonanPPIDID  { get; set; }
    public string  NoPermohonan      { get; set; } = string.Empty;
    public string  NamaPemohon       { get; set; } = string.Empty;
    public string  JudulPenelitian   { get; set; } = string.Empty;
    public string  DetailWawancara   { get; set; } = string.Empty;
    public string? NamaProdusenData  { get; set; }

    [Required]
    [Display(Name = "Tanggal Wawancara")]
    public DateOnly TanggalWawancara { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

    [Required]
    [Display(Name = "Jam Wawancara")]
    public TimeOnly WaktuWawancara { get; set; } = new TimeOnly(9, 0);

    [Required]
    [Display(Name = "Nama Narasumber / PIC")]
    public string NamaPIC { get; set; } = string.Empty;

    [Display(Name = "No. Telepon Narasumber / PIC")]
    [Phone(ErrorMessage = "Format nomor telepon tidak valid")]
    public string? TeleponPIC { get; set; }

    [Display(Name = "Lokasi / Platform")]
    public string? Lokasi { get; set; }

    public bool JadwalSudahAda { get; set; }
}

public class SelesaiWawancaraVm
{
    public Guid      PermohonanPPIDID { get; set; }
    public string    NoPermohonan     { get; set; } = string.Empty;
    public string    NamaPemohon      { get; set; } = string.Empty;
    public string    JudulPenelitian  { get; set; } = string.Empty;
    public DateOnly? TanggalWawancara { get; set; }
    public TimeOnly? WaktuWawancara   { get; set; }
    public string?   NamaPIC          { get; set; }
    public string?   TeleponPIC       { get; set; }

    [Display(Name = "Dokumen Hasil (Opsional)")]
    public IFormFile? FileHasil { get; set; }

    [Display(Name = "Catatan")]
    public string? Catatan { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// KUESIONER
// ═══════════════════════════════════════════════════════════════════════════

public class KuesionerVm
{
    public Guid   PermohonanPPIDID { get; set; }
    public string NoPermohonan     { get; set; } = string.Empty;

    [Required]
    [Range(1, 5)]
    public int NilaiKepuasan { get; set; }

    public string? Catatan { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// MENU DATA
// ═══════════════════════════════════════════════════════════════════════════

public class MenuDataVm
{
    public List<PermohonanPPID> List        { get; set; } = [];
    public string               LoketJenis  { get; set; } = Models.LoketJenis.Kepegawaian;
    public string               Judul       { get; set; } = "Menu Data";

    public int Total    => List.Count;
    public int Proses   => List.Count(p => StatusId.IsProses(p.StatusPPIDID));
    public int Selesai  => List.Count(p => StatusId.IsSelesai(p.StatusPPIDID));
    public int Overdue  => List.Count(p => p.IsOverdue);

    public int JmlObservasi      => List.Count(p => p.IsObservasi);
    public int JmlPermintaanData => List.Count(p => p.IsPermintaanData);
    public int JmlWawancara      => List.Count(p => p.IsWawancara);

    public int JmlMahasiswa => List.Count(p => p.KategoriPemohon == "Mahasiswa");
    public int JmlLSM       => List.Count(p => p.KategoriPemohon != "Mahasiswa");
}

// ═══════════════════════════════════════════════════════════════════════════
// EDIT PERMOHONAN
// ═══════════════════════════════════════════════════════════════════════════

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

    [Display(Name = "No. Telepon Pengampu / PIC")]
    [Phone(ErrorMessage = "Format nomor telepon tidak valid")]
    public string? TeleponPengampu { get; set; }

    [Display(Name = "Batas Waktu")]
    public DateOnly? BatasWaktu { get; set; }

    [Display(Name = "Observasi")]       public bool IsObservasi      { get; set; }
    [Display(Name = "Permintaan Data")] public bool IsPermintaanData { get; set; }
    [Display(Name = "Wawancara")]       public bool IsWawancara      { get; set; }
}
