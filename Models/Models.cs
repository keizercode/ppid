using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;

namespace PermintaanData.Models;

// ═══════════════════════════════════════════════════════════════════════════
// ENTITAS UTAMA
// ═══════════════════════════════════════════════════════════════════════════

[Table("Pribadi", Schema = "public")]
public class Pribadi
{
    [Key, Column("PribadiID")]  public Guid     PribadiID     { get; set; } = Guid.NewGuid();
    [Column("NIK")]             public string?  NIK           { get; set; }
    [Column("Nama")]            public string?  Nama          { get; set; }
    [Column("Email")]           public string?  Email         { get; set; }
    [Column("Alamat")]          public string?  Alamat        { get; set; }
    [Column("RT")]              public string?  RT            { get; set; }
    [Column("RW")]              public string?  RW            { get; set; }
    [Column("KelurahanID")]     public string?  KelurahanID   { get; set; }
    [Column("KecamatanID")]     public string?  KecamatanID   { get; set; }
    [Column("KabupatenID")]     public string?  KabupatenID   { get; set; }
    [Column("NamaKelurahan")]   public string?  NamaKelurahan { get; set; }
    [Column("NamaKecamatan")]   public string?  NamaKecamatan { get; set; }
    [Column("NamaKabupaten")]   public string?  NamaKabupaten { get; set; }
    [Column("Telepon")]         public string?  Telepon       { get; set; }
    [Column("Kelamin")]         public bool?    Kelamin       { get; set; }
    [Column("IsKendaraan")]     public bool?    IsKendaraan   { get; set; }
    [Column("CreatedAt")]       public DateTime? CreatedAt    { get; set; }
    [Column("UpdatedAt")]       public DateTime? UpdatedAt    { get; set; }

    public PribadiPPID?                   PribadiPPID { get; set; }
    public ICollection<PermohonanPPID>    Permohonan  { get; set; } = [];
}

[Table("PribadiPPID", Schema = "public")]
public class PribadiPPID
{
    [Key, Column("PribadiPPIDID")] public Guid     PribadiPPIDID { get; set; } = Guid.NewGuid();
    [Column("PribadiID")]          public Guid?    PribadiID     { get; set; }
    [Column("ProvinsiID")]         public string?  ProvinsiID    { get; set; }
    [Column("NamaProvinsi")]       public string?  NamaProvinsi  { get; set; }
    [Column("Lembaga")]            public string?  Lembaga       { get; set; }
    [Column("Fakultas")]           public string?  Fakultas      { get; set; }
    [Column("Jurusan")]            public string?  Jurusan       { get; set; }
    // Catatan: kolom DB menggunakan lowercase "pekerjaan" — dipertahankan sesuai skema.
    [Column("pekerjaan")]          public string?  Pekerjaan     { get; set; }
    [Column("NIM")]                public string?  NIM           { get; set; }
    [Column("CreatedAt")]          public DateTime? CreatedAt    { get; set; }
    [Column("UpdatedAt")]          public DateTime? UpdatedAt    { get; set; }

    [ForeignKey("PribadiID")] public Pribadi? Pribadi { get; set; }
}

[Table("PermohonanPPID", Schema = "public")]
public class PermohonanPPID
{
    [Key, Column("PermohonanPPIDID")] public Guid      PermohonanPPIDID  { get; set; } = Guid.NewGuid();
    [Column("PribadiID")]             public Guid?     PribadiID         { get; set; }

    /// <summary>Format: MHS687592/PPID/III/2026 atau UMM687592/PPID/III/2026</summary>
    [Column("NoPermohonan")]          public string?   NoPermohonan      { get; set; }

    [Column("KategoriPemohon")]       public string?   KategoriPemohon   { get; set; }
    [Column("NoSuratPermohonan")]     public string?   NoSuratPermohonan { get; set; }
    [Column("TanggalPermohonan")]     public DateOnly? TanggalPermohonan { get; set; }
    [Column("BatasWaktu")]            public DateOnly? BatasWaktu        { get; set; }
    [Column("TanggalSelesai")]        public DateOnly? TanggalSelesai    { get; set; }
    [Column("Pengampu")]              public string?   Pengampu          { get; set; }
    [Column("TeleponPengampu")]       public string?   TeleponPengampu   { get; set; }
    [Column("JudulPenelitian")]       public string?   JudulPenelitian   { get; set; }
    [Column("LatarBelakang")]         public string?   LatarBelakang     { get; set; }
    [Column("TujuanPermohonan")]      public string?   TujuanPermohonan  { get; set; }
    [Column("IsObservasi")]           public bool      IsObservasi       { get; set; }
    [Column("IsWawancara")]           public bool      IsWawancara       { get; set; }
    [Column("IsPermintaanData")]      public bool      IsPermintaanData  { get; set; }
    [Column("StatusPPIDID")]          public int?      StatusPPIDID      { get; set; }
    [Column("Sequance")]              public int?      Sequance          { get; set; }
    // Catatan: typo "CratedAt" dipertahankan agar sesuai skema database.
    [Column("CratedAt")]              public DateTime? CratedAt          { get; set; }
    [Column("UpdatedAt")]             public DateTime? UpdatedAt         { get; set; }
    [Column("BidangID")]              public Guid?     BidangID          { get; set; }
    [Column("NamaBidang")]            public string?   NamaBidang        { get; set; }
    [Column("NamaProdusenData")]      public string?   NamaProdusenData  { get; set; }
    [Column("LoketJenis")]            public string?   LoketJenis        { get; set; }

    [ForeignKey("PribadiID")]    public Pribadi?    Pribadi  { get; set; }
    [ForeignKey("StatusPPIDID")] public StatusPPID? Status   { get; set; }

    public ICollection<PermohonanPPIDDetail> Detail   { get; set; } = [];
    public ICollection<DokumenPPID>          Dokumen  { get; set; } = [];
    public ICollection<JadwalPPID>           Jadwal   { get; set; } = [];
    public ICollection<AuditLogPPID>         AuditLog { get; set; } = [];

    // ── Computed helpers ─────────────────────────────────────────────────
    public bool IsOverdue => BatasWaktu.HasValue
        && StatusPPIDID < StatusId.Selesai
        && BatasWaktu.Value < DateOnly.FromDateTime(DateTime.Today);

    public int? HariSisa => BatasWaktu.HasValue
        ? (int?)(BatasWaktu.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).TotalDays
        : null;
}

// ═══════════════════════════════════════════════════════════════════════════
// NOPERMOHONAN TOKEN GENERATOR
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Menghasilkan 6 digit angka acak kriptografis untuk suffix NoPermohonan.
/// Format: MHS687592/PPID/III/2026 (Kepegawaian) | UMM687592/PPID/III/2026 (Umum)
/// </summary>
public static class NoPermohonanToken
{
    private static readonly string[] RomanMonths =
        ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII"];

    public static string GetRomanMonth(int month) => RomanMonths[month - 1];

    /// <summary>Generate 6 digit angka acak (100000–999999) menggunakan RNG kriptografis.</summary>
    public static string GenerateDigits()
    {
        var buf = new byte[4];
        RandomNumberGenerator.Fill(buf);
        var val = (BitConverter.ToUInt32(buf) % 900_000) + 100_000;
        return val.ToString("D6");
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ENTITAS PENDUKUNG
// ═══════════════════════════════════════════════════════════════════════════

[Table("PermohonanPPIDDetail", Schema = "public")]
public class PermohonanPPIDDetail
{
    [Key, Column("PermohonanPPIDDetailID")] public Guid    PermohonanPPIDDetailID { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")]            public Guid?   PermohonanPPIDID       { get; set; }
    [Column("KeperluanID")]                 public int?    KeperluanID            { get; set; }
    [Column("DetailKeperluan")]             public string? DetailKeperluan        { get; set; }
    [Column("CreatedAt")]                   public DateTime? CreatedAt            { get; set; }
    [Column("UpdatedAt")]                   public DateTime? UpdatedAt            { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
    [ForeignKey("KeperluanID")]      public Keperluan?       Keperluan  { get; set; }
}

[Table("Keperluan", Schema = "public")]
public class Keperluan
{
    [Key, Column("KeperluanID")]  public int      KeperluanID   { get; set; }
    [Column("NamaKeperluan")]     public string?  NamaKeperluan { get; set; }
    [Column("CreatedAt")]         public DateTime? CreatedAt    { get; set; }
    [Column("UpdatedAt")]         public DateTime? UpdatedAt    { get; set; }
}

[Table("StatusPPID", Schema = "public")]
public class StatusPPID
{
    [Key, Column("StatusPPIDID")]  public int      StatusPPIDID   { get; set; }
    [Column("NamaStatusPPID")]     public string?  NamaStatusPPID { get; set; }
    [Column("CreatedAt")]          public DateTime? CreatedAt     { get; set; }
    [Column("UpdatedAt")]          public DateTime? UpdatedAt     { get; set; }
}

[Table("DokumenPPID", Schema = "public")]
public class DokumenPPID
{
    [Key, Column("DokumenPPIDID")]       public Guid    DokumenPPIDID        { get; set; } = Guid.NewGuid();
    [Column("NamaDokumenPPID")]          public string? NamaDokumenPPID      { get; set; }
    [Column("PermohonanPPIDID")]         public Guid?   PermohonanPPIDID     { get; set; }
    [Column("UploadDokumenPPID")]        public string? UploadDokumenPPID    { get; set; }
    [Column("JenisDokumenPPIDID")]       public int?    JenisDokumenPPIDID   { get; set; }
    [Column("NamaJenisDokumenPPID")]     public string? NamaJenisDokumenPPID { get; set; }
    [Column("CreatedAt")]                public DateTime? CreatedAt          { get; set; }
    [Column("UpdatedAt")]                public DateTime? UpdatedAt          { get; set; }

    [ForeignKey("PermohonanPPIDID")]  public PermohonanPPID?  Permohonan   { get; set; }
    [ForeignKey("JenisDokumenPPIDID")] public JenisDokumenPPID? JenisDokumen { get; set; }
}

[Table("JenisDokumenPPID", Schema = "public")]
public class JenisDokumenPPID
{
    [Key, Column("JenisDokumenPPIDID")] public int      JenisDokumenPPIDID   { get; set; }
    [Column("NamaJenisDokumenPPID")]    public string?  NamaJenisDokumenPPID { get; set; }
    [Column("IsActive")]                public bool     IsActive             { get; set; }
    [Column("CreatedAt")]               public DateTime? CreatedAt           { get; set; }
    [Column("UpdatedAt")]               public DateTime? UpdatedAt           { get; set; }
}

[Table("JadwalPPID", Schema = "public")]
public class JadwalPPID
{
    [Key, Column("JadwalPPIDID")]  public Guid     JadwalPPIDID     { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")]   public Guid?    PermohonanPPIDID { get; set; }
    [Column("JenisJadwal")]        public string   JenisJadwal      { get; set; } = "Observasi";
    [Column("Tanggal")]            public DateOnly? Tanggal         { get; set; }
    [Column("Waktu")]              public TimeOnly? Waktu           { get; set; }
    [Column("NamaPIC")]            public string?  NamaPIC          { get; set; }
    [Column("TeleponPIC")]         public string?  TeleponPIC       { get; set; }

    /// <summary>Alasan perubahan jadwal / keterangan tambahan (EC-1, EC-6).</summary>
    [Column("Keterangan")]         public string?  Keterangan       { get; set; }

    /// <summary>
    /// Hanya satu jadwal per jenis yang aktif (EC-1).
    /// Jadwal lama tetap disimpan untuk audit trail tapi IsAktif = false.
    /// </summary>
    [Column("IsAktif")]            public bool     IsAktif          { get; set; } = true;

    [Column("CreatedAt")]          public DateTime? CreatedAt       { get; set; }
    [Column("UpdatedAt")]          public DateTime? UpdatedAt       { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
}

[Table("AuditLogPPID", Schema = "public")]
public class AuditLogPPID
{
    [Key, Column("AuditLogID")]   public Guid     AuditLogID       { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")]  public Guid     PermohonanPPIDID { get; set; }
    [Column("StatusLama")]        public int?     StatusLama       { get; set; }
    [Column("StatusBaru")]        public int?     StatusBaru       { get; set; }
    [Column("Keterangan")]        public string?  Keterangan       { get; set; }
    [Column("Operator")]          public string?  Operator         { get; set; }
    [Column("CreatedAt")]         public DateTime CreatedAt        { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// SUB-TASK PARALLEL PROCESSING
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Melacak tugas individual (PermintaanData / Observasi / Wawancara) secara paralel.
/// Semua SubTask harus StatusTask = Selesai sebelum permohonan advance ke DataSiap.
/// </summary>
[Table("SubTaskPPID", Schema = "public")]
public class SubTaskPPID
{
    [Key, Column("SubTaskID")]       public Guid      SubTaskID        { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")]     public Guid      PermohonanPPIDID { get; set; }
    [Column("JenisTask")]            public string    JenisTask        { get; set; } = string.Empty;
    [Column("StatusTask")]           public int       StatusTask       { get; set; } = SubTaskStatus.Pending;
    [Column("FilePath")]             public string?   FilePath         { get; set; }
    [Column("NamaFile")]             public string?   NamaFile         { get; set; }
    [Column("Catatan")]              public string?   Catatan          { get; set; }
    [Column("NamaPIC")]              public string?   NamaPIC          { get; set; }
    [Column("TeleponPIC")]           public string?   TeleponPIC       { get; set; }
    [Column("TanggalJadwal")]        public DateOnly? TanggalJadwal    { get; set; }
    [Column("WaktuJadwal")]          public TimeOnly? WaktuJadwal      { get; set; }
    [Column("Operator")]             public string?   Operator         { get; set; }
    [Column("CreatedAt")]            public DateTime  CreatedAt        { get; set; } = DateTime.UtcNow;
    [Column("SelesaiAt")]            public DateTime? SelesaiAt        { get; set; }
    [Column("UpdatedAt")]            public DateTime? UpdatedAt        { get; set; }

    // ── Lifecycle columns (EC-1, EC-2, EC-3, EC-4) ────────────────────────
    /// <summary>Alasan pembatalan (EC-2). Null jika belum pernah dibatalkan.</summary>
    [Column("BatalAlasan")]          public string?   BatalAlasan      { get; set; }

    /// <summary>Berapa kali jadwal sudah diubah (EC-1). Dipakai untuk audit + UI warning.</summary>
    [Column("RescheduleCount")]      public int       RescheduleCount  { get; set; }

    /// <summary>Terakhir di-reopen (EC-3).</summary>
    [Column("ReopenedAt")]           public DateTime? ReopenedAt       { get; set; }

    /// <summary>Alasan reopen (EC-3).</summary>
    [Column("ReopenAlasan")]         public string?   ReopenAlasan     { get; set; }

    /// <summary>
    /// Optimistic concurrency guard (EC-4).
    /// Di-increment setiap UPDATE di AdvanceIfAllSubTasksDone.
    /// </summary>
    [Column("RowVersion")]           public long      RowVersion       { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }

    // ── Computed helpers ──────────────────────────────────────────────────
    public bool IsPending    => StatusTask == SubTaskStatus.Pending;
    public bool IsInProgress => StatusTask == SubTaskStatus.InProgress;
    public bool IsSelesai    => StatusTask == SubTaskStatus.Selesai;
    public bool IsDibatalkan => StatusTask == SubTaskStatus.Dibatalkan;
    public bool IsTerminal   => SubTaskStatus.IsTerminal(StatusTask);
    public bool HasFile      => !string.IsNullOrEmpty(FilePath);
    public bool HasJadwal    => TanggalJadwal.HasValue;
    public bool WasRescheduled => RescheduleCount > 0;
}

// ═══════════════════════════════════════════════════════════════════════════
// KONSTANTA & ENUMERASI
// ═══════════════════════════════════════════════════════════════════════════

public static class SubTaskStatus
{
    public const int Pending    = 0;
    public const int InProgress = 1;
    public const int Selesai    = 2;
    public const int Dibatalkan = 3;    // EC-2: batal, tidak akan dikerjakan

    public static string GetLabel(int status) => status switch
    {
        Pending    => "Menunggu",
        InProgress => "Sedang Diproses",
        Selesai    => "Selesai",
        Dibatalkan => "Dibatalkan",
        _          => "—"
    };

    public static string GetBadgeClass(int status) => status switch
    {
        Pending    => "bg-gray-100 text-gray-500",
        InProgress => "bg-amber-50 text-amber-700",
        Selesai    => "bg-emerald-50 text-emerald-700",
        Dibatalkan => "bg-red-50 text-red-600",
        _          => "bg-gray-50 text-gray-400"
    };

    public static bool IsTerminal(int status) => status is Selesai or Dibatalkan;
}

public static class JenisTask
{
    public const string PermintaanData = "PermintaanData";
    public const string Observasi      = "Observasi";
    public const string Wawancara      = "Wawancara";

    public static string GetLabel(string jenis) => jenis switch
    {
        PermintaanData => "Permintaan Data",
        Observasi      => "Observasi",
        Wawancara      => "Wawancara",
        _              => jenis
    };

    public static string GetIcon(string jenis) => jenis switch
    {
        PermintaanData => "📊",
        Observasi      => "🔍",
        Wawancara      => "🎤",
        _              => "📋"
    };

    public static string GetColor(string jenis) => jenis switch
    {
        PermintaanData => "blue",
        Observasi      => "orange",
        Wawancara      => "violet",
        _              => "gray"
    };
}

public static class StatusId
{
    public const int Baru                 = 1;
    public const int TerdaftarSistem      = 2;
    public const int IdentifikasiAwal     = 3;
    public const int MenungguSuratIzin    = 4;
    public const int SuratIzinTerbit      = 5;
    public const int Didisposisi          = 6;
    public const int DiProses             = 7;
    public const int ObservasiDijadwalkan = 8;
    public const int ObservasiSelesai     = 9;
    public const int DataSiap             = 10;
    public const int Selesai              = 11;
    public const int WawancaraDijadwalkan = 12;
    public const int WawancaraSelesai     = 13;
    public const int MenungguVerifikasi   = 14;
    public const int FeedbackPemohon      = 15;

    public static string GetStepLabel(int? statusId) => statusId switch
    {
        TerdaftarSistem                              => "1. Permohonan",
        IdentifikasiAwal                             => "2. Tanda Tangan Identifikasi Awal",
        MenungguVerifikasi or MenungguSuratIzin      => "3. Verifikasi Identifikasi Awal",
        SuratIzinTerbit                              => "4–5. Surat Izin",
        Didisposisi or DiProses                      => "6. Pemrosesan / Pembuatan Jadwal",
        ObservasiDijadwalkan or WawancaraDijadwalkan => "6. Jadwal Observasi/Wawancara",
        ObservasiSelesai or WawancaraSelesai
            or DataSiap                              => "7. Data Tersedia / Selesai Obs/Waw",
        FeedbackPemohon                              => "8. Pengisian Feedback",
        Selesai                                      => "9. Selesai",
        _                                            => "—"
    };

    /// <summary>True jika permohonan sedang dalam proses (belum selesai, sudah terdaftar).</summary>
    public static bool IsProses(int? id)  => id.HasValue && id.Value > TerdaftarSistem && id.Value < Selesai;

    /// <summary>True jika permohonan sudah selesai.</summary>
    public static bool IsSelesai(int? id) => id == Selesai;
}

public static class JenisDokumenId
{
    public const int KTP                = 1;
    public const int SuratPermohonan    = 2;
    public const int Proposal           = 3;
    public const int AktaNotaris        = 4;
    public const int IdentifikasiSigned = 5;
    public const int SuratIzin          = 6;
    public const int DataHasil          = 7;
    public const int TugasFinal = 8;
}

public static class KeperluanId
{
    public const int Observasi      = 1;
    public const int PermintaanData = 2;
    public const int Wawancara      = 3;
}

public static class LoketJenis
{
    public const string Kepegawaian = "Kepegawaian";
    public const string Umum        = "Umum";

    /// <summary>
    /// Mengembalikan prefix nomor permohonan: "MHS" untuk Kepegawaian (mahasiswa),
    /// "UMM" untuk Umum (LSM/Organisasi/Perusahaan).
    /// </summary>
    public static string GetPrefix(string? loketJenis) =>
        loketJenis == Umum ? "UMM" : "MHS";
}

public static class AppRoles
{
    // ── Loket ─────────────────────────────────────────────────────────────
    /// <summary>Operator Loket Kepegawaian — prefix MHS</summary>
    public const string Loket               = "Loket";

    /// <summary>Operator Loket Umum — prefix UMM</summary>
    public const string LoketUmum           = "LoketUmum";

    // ── Kasubkel ──────────────────────────────────────────────────────────
    /// <summary>Kasubkel Kepegawaian: verifikasi + surat izin + disposisi</summary>
    public const string KasubkelKepegawaian = "KasubkelKepegawaian";

    /// <summary>Kasubkel KDI: permintaan data + parallel tasks + jadwal</summary>
    public const string KasubkelKDI         = "KasubkelKDI";

    // ── Sistem ────────────────────────────────────────────────────────────
    public const string Admin               = "Admin";

    // ── Helper: semua role yang boleh login ───────────────────────────────
    public static readonly string[] All =
    [
        Loket, LoketUmum,
        KasubkelKepegawaian, KasubkelKDI,
        Admin
    ];
}
