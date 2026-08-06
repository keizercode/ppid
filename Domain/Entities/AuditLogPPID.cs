using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

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
