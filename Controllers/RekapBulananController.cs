using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PermintaanData.Data;
using PermintaanData.Helpers;
using PermintaanData.Models;
using PermintaanData.Models.ViewModels;

namespace PermintaanData.Controllers;

/// <summary>Rekap bulanan permohonan — reusable di semua role loket/kasubkel.</summary>
public class RekapBulananController(AppDbContext db) : Controller
{
    [HttpGet("petugas-loket/rekap-bulanan")]
    [Authorize(Roles = $"{AppRoles.Loket},{AppRoles.Admin}")]
    public Task<IActionResult> LoketKepegawaian(int? year, int? month)
        => Render(RekapBulananScope.LoketKepegawaian, "/petugas-loket/rekap-bulanan",
            "Loket Kepegawaian", "Petugas Loket", year, month);

    [HttpGet("loket-umum/rekap-bulanan")]
    [Authorize(Roles = $"{AppRoles.LoketUmum},{AppRoles.Admin}")]
    public Task<IActionResult> LoketUmum(int? year, int? month)
        => Render(RekapBulananScope.LoketUmum, "/loket-umum/rekap-bulanan",
            "Loket Umum", "Loket Umum", year, month);

    [HttpGet("kasubkel-kepegawaian/rekap-bulanan")]
    [Authorize(Roles = $"{AppRoles.KasubkelKepegawaian},{AppRoles.Admin}")]
    public Task<IActionResult> KasubkelKepegawaian(int? year, int? month)
        => Render(RekapBulananScope.KasubkelKepegawaian, "/kasubkel-kepegawaian/rekap-bulanan",
            "Kasubkel Kepegawaian", "Kasubkel Kepegawaian", year, month);

    [HttpGet("kasubkel-kdi/rekap-bulanan")]
    [Authorize(Roles = $"{AppRoles.KasubkelKDI},{AppRoles.Admin}")]
    public Task<IActionResult> KasubkelKdi(int? year, int? month)
        => Render(RekapBulananScope.KasubkelKdi, "/kasubkel-kdi/rekap-bulanan",
            "Kasubkel KDI", "Kasubkel KDI", year, month);

    private async Task<IActionResult> Render(
        RekapBulananScope scope, string routePrefix, string scopeTitle, string role,
        int? year, int? month)
    {
        var now  = DateTime.Today;
        var y    = year  ?? now.Year;
        var m    = month ?? now.Month;

        var vm = await db.BuildRekapBulanan(scope, y, m);
        vm.RoutePrefix = routePrefix;
        vm.ScopeTitle  = scopeTitle;

        ViewData["Title"] = "Rekap Bulanan";
        ViewData["Role"]  = role;
        return View("~/Views/Shared/RekapBulanan.cshtml", vm);
    }
}
