using Microsoft.AspNetCore.Mvc;
using PermintaanData.Models;

namespace PermintaanData.Controllers
{
    public class LaporanController : Controller
    {
        // ── Seed data – ganti dengan query database / repository sesuai kebutuhan ──
        private static readonly List<LaporanPpidItem> _laporanList = new()
        {
            new LaporanPpidItem
            {
                Tahun        = 2025,
                Judul        = "Laporan Layanan Informasi Publik Pemerintah Provinsi DKI Jakarta",
                CoverImageUrl = "https://res.cloudinary.com/dmcvht1vr/image/upload/v1777878579/laporan_ppid_2025_uvfjwd.png",          // isi URL gambar sampul jika tersedia
                FileUrl      = "/laporan/laporan-ppid-2025.pdf",            // ganti dengan URL PDF sebenarnya
                TotalHalaman = 38,
                Deskripsi    = "Laporan tahunan pengelolaan informasi dan dokumentasi PPID DLH Jakarta tahun 2025."
            },
            new LaporanPpidItem
            {
                Tahun        = 2024,
                Judul        = "Laporan Layanan Informasi Publik Pemerintah Provinsi DKI Jakarta",
                CoverImageUrl = null,
                FileUrl      = "#",
                TotalHalaman = 42,
                Deskripsi    = "Laporan tahunan pengelolaan informasi dan dokumentasi PPID DLH Jakarta tahun 2024."
            },
            new LaporanPpidItem
            {
                Tahun        = 2023,
                Judul        = "Laporan Layanan Informasi Publik Pemerintah Provinsi DKI Jakarta",
                CoverImageUrl = null,
                FileUrl      = "#",
                TotalHalaman = 35,
                Deskripsi    = "Laporan tahunan pengelolaan informasi dan dokumentasi PPID DLH Jakarta tahun 2023."
            },
            // new LaporanPpidItem
            // {
            //     Tahun        = 2022,
            //     Judul        = "Laporan Layanan Informasi Publik Pemerintah Provinsi DKI Jakarta",
            //     CoverImageUrl = null,
            //     FileUrl      = "#",
            //     TotalHalaman = 31,
            //     Deskripsi    = "Laporan tahunan pengelolaan informasi dan dokumentasi PPID DLH Jakarta tahun 2022."
            // },
        };

        // GET /Laporan
        [Route("laporan-ppid")]
        public IActionResult Index()
        {
            // Urutkan dari terbaru
            var model = _laporanList.OrderByDescending(x => x.Tahun).ToList();
            return View("~/Views/Home/LaporanPpid.cshtml", model);
        }
    }
}
