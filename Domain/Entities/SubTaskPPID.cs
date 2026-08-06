using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PermintaanData.Models;

namespace PermintaanData.Domain.Entities;

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
    /// <summary>"Offline" atau "Online"</summary>
    [Column("LokasiJenis")]   public string? LokasiJenis  { get; set; }

    /// <summary>Nama ruangan (offline) atau link Zoom/Meet (online)</summary>
    [Column("LokasiDetail")]  public string? LokasiDetail { get; set; }

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

    // RowVersion dihapus — digantikan xmin via UseXminAsConcurrencyToken() di DbContext.
   // Kolom bigint "RowVersion" di DB dibiarkan apa adanya (tidak dikelola EF lagi).

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }

    // ── Computed helpers ──────────────────────────────────────────────────
    public bool IsPending           => StatusTask == SubTaskStatus.Pending;
    public bool IsInProgress        => StatusTask == SubTaskStatus.InProgress;
    public bool IsSelesai           => StatusTask == SubTaskStatus.Selesai;
    public bool IsDibatalkan        => StatusTask == SubTaskStatus.Dibatalkan;
    public bool IsWaitingKonfirmasi => StatusTask == SubTaskStatus.WaitingKonfirmasi;
    public bool IsTerminal          => SubTaskStatus.IsTerminal(StatusTask);
    public bool HasFile      => !string.IsNullOrEmpty(FilePath);
    public bool HasJadwal    => TanggalJadwal.HasValue;
    public bool WasRescheduled => RescheduleCount > 0;
}
