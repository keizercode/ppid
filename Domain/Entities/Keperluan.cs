using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

[Table("Keperluan", Schema = "public")]
public class Keperluan
{
    [Key, Column("KeperluanID")]  public int      KeperluanID   { get; set; }
    [Column("NamaKeperluan")]     public string?  NamaKeperluan { get; set; }
    [Column("CreatedAt")]         public DateTime? CreatedAt    { get; set; }
    [Column("UpdatedAt")]         public DateTime? UpdatedAt    { get; set; }
}
