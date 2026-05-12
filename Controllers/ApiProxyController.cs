using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Data;
using System.Text.Json;

namespace PermintaanData.Controllers;

/// <summary>
/// Proxy ke semua API eksternal — fully self-contained.
/// Membaca URL langsung dari IConfiguration, tidak bergantung pada
/// named HttpClient di Program.cs sehingga tidak perlu AddHttpClient("WilayahApi").
///
/// appsettings.json yang dibutuhkan:
/// "ExternalApi": {
///   "WilayahBase": "https://api-wilayah.dinaslhdki.id",
///   "NikCheck":    "https://banksampah.jakarta.go.id/api/web/cek-nik",
///   "Bidang":      "https://ekinerjapjlp.jakarta.go.id/api/master/bidang/search"
/// }
/// </summary>
[Route("api")]
public class ApiProxyController(
    IHttpClientFactory httpFactory,
    IConfiguration cfg,
    AppDbContext db) : Controller
{
    // HttpClient stateless — aman dibuat per-request lewat factory
    private HttpClient Http() => httpFactory.CreateClient();

    private string WilayahBase => (cfg["ExternalApi:WilayahBase"] ?? "").TrimEnd('/');
    private string NikUrl => cfg["ExternalApi:NikCheck"] ?? "";
    private string BidangUrl => cfg["ExternalApi:Bidang"] ?? "";

    // ── GET /api/provinsi ─────────────────────────────────────────────────
    [HttpGet("provinsi")]
    public async Task<IActionResult> Provinsi()
    {
        if (string.IsNullOrEmpty(WilayahBase)) return Ok("[]");
        try
        {
            var res = await Http().GetStringAsync($"{WilayahBase}/api/provinsi/search");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // ── GET /api/kabupaten ────────────────────────────────────────────────
    // API wilayah DLH hanya menyediakan kabupaten DKJ — param prov diabaikan.
    [HttpGet("kabupaten")]
    public async Task<IActionResult> Kabupaten([FromQuery] string? prov)
    {
        if (string.IsNullOrEmpty(WilayahBase)) return Ok("[]");
        try
        {
            var res = await Http().GetStringAsync($"{WilayahBase}/api/kabupaten/dkj/search");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // ── GET /api/kecamatan?kab=31XX ───────────────────────────────────────
    [HttpGet("kecamatan")]
    public async Task<IActionResult> Kecamatan([FromQuery] string? kab)
    {
        if (string.IsNullOrEmpty(kab) || string.IsNullOrEmpty(WilayahBase))
            return Ok("[]");
        try
        {
            var res = await Http().GetStringAsync($"{WilayahBase}/api/kecamatan/search?kab={kab}");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // ── GET /api/kelurahan?kec=31XX01 ─────────────────────────────────────
    [HttpGet("kelurahan")]
    public async Task<IActionResult> Kelurahan([FromQuery] string? kec)
    {
        if (string.IsNullOrEmpty(kec) || string.IsNullOrEmpty(WilayahBase))
            return Ok("[]");
        try
        {
            var res = await Http().GetStringAsync($"{WilayahBase}/api/kelurahan/search?kec={kec}");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // ── GET /api/cek-nik?nik=3174XXXXXX ──────────────────────────────────
    //
    // Response banksampah:
    // { noKTP, nik, nama, jenisKelamin, kota, kecamatan, kelurahan,
    //   rw, rt, kelurahanID, kecamatanID, kabupatenID, provinsiID, kelurahanVal }
    // Semua field null jika NIK tidak terdaftar → return null ke JS.
    //
    // Prioritas: DB lokal dulu → banksampah API.

    [HttpGet("cek-nik")]
    public async Task<IActionResult> CekNik([FromQuery] string? nik)
    {
        if (string.IsNullOrWhiteSpace(nik) || nik.Length != 16)
            return Json(null);

        // ── 1. DB lokal — pemohon yang pernah daftar ──────────────────────
        var pribadi = await db.Pribadi
            .Include(p => p.PribadiPPID)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.NIK == nik);

        if (pribadi != null)
        {
            return Json(new
            {
                nama = pribadi.Nama,
                telepon = pribadi.Telepon,
                email = pribadi.Email,
                alamat = pribadi.Alamat,
                rt = pribadi.RT,
                rw = pribadi.RW,
                kelurahanID = pribadi.KelurahanID,
                kelurahan = pribadi.NamaKelurahan,
                kecamatanID = pribadi.KecamatanID,
                kecamatan = pribadi.NamaKecamatan,
                kabupatenID = pribadi.KabupatenID,
                kota = pribadi.NamaKabupaten,
                lembaga = pribadi.PribadiPPID?.Lembaga,
                fakultas = pribadi.PribadiPPID?.Fakultas,
                jurusan = pribadi.PribadiPPID?.Jurusan,
                pekerjaan = pribadi.PribadiPPID?.Pekerjaan,
                provinsiID = pribadi.PribadiPPID?.ProvinsiID,
                namaProvinsi = pribadi.PribadiPPID?.NamaProvinsi,
                source = "db"
            });
        }

        // ── 2. banksampah API ─────────────────────────────────────────────
        if (string.IsNullOrEmpty(NikUrl))
            return Json(null);

        try
        {
            var raw = await Http().GetStringAsync($"{NikUrl}/?nik={nik}");

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var nama = Str(root, "nama");
            if (string.IsNullOrEmpty(nama))
                return Json(null);   // NIK tidak terdaftar di banksampah

            return Json(new
            {
                nama,
                rt = Str(root, "rt"),
                rw = Str(root, "rw"),
                kelurahanID = Str(root, "kelurahanID"),
                kelurahan = Str(root, "kelurahan"),
                kecamatanID = Str(root, "kecamatanID"),
                kecamatan = Str(root, "kecamatan"),
                kabupatenID = Str(root, "kabupatenID"),
                kota = Str(root, "kota"),
                provinsiID = Str(root, "provinsiID"),
                telepon = (string?)null,
                email = (string?)null,
                alamat = (string?)null,
                lembaga = (string?)null,
                fakultas = (string?)null,
                jurusan = (string?)null,
                pekerjaan = (string?)null,
                namaProvinsi = (string?)null,
                source = "api"
            });
        }
        catch { return Json(null); }
    }

    // ── GET /api/bidang ───────────────────────────────────────────────────
    // Prioritas: ekinerjapjlp API → DB lokal → hardcode DLH Jakarta.

    [HttpGet("bidang")]
    public async Task<IActionResult> Bidang()
    {
        // ── 1. API eksternal ──────────────────────────────────────────────
        if (!string.IsNullOrEmpty(BidangUrl))
        {
            try
            {
                var raw = await Http().GetStringAsync(BidangUrl);
                using var doc = JsonDocument.Parse(raw);
                var arr = doc.RootElement;
                if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                    return Content(raw, "application/json");
            }
            catch { /* lanjut fallback */ }
        }

        // ── 2. DB lokal — distinct NamaBidang dari permohonan sebelumnya ──
        var fromDb = await db.PermohonanPPID
            .Where(p => p.NamaBidang != null && p.NamaBidang != "")
            .Select(p => new { p.BidangID, p.NamaBidang })
            .Distinct()
            .ToListAsync();

        var dbList = fromDb
            .GroupBy(x => x.NamaBidang)
            .Select(g => new
            {
                id = (g.FirstOrDefault()?.BidangID ?? Guid.NewGuid()).ToString(),
                namaBidang = g.Key!
            })
            .OrderBy(x => x.namaBidang)
            .ToList();

        if (dbList.Count > 0)
            return Json(dbList);

        // ── 3. Hardcode DLH Jakarta ───────────────────────────────────────
        return Json(_HardcodedBidang.Select(b => new { id = b.Id, namaBidang = b.Nama }));
    }

    // ── Helper ────────────────────────────────────────────────────────────
    private static string? Str(JsonElement el, string key)
    {
        if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
            return p.GetString();
        return null;
    }

    // ── GET /api/bidang-hierarki ──────────────────────────────────────────
[HttpGet("bidang-hierarki")]
public async Task<IActionResult> BidangHierarki()
{
    // _HardcodedBidang adalah satu-satunya sumber urutan & nama yang benar.
    // API eksternal hanya dipakai untuk mengetahui ID mana yang aktif —
    // jika API tersedia, sembunyikan ID yang tidak dikenal API;
    // jika API down atau kosong, tampilkan semua dari _HardcodedBidang.
    HashSet<string>? activeIds = null;

    if (!string.IsNullOrEmpty(BidangUrl))
    {
        try
        {
            var raw = await Http().GetStringAsync(BidangUrl);
            using var doc = JsonDocument.Parse(raw);
            var arr = doc.RootElement;
            if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
            {
                activeIds = [];
                foreach (var el in arr.EnumerateArray())
                {
                    var id = Str(el, "id") ?? string.Empty;
                    if (!string.IsNullOrEmpty(id)) activeIds.Add(id);
                }
            }
        }
        catch { /* API down → activeIds tetap null → tampilkan semua */ }
    }

    // Urutan dijamin oleh _HardcodedBidang (array berurutan).
    // Nama selalu dari _HardcodedBidang — tidak terpengaruh urutan/nama API.
    var result = _HardcodedBidang
        .Where(b => activeIds is null || activeIds.Contains(b.Id))
        .Select(b => new
        {
            id         = b.Id,
            namaBidang = b.Nama,
            children   = _BidangChildren.TryGetValue(b.Id, out var ch) ? ch : Array.Empty<string>()
        });

    return Json(result);
}

// ── Hardcoded fallback — urutan resmi: Bidang → UPT → Sudin 5 Kota → Kep. Seribu ──
private static readonly (string Id, string Nama)[] _HardcodedBidang =
[
    // ── Bidang (6) ────────────────────────────────────────────────────────
    ("08dacde9-3932-4c5d-8808-5fe4db495eae", "Kepala Bidang Tata Lingkungan"),
    ("d81e1bb5-61b9-44a1-aa97-a7f9450bd046", "Kepala Bidang Pengelolaan Sampah dan Limbah B3"),
    ("08dacde9-c0e9-4594-84bd-015a21616a8b", "Kepala Bidang Pengendalian Pencemaran dan Kerusakan Lingkungan"),
    ("08dacde9-eb49-4127-852b-566d7540c603", "Kepala Bidang Pengawasan dan Penataan Hukum"),
    ("08dacde9-9e35-4f34-825a-a6201ea6ff9f", "Kepala Bidang Peran Serta Masyarakat, Data, dan Informasi"),
    ("08dacde9-6fb2-4da9-8de6-4207712ffa3f", "Kepala Bidang Pengurangan dan Penanganan Sampah"),
    // ── UPT (3) ───────────────────────────────────────────────────────────
    ("d0601901-7f57-455f-b9ac-f4b794396030", "Kepala Laboratorium Lingkungan Hidup Daerah"),
    ("1c74a891-afe4-40c8-aff6-7ca8afa2af95", "Kepala Unit Penanganan Sampah Badan Air"),
    ("2d9b3feb-8690-4198-9a9a-c65e78538a36", "Kepala Unit Pengelola Sampah Terpadu"),
    // ── Suku Dinas 5 Kota ─────────────────────────────────────────────────
    ("77136212-8ad7-48de-8270-69ae949ed895", "Kepala Suku Dinas Lingkungan Hidup Kota Administrasi Jakarta Pusat"),
    ("fcc59e08-cf7f-40e0-b6e3-2ef748b8c218", "Kepala Suku Dinas Lingkungan Hidup Kota Administrasi Jakarta Utara"),
    ("c89321db-b4e5-4638-ba59-f4280030f9fe", "Kepala Suku Dinas Lingkungan Hidup Kota Administrasi Jakarta Barat"),
    ("d47b8252-a7ac-46a1-8d9f-b0e5a8e4ba67", "Kepala Suku Dinas Lingkungan Hidup Kota Administrasi Jakarta Timur"),
    ("b4b04c3b-3f6f-4005-a79b-2b4daba5de1a", "Kepala Suku Dinas Lingkungan Hidup Kota Administrasi Jakarta Selatan"),
    // ── Suku Dinas Kepulauan Seribu ───────────────────────────────────────
    ("529d9d7a-b365-47d5-9f4f-3f2b67326c79", "Kepala Suku Dinas Lingkungan Hidup Kabupaten Administrasi Kepulauan Seribu"),
];

// ── Sub-unit per Bidang (struktur organisasi resmi — lengkap) ─────────
private static readonly Dictionary<string, string[]> _BidangChildren = new()
{
    // ── Bidang Tata Lingkungan ────────────────────────────────────────────
    ["08dacde9-3932-4c5d-8808-5fe4db495eae"] =
    [
        "Ketua Subkelompok Perencanaan Lingkungan",
        "Ketua Subkelompok Kajian Dampak Lingkungan",
        "Ketua Subkelompok Pemeliharaan Lingkungan",
    ],
    // ── Bidang Pengelolaan Sampah dan Limbah B3 ───────────────────────────
    ["d81e1bb5-61b9-44a1-aa97-a7f9450bd046"] =
    [
        "Ketua Subkelompok Pengelolaan Sampah",
        "Ketua Subkelompok Pengelolaan Limbah B3",
        "Ketua Subkelompok Pengembangan Fasilitas Teknis",
    ],
    // ── Bidang Pengendalian Pencemaran dan Kerusakan Lingkungan ───────────
    ["08dacde9-c0e9-4594-84bd-015a21616a8b"] =
    [
        "Ketua Subkelompok Pemantauan Lingkungan",
        "Ketua Subkelompok Pencegahan Pencemaran Lingkungan",
        "Ketua Subkelompok Kerusakan Lingkungan",
    ],
    // ── Bidang Pengawasan dan Penataan Hukum ─────────────────────────────
    ["08dacde9-eb49-4127-852b-566d7540c603"] =
    [
        "Ketua Subkelompok Pengaduan dan Penyelesaian Sengketa Lingkungan",
        "Ketua Subkelompok Pengawasan Lingkungan",
        "Ketua Subkelompok Penegakan Hukum Lingkungan",
    ],
    // ── Bidang Peran Serta Masyarakat, Data, dan Informasi ───────────────
    ["08dacde9-9e35-4f34-825a-a6201ea6ff9f"] =
    [
        "Ketua Subkelompok Penyuluhan dan Hubungan Masyarakat",
        "Ketua Subkelompok Pemberdayaan Masyarakat",
        "Ketua Subkelompok Kemitraan, Data, dan Informasi",
    ],
    // ── Bidang Pengurangan dan Penanganan Sampah ──────────────────────────
    ["08dacde9-6fb2-4da9-8de6-4207712ffa3f"] =
    [
        "Kepala Seksi Pengurangan Sampah",
        "Kepala Seksi Pemilahan dan Pengumpulan Sampah",
        "Kepala Seksi Pengangkutan Sampah",
    ],

    // ── Suku Dinas Jakarta Pusat ──────────────────────────────────────────
    ["77136212-8ad7-48de-8270-69ae949ed895"] =
    [
        "Kepala Subbagian Tata Usaha Sudin Jakpus",
        "Kepala Seksi Pengelolaan Sampah dan Limbah B3 Sudin Jakpus",
        "Kepala Seksi Pengendalian Pencemaran Dan Kerusakan Lingkungan Sudin Jakpus",
        "Kepala Seksi Pengawasan dan Penaatan Hukum Sudin Jakpus",
        "Kepala Seksi Peran Serta Masyarakat Sudin Jakpus",
    ],
    // ── Suku Dinas Jakarta Utara ──────────────────────────────────────────
    ["fcc59e08-cf7f-40e0-b6e3-2ef748b8c218"] =
    [
        "Kepala Subbagian Tata Usaha",
        "Kepala Seksi Pengelolaan Sampah dan Limbah B3",
        "Kepala Seksi Pengendalian Pencemaran Dan Kerusakan Lingkungan",
        "Kepala Seksi Pengawasan dan Penaatan Hukum",
        "Kepala Seksi Peran Serta Masyarakat",
    ],
    // ── Suku Dinas Jakarta Barat ──────────────────────────────────────────
    ["c89321db-b4e5-4638-ba59-f4280030f9fe"] =
    [
        "Kepala Subbagian Tata Usaha",
        "Kepala Seksi Pengelolaan Sampah dan Limbah B3",
        "Kepala Seksi Pengendalian Pencemaran Dan Kerusakan Lingkungan",
        "Kepala Seksi Pengawasan dan Penaatan Hukum",
        "Kepala Seksi Peran Serta Masyarakat",
    ],
    // ── Suku Dinas Jakarta Timur ──────────────────────────────────────────
    ["d47b8252-a7ac-46a1-8d9f-b0e5a8e4ba67"] =
    [
        "Kepala Subbagian Tata Usaha",
        "Kepala Seksi Pengelolaan Sampah dan Limbah B3",
        "Kepala Seksi Pengendalian Pencemaran Dan Kerusakan Lingkungan",
        "Kepala Seksi Pengawasan dan Penaatan Hukum",
        "Kepala Seksi Peran Serta Masyarakat",
    ],
    // ── Suku Dinas Jakarta Selatan ────────────────────────────────────────
    ["b4b04c3b-3f6f-4005-a79b-2b4daba5de1a"] =
    [
        "Kepala Subbagian Tata Usaha",
        "Kepala Seksi Pengelolaan Sampah dan Limbah B3",
        "Kepala Seksi Pengendalian Pencemaran Dan Kerusakan Lingkungan",
        "Kepala Seksi Pengawasan dan Penaatan Hukum",
        "Kepala Seksi Peran Serta Masyarakat",
    ],
    // ── Suku Dinas Kepulauan Seribu ───────────────────────────────────────
    ["529d9d7a-b365-47d5-9f4f-3f2b67326c79"] =
    [
        "Kepala Subbagian Tata Usaha",
        "Kepala Seksi Pengelolaan Sampah dan Limbah B3",
        "Kepala Seksi Pengendalian Pencemaran dan Kerusakan Lingkungan",
        "Kepala Seksi Peran Serta Masyarakat dan Penaatan Hukum",
    ],
};
}
