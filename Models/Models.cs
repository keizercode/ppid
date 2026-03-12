using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Models;

[Table("Pribadi", Schema = "public")]
public class Pribadi
{
    [Key, Column("PribadiID")] public Guid PribadiID { get; set; } = Guid.NewGuid();
    [Column("NIK")] public string? NIK { get; set; }
    [Column("Nama")] public string? Nama { get; set; }
    [Column("Email")] public string? Email { get; set; }
    [Column("Alamat")] public string? Alamat { get; set; }
    [Column("RT")] public string? RT { get; set; }
    [Column("RW")] public string? RW { get; set; }
    [Column("KelurahanID")] public string? KelurahanID { get; set; }
    [Column("KecamatanID")] public string? KecamatanID { get; set; }
    [Column("KabupatenID")] public string? KabupatenID { get; set; }
    [Column("NamaKelurahan")] public string? NamaKelurahan { get; set; }
    [Column("NamaKecamatan")] public string? NamaKecamatan { get; set; }
    [Column("NamaKabupaten")] public string? NamaKabupaten { get; set; }
    [Column("Telepon")] public string? Telepon { get; set; }
    [Column("Kelamin")] public bool? Kelamin { get; set; }
    [Column("IsKendaraan")] public bool? IsKendaraan { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }

    public PribadiPPID? PribadiPPID { get; set; }
    public ICollection<PermohonanPPID> Permohonan { get; set; } = new List<PermohonanPPID>();
}

[Table("PribadiPPID", Schema = "public")]
public class PribadiPPID
{
    [Key, Column("PribadiPPIDID")] public Guid PribadiPPIDID { get; set; } = Guid.NewGuid();
    [Column("PribadiID")] public Guid? PribadiID { get; set; }
    [Column("ProvinsiID")] public string? ProvinsiID { get; set; }
    [Column("NamaProvinsi")] public string? NamaProvinsi { get; set; }
    [Column("Lembaga")] public string? Lembaga { get; set; }
    [Column("Fakultas")] public string? Fakultas { get; set; }
    [Column("Jurusan")] public string? Jurusan { get; set; }
    [Column("pekerjaan")] public string? Pekerjaan { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }

    [ForeignKey("PribadiID")] public Pribadi? Pribadi { get; set; }
}

[Table("PermohonanPPID", Schema = "public")]
public class PermohonanPPID
{
    [Key, Column("PermohonanPPIDID")] public Guid PermohonanPPIDID { get; set; } = Guid.NewGuid();
    [Column("PribadiID")] public Guid? PribadiID { get; set; }
    [Column("NoPermohonan")] public string? NoPermohonan { get; set; }
    [Column("KategoriPemohon")] public string? KategoriPemohon { get; set; }
    [Column("NoSuratPermohonan")] public string? NoSuratPermohonan { get; set; }
    [Column("TanggalPermohonan")] public DateOnly? TanggalPermohonan { get; set; }
    [Column("JudulPenelitian")] public string? JudulPenelitian { get; set; }
    [Column("LatarBelakang")] public string? LatarBelakang { get; set; }
    [Column("TujuanPermohonan")] public string? TujuanPermohonan { get; set; }
    [Column("IsObservasi")] public bool IsObservasi { get; set; }
    [Column("IsWawancara")] public bool IsWawancara { get; set; }
    [Column("IsPermintaanData")] public bool IsPermintaanData { get; set; }
    [Column("StatusPPIDID")] public int? StatusPPIDID { get; set; }
    [Column("Sequance")] public int? Sequance { get; set; }
    [Column("CratedAt")] public DateTime? CratedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }
    [Column("BidangID")] public Guid? BidangID { get; set; }
    [Column("NamaBidang")] public string? NamaBidang { get; set; }
    /// <summary>Nama unit/bidang yang menjadi produsen data untuk wawancara langsung.</summary>
    [Column("NamaProdusenData")] public string? NamaProdusenData { get; set; }
    [Column("LoketJenis")] public string? LoketJenis { get; set; } // "Kepegawaian" | "Umum"

    [ForeignKey("PribadiID")] public Pribadi? Pribadi { get; set; }
    [ForeignKey("StatusPPIDID")] public StatusPPID? Status { get; set; }
    public ICollection<PermohonanPPIDDetail> Detail { get; set; } = new List<PermohonanPPIDDetail>();
    public ICollection<DokumenPPID> Dokumen { get; set; } = new List<DokumenPPID>();
    public ICollection<JadwalPPID> Jadwal { get; set; } = new List<JadwalPPID>();
}

[Table("PermohonanPPIDDetail", Schema = "public")]
public class PermohonanPPIDDetail
{
    [Key, Column("PermohonanPPIDDetailID")] public Guid PermohonanPPIDDetailID { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")] public Guid? PermohonanPPIDID { get; set; }
    [Column("KeperluanID")] public int? KeperluanID { get; set; }
    [Column("DetailKeperluan")] public string? DetailKeperluan { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
    [ForeignKey("KeperluanID")] public Keperluan? Keperluan { get; set; }
}

[Table("Keperluan", Schema = "public")]
public class Keperluan
{
    [Key, Column("KeperluanID")] public int KeperluanID { get; set; }
    [Column("NamaKeperluan")] public string? NamaKeperluan { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }
}

[Table("StatusPPID", Schema = "public")]
public class StatusPPID
{
    [Key, Column("StatusPPIDID")] public int StatusPPIDID { get; set; }
    [Column("NamaStatusPPID")] public string? NamaStatusPPID { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }
}

[Table("DokumenPPID", Schema = "public")]
public class DokumenPPID
{
    [Key, Column("DokumenPPIDID")] public Guid DokumenPPIDID { get; set; } = Guid.NewGuid();
    [Column("NamaDokumenPPID")] public string? NamaDokumenPPID { get; set; }
    [Column("PermohonanPPIDID")] public Guid? PermohonanPPIDID { get; set; }
    [Column("UploadDokumenPPID")] public string? UploadDokumenPPID { get; set; }
    [Column("JenisDokumenPPIDID")] public int? JenisDokumenPPIDID { get; set; }
    [Column("NamaJenisDokumenPPID")] public string? NamaJenisDokumenPPID { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
    [ForeignKey("JenisDokumenPPIDID")] public JenisDokumenPPID? JenisDokumen { get; set; }
}

[Table("JenisDokumenPPID", Schema = "public")]
public class JenisDokumenPPID
{
    [Key, Column("JenisDokumenPPIDID")] public int JenisDokumenPPIDID { get; set; }
    [Column("NamaJenisDokumenPPID")] public string? NamaJenisDokumenPPID { get; set; }
    [Column("IsActive")] public bool IsActive { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }
}

[Table("JadwalPPID", Schema = "public")]
public class JadwalPPID
{
    [Key, Column("JadwalPPIDID")] public Guid JadwalPPIDID { get; set; } = Guid.NewGuid();
    [Column("PermohonanPPIDID")] public Guid? PermohonanPPIDID { get; set; }
    [Column("JenisJadwal")] public string JenisJadwal { get; set; } = "Observasi"; // "Observasi" | "Wawancara"
    [Column("Tanggal")] public DateOnly? Tanggal { get; set; }
    [Column("Waktu")] public TimeOnly? Waktu { get; set; }
    [Column("NamaPIC")] public string? NamaPIC { get; set; }
    [Column("CreatedAt")] public DateTime? CreatedAt { get; set; }
    [Column("UpdatedAt")] public DateTime? UpdatedAt { get; set; }

    [ForeignKey("PermohonanPPIDID")] public PermohonanPPID? Permohonan { get; set; }
}

// ── Konstanta Status & Jenis Dokumen ─────────────────────────────────────────

/// <summary>
/// Alur status permohonan PPID.
/// ─────────────────────────────────────────────────────────────
/// LOKET KEPEGAWAIAN (Mahasiswa)  →  semua keperluan (Obs, Waw, Data)
/// LOKET UMUM        (LSM)        →  Observasi + Permintaan Data
///
/// Routing setelah Surat Izin terbit (Kepegawaian):
///   HasPermintaanData || HasObservasi  →  Didisposisi  →  KDI
///   IsWawancara ONLY                  →  WawancaraDijadwalkan  →  Produsen Data
///   Wawancara + Data/Obs (kombinasi)  →  Didisposisi  →  KDI
///                                         (KDI juga jadwalkan wawancara jika perlu)
/// ─────────────────────────────────────────────────────────────
/// </summary>
public static class StatusId
{
    public const int Baru = 1;
    public const int TerdaftarSistem = 2;
    public const int IdentifikasiAwal = 3;
    public const int MenungguSuratIzin = 4;
    public const int SuratIzinTerbit = 5;  // ditetapkan sebelum disposisi
    public const int Didisposisi = 6;  // → KDI (ada data / observasi)
    public const int DiProses = 7;
    public const int ObservasiDijadwalkan = 8;
    public const int ObservasiSelesai = 9;
    public const int DataSiap = 10;
    public const int Selesai = 11;
    // ── Jalur Wawancara (langsung ke Produsen Data) ───────────
    public const int WawancaraDijadwalkan = 12;  // wawancara-only: langsung ke unit produsen data
    public const int WawancaraSelesai = 13;  // wawancara selesai → DataSiap atau Selesai
}

public static class JenisDokumenId
{
    public const int KTP = 1;
    public const int SuratPermohonan = 2;
    public const int Proposal = 3;
    public const int AktaNotaris = 4;
    public const int IdentifikasiSigned = 5;
    public const int SuratIzin = 6;
    public const int DataHasil = 7;
}

public static class KeperluanId
{
    public const int Observasi = 1;
    public const int PermintaanData = 2;
    public const int Wawancara = 3;
}

/// <summary>Jenis loket tempat pemohon mendaftar.</summary>
public static class LoketJenis
{
    public const string Kepegawaian = "Kepegawaian"; // Mahasiswa/Peneliti – semua keperluan
    public const string Umum = "Umum";        // LSM/Organisasi – Obs + Data
}
