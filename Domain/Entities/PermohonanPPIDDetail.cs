using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

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
