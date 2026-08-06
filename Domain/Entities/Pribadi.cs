using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Domain.Entities;

[Table("Pribadi", Schema = "public")]
public class Pribadi
{
    [Key, Column("PribadiID")]  public Guid     PribadiID     { get; set; } = Guid.NewGuid();
    [Column("NIK")]             public string?  NIK           { get; set; }
    [Column("Nama")]            public string?  Nama          { get; set; }
    [Column("Email")]           public string?  Email         { get; set; }
    [Column("Alamat")]          public string?  Alamat        { get; set; }
    [Column("RT")]              public string?  RT            { get; set; }
    [Column("RW")]              public string?  RW            { get; set; }
    [Column("KelurahanID")]     public string?  KelurahanID   { get; set; }
    [Column("KecamatanID")]     public string?  KecamatanID   { get; set; }
    [Column("KabupatenID")]     public string?  KabupatenID   { get; set; }
    [Column("NamaKelurahan")]   public string?  NamaKelurahan { get; set; }
    [Column("NamaKecamatan")]   public string?  NamaKecamatan { get; set; }
    [Column("NamaKabupaten")]   public string?  NamaKabupaten { get; set; }
    [Column("Telepon")]         public string?  Telepon       { get; set; }
    [Column("Kelamin")]         public bool?    Kelamin       { get; set; }
    [Column("IsKendaraan")]     public bool?    IsKendaraan   { get; set; }
    [Column("CreatedAt")]       public DateTime? CreatedAt    { get; set; }
    [Column("UpdatedAt")]       public DateTime? UpdatedAt    { get; set; }

    public PribadiPPID?                   PribadiPPID { get; set; }
    public ICollection<PermohonanPPID>    Permohonan  { get; set; } = [];
}
