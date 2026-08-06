using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

[Table("PribadiPPID", Schema = "public")]
public class PribadiPPID
{
    [Key, Column("PribadiPPIDID")] public Guid     PribadiPPIDID { get; set; } = Guid.NewGuid();
    [Column("PribadiID")]          public Guid?    PribadiID     { get; set; }
    [Column("ProvinsiID")]         public string?  ProvinsiID    { get; set; }
    [Column("NamaProvinsi")]       public string?  NamaProvinsi  { get; set; }
    [Column("Lembaga")]            public string?  Lembaga       { get; set; }
    [Column("Fakultas")]           public string?  Fakultas      { get; set; }
    [Column("Jurusan")]            public string?  Jurusan       { get; set; }
    // Catatan: kolom DB menggunakan lowercase "pekerjaan" — dipertahankan sesuai skema.
    [Column("pekerjaan")]          public string?  Pekerjaan     { get; set; }
    [Column("NIM")]                public string?  NIM           { get; set; }
    [Column("CreatedAt")]          public DateTime? CreatedAt    { get; set; }
    [Column("UpdatedAt")]          public DateTime? UpdatedAt    { get; set; }

    [ForeignKey("PribadiID")] public Pribadi? Pribadi { get; set; }
}
