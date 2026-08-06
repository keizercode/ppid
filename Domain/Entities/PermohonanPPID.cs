using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PermintaanData.Models;

namespace PermintaanData.Domain.Entities;

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
    [Column("AlasanBatal")]    public string?   AlasanBatal    { get; set; }
    [Column("DibatalkanAt")]   public DateTime? DibatalkanAt   { get; set; }
    [Column("DibatalkanOleh")] public string?   DibatalkanOleh { get; set; }

// Computed helper
    [Column("BidangID")]              public Guid?     BidangID          { get; set; }
    [Column("NamaBidang")]            public string?   NamaBidang        { get; set; }
    [Column("NamaProdusenData")]      public string?   NamaProdusenData  { get; set; }
    [Column("LoketJenis")]            public string?   LoketJenis        { get; set; }

    /// <summary>"Online" | "Offline" — sumber registrasi awal.</summary>
    [Column("SumberRegistrasi")]      public string?   SumberRegistrasi  { get; set; }

    [ForeignKey("PribadiID")]    public Pribadi?    Pribadi  { get; set; }
    [ForeignKey("StatusPPIDID")] public StatusPPID? Status   { get; set; }

    public ICollection<PermohonanPPIDDetail> Detail   { get; set; } = [];
    public ICollection<DokumenPPID>          Dokumen  { get; set; } = [];
    public ICollection<JadwalPPID>           Jadwal   { get; set; } = [];
    public ICollection<AuditLogPPID>         AuditLog { get; set; } = [];

    public bool IsDibatalkan => StatusPPIDID == StatusId.Dibatalkan;
    // ── Computed helpers ─────────────────────────────────────────────────
    public bool IsOverdue => BatasWaktu.HasValue
        && StatusPPIDID < StatusId.Selesai
        && BatasWaktu.Value < DateOnly.FromDateTime(DateTime.Today);

    public int? HariSisa => BatasWaktu.HasValue
        ? (int?)(BatasWaktu.Value.ToDateTime(TimeOnly.MinValue) - DateTime.Today).TotalDays
        : null;
}
