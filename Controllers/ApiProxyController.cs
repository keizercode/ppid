using Microsoft.AspNetCore.Mvc;

namespace PermintaanData.Controllers;

/// <summary>
/// Proxy ke semua API eksternal sehingga frontend tidak terkena CORS.
/// </summary>
[Route("api")]
public class ApiProxyController(IHttpClientFactory http, IConfiguration cfg) : Controller
{
    // GET /api/provinsi
    [HttpGet("provinsi")]
    public async Task<IActionResult> Provinsi()
    {
        try
        {
            var client = http.CreateClient("WilayahApi");
            var res    = await client.GetStringAsync("/api/provinsi/search");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // GET /api/kabupaten  (DKJ — Jakarta)
    [HttpGet("kabupaten")]
    public async Task<IActionResult> Kabupaten()
    {
        try
        {
            var client = http.CreateClient("WilayahApi");
            var res    = await client.GetStringAsync("/api/kabupaten/dkj/search");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // GET /api/kecamatan?kab=31XX
    [HttpGet("kecamatan")]
    public async Task<IActionResult> Kecamatan([FromQuery] string kab)
    {
        try
        {
            var client = http.CreateClient("WilayahApi");
            var res    = await client.GetStringAsync($"/api/kecamatan/search?kab={kab}");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // GET /api/kelurahan?kec=31XX01
    [HttpGet("kelurahan")]
    public async Task<IActionResult> Kelurahan([FromQuery] string kec)
    {
        try
        {
            var client = http.CreateClient("WilayahApi");
            var res    = await client.GetStringAsync($"/api/kelurahan/search?kec={kec}");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }

    // GET /api/cek-nik?nik=3174XXXXXX
    [HttpGet("cek-nik")]
    public async Task<IActionResult> CekNik([FromQuery] string nik)
    {
        if (string.IsNullOrWhiteSpace(nik) || nik.Length != 16)
            return BadRequest(new { error = "NIK harus 16 digit" });
        try
        {
            var client = http.CreateClient("NikApi");
            var res    = await client.GetStringAsync($"/api/web/cek-nik/?nik={nik}");
            return Content(res, "application/json");
        }
        catch { return Ok("{}"); }
    }

    // GET /api/bidang
    [HttpGet("bidang")]
    public async Task<IActionResult> Bidang()
    {
        try
        {
            var client = http.CreateClient("BidangApi");
            var res    = await client.GetStringAsync("/api/master/bidang/search");
            return Content(res, "application/json");
        }
        catch { return Ok("[]"); }
    }
}
