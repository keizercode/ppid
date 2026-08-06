using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

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
