using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

/// <summary>
/// Feedback pemohon untuk setiap jenis tugas yang telah diselesaikan
/// (Observasi, PermintaanData, Wawancara).
/// Satu PermohonanPPID dapat memiliki hingga 3 feedback (unik per JenisTask).
/// Diterima dan dilihat oleh Kasubkel Kepegawaian.
/// </summary>
[Table("FeedbackTaskPPID", Schema = "public")]
public class FeedbackTaskPPID
{
    [Key, Column("FeedbackTaskID")]  public Guid    FeedbackTaskID   { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")]     public Guid    PermohonanPPIDID { get; set; }
    [Column("JenisTask")]            public string  JenisTask        { get; set; } = string.Empty;

    /// <summary>Nilai kepuasan pemohon 1–5.</summary>
    [Column("NilaiKepuasan")]        public int     NilaiKepuasan    { get; set; }

    [Column("Catatan")]              public string? Catatan          { get; set; }

    /// <summary>Path file laporan/tugas yang diunggah bersama feedback ini.</summary>
    [Column("FileLaporan")]          public string? FileLaporan      { get; set; }
    [Column("NamaFile")]             public string? NamaFile         { get; set; }
    [Column("CreatedAt")]            public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
}
