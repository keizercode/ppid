namespace PermintaanData.Models
{
    /// <summary>
    /// Representasi satu laporan PPID tahunan.
    /// </summary>
    public class LaporanPpidItem
    {
        /// <summary>Tahun laporan, misal 2025.</summary>
        public int Tahun { get; set; }

        /// <summary>Judul lengkap laporan.</summary>
        public string Judul { get; set; } = string.Empty;

        /// <summary>URL gambar sampul (opsional). Jika null, placeholder otomatis ditampilkan.</summary>
        public string? CoverImageUrl { get; set; }

        /// <summary>URL unduhan PDF laporan.</summary>
        public string? FileUrl { get; set; }

        /// <summary>Jumlah halaman laporan (opsional).</summary>
        public int? TotalHalaman { get; set; }

        /// <summary>Deskripsi singkat laporan.</summary>
        public string? Deskripsi { get; set; }
    }
}
