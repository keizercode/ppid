using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

[Table("JenisDokumenPPID", Schema = "public")]
public class JenisDokumenPPID
{
    [Key, Column("JenisDokumenPPIDID")] public int      JenisDokumenPPIDID   { get; set; }
    [Column("NamaJenisDokumenPPID")]    public string?  NamaJenisDokumenPPID { get; set; }
    [Column("IsActive")]                public bool     IsActive             { get; set; }
    [Column("CreatedAt")]               public DateTime? CreatedAt           { get; set; }
    [Column("UpdatedAt")]               public DateTime? UpdatedAt           { get; set; }
}
