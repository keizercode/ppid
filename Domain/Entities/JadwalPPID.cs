using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

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

    /// <summary>"Offline" atau "Online"</summary>
    [Column("LokasiJenis")]   public string? LokasiJenis  { get; set; }

    /// <summary>Nama ruangan (offline) atau link Zoom/Meet (online)</summary>
    [Column("LokasiDetail")]  public string? LokasiDetail { get; set; }

    [Column("CreatedAt")]          public DateTime? CreatedAt       { get; set; }
    [Column("UpdatedAt")]          public DateTime? UpdatedAt       { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
}
