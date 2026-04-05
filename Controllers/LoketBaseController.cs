using Microsoft.AspNetCore.Mvc;
using PermintaanData.Data;
using PermintaanData.Models;
using PermintaanData.Services;

namespace PermintaanData.Controllers;

/// <summary>
/// Base controller untuk semua controller Loket (Kepegawaian dan Umum).
/// Menyediakan helper upload dokumen dengan validasi server-side dan
/// utilitas CSV export yang digunakan bersama oleh semua loket controller.
/// </summary>
public abstract class LoketBaseController(AppDbContext db, IWebHostEnvironment env) : Controller
{
    protected string UploadsRoot =>
        Path.Combine(
            string.IsNullOrEmpty(env.WebRootPath)
                ? Path.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath,
            "uploads");

    // ── Upload helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Upload dokumen langsung ke direktori final dengan validasi server-side.
    /// Kembalikan pesan error jika validasi gagal; null jika berhasil atau file kosong.
    /// </summary>
    protected async Task<string?> UploadDokumen(
        Guid permohonanId, IFormFile? file,
        int jenisDokId, string nama, DateTime now)
    {
        if (file == null || file.Length == 0) return null;

        var validation = FileValidator.ValidateDocument(file);
        if (!validation.IsValid) return validation.ErrorMessage;

        var uploadDir = Path.Combine(UploadsRoot, permohonanId.ToString());
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{jenisDokId}_{Path.GetFileName(file.FileName)}";
        await using var stream = new FileStream(Path.Combine(uploadDir, fileName), FileMode.Create);
        await file.CopyToAsync(stream);

        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID     = permohonanId,
            NamaDokumenPPID      = nama,
            UploadDokumenPPID    = $"/uploads/{permohonanId}/{fileName}",
            JenisDokumenPPIDID   = jenisDokId,
            NamaJenisDokumenPPID = nama,
            CreatedAt            = now
        });

        return null;
    }

    /// <summary>
    /// Stage dokumen ke direktori sementara selama transaksi berlangsung,
    /// lalu pindahkan ke direktori final setelah commit berhasil.
    /// Kembalikan pesan error jika validasi gagal; null jika berhasil atau file kosong.
    /// </summary>
    protected async Task<string?> StageDokumen(
        Guid permohonanId, IFormFile? file,
        int jenisDokId, string nama, DateTime now,
        string tempDir, List<(string Temp, string Final)> movers)
    {
        if (file == null || file.Length == 0) return null;

        var validation = FileValidator.ValidateDocument(file);
        if (!validation.IsValid) return validation.ErrorMessage;

        var fileName  = $"{jenisDokId}_{Path.GetFileName(file.FileName)}";
        var tempPath  = Path.Combine(tempDir, fileName);
        var finalPath = Path.Combine(UploadsRoot, permohonanId.ToString(), fileName);

        await using var stream = new FileStream(tempPath, FileMode.Create);
        await file.CopyToAsync(stream);
        movers.Add((tempPath, finalPath));

        db.DokumenPPID.Add(new DokumenPPID
        {
            PermohonanPPIDID     = permohonanId,
            NamaDokumenPPID      = nama,
            UploadDokumenPPID    = $"/uploads/{permohonanId}/{fileName}",
            JenisDokumenPPIDID   = jenisDokId,
            NamaJenisDokumenPPID = nama,
            CreatedAt            = now
        });

        return null;
    }

    /// <summary>Pindahkan semua file dari temp ke direktori final setelah commit DB berhasil.</summary>
    protected static void CommitStagedFiles(List<(string Temp, string Final)> movers)
    {
        foreach (var (temp, final) in movers)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(final)!);
            System.IO.File.Move(temp, final, overwrite: true);
        }
    }

    // ── CSV helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Escape dan quote nilai untuk CSV export.
    /// Mencegah CSV injection dengan prefix apostrof pada formula cell.
    /// </summary>
    protected static string CsvQuote(string? s)
    {
        if (s is null) return string.Empty;
        // Cegah CSV injection: formula cells dimulai dengan =, +, -, @
        if (s.Length > 0 && "=+-@".Contains(s[0]))
            s = "'" + s;
        return $"\"{s.Replace("\"", "\"\"")}\"";
    }

    /// <summary>Export list permohonan ke FileContentResult CSV dengan BOM UTF-8.</summary>
    protected static FileContentResult ToCsvFile(string csvContent, string filename)
    {
        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csvContent))
            .ToArray();
        return new FileContentResult(bytes, "text/csv")
        {
            FileDownloadName = $"{filename}_{DateTime.Now:yyyyMMdd}.csv"
        };
    }
}
