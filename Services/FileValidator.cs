namespace PermintaanData.Services;

/// <summary>
/// Validasi file upload di sisi server.
/// Validasi client-side (atribut HTML accept="...") mudah di-bypass — validasi ini
/// wajib ada di server untuk keamanan yang sesungguhnya.
/// </summary>
public static class FileValidator
{
    // ── Konfigurasi batas ─────────────────────────────────────────────────

    /// <summary>Ukuran maksimum file dokumen (KTP, surat, proposal): 10 MB</summary>
    public const long MaxDocumentSize = 10 * 1024 * 1024;

    /// <summary>Ukuran maksimum file data hasil (ZIP, Excel, dsb.): 50 MB</summary>
    public const long MaxDataSize = 50 * 1024 * 1024;

    // ── Magic bytes (file signature) ──────────────────────────────────────

    private static readonly Dictionary<string, byte[][]> AllowedSignatures = new()
    {
        [".pdf"]  = [[ 0x25, 0x50, 0x44, 0x46 ]],                          // %PDF
        [".jpg"]  = [[ 0xFF, 0xD8, 0xFF ]],                                 // JPEG
        [".jpeg"] = [[ 0xFF, 0xD8, 0xFF ]],
        [".png"]  = [[ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A ]], // PNG
        [".xlsx"] = [[ 0x50, 0x4B, 0x03, 0x04 ]],                          // ZIP (xlsx adalah ZIP)
        [".xls"]  = [[ 0xD0, 0xCF, 0x11, 0xE0 ]],                          // Compound Document
        [".csv"]  = [],                                                       // Plain text — cek ext saja
        [".zip"]  = [[ 0x50, 0x4B, 0x03, 0x04 ]],                          // ZIP
        [".docx"] = [[ 0x50, 0x4B, 0x03, 0x04 ]],                          // ZIP (docx adalah ZIP)
        [".doc"]  = [[ 0xD0, 0xCF, 0x11, 0xE0 ]],                          // Compound Document
    };

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Validasi dokumen standar (KTP, surat, proposal).
    /// Hanya PDF dan gambar yang diizinkan, maksimum 10 MB.
    /// </summary>
    public static ValidationResult ValidateDocument(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return ValidationResult.Ok(); // File opsional

        var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
        return Validate(file, allowedExtensions, MaxDocumentSize);
    }

    /// <summary>
    /// Validasi file data hasil (upload KDI/Produsen Data).
    /// Menerima PDF, Excel, CSV, Word, ZIP, maksimum 50 MB.
    /// </summary>
    public static ValidationResult ValidateDataFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return ValidationResult.Ok(); // File opsional

        var allowedExtensions = new[] { ".pdf", ".xlsx", ".xls", ".csv", ".doc", ".docx", ".zip" };
        return Validate(file, allowedExtensions, MaxDataSize);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private static ValidationResult Validate(
        IFormFile file, string[] allowedExtensions, long maxSize)
    {
        // 1. Cek ukuran
        if (file.Length > maxSize)
            return ValidationResult.Fail(
                $"Ukuran file melebihi batas maksimum ({maxSize / 1024 / 1024} MB).");

        // 2. Cek extension
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return ValidationResult.Fail(
                $"Tipe file tidak diizinkan. Tipe yang diterima: {string.Join(", ", allowedExtensions)}.");

        // 3. Cek magic bytes (file signature) — mencegah extension spoofing
        if (!HasValidSignature(file, ext))
            return ValidationResult.Fail(
                "File tidak valid atau tipe file tidak sesuai dengan isinya.");

        // 4. Sanitasi nama file — cegah path traversal
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return ValidationResult.Fail("Nama file mengandung karakter yang tidak diizinkan.");

        return ValidationResult.Ok();
    }

    private static bool HasValidSignature(IFormFile file, string ext)
    {
        if (!AllowedSignatures.TryGetValue(ext, out var signatures))
            return false;

        // CSV tidak punya magic bytes yang khas, cukup cek extension
        if (signatures.Length == 0)
            return true;

        using var stream = file.OpenReadStream();
        var maxHeaderLen = signatures.Max(s => s.Length);
        var header       = new byte[maxHeaderLen];
        var bytesRead    = stream.Read(header, 0, maxHeaderLen);

        return signatures.Any(sig =>
            bytesRead >= sig.Length &&
            header.Take(sig.Length).SequenceEqual(sig));
    }
}

/// <summary>Hasil validasi file.</summary>
public record ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Ok()             => new(true,  null);
    public static ValidationResult Fail(string msg) => new(false, msg);
}
