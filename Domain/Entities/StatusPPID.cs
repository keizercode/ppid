using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

[Table("StatusPPID", Schema = "public")]
public class StatusPPID
{
    [Key, Column("StatusPPIDID")]  public int      StatusPPIDID   { get; set; }
    [Column("NamaStatusPPID")]     public string?  NamaStatusPPID { get; set; }
    [Column("CreatedAt")]          public DateTime? CreatedAt     { get; set; }
    [Column("UpdatedAt")]          public DateTime? UpdatedAt     { get; set; }
}
