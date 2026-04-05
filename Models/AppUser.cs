using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PermintaanData.Models;

/// <summary>
/// Pengguna internal sistem PPID.
/// Password disimpan menggunakan BCrypt (work factor 12) — aman terhadap brute-force
/// dan rainbow table attack meskipun database bocor.
/// </summary>
[Table("AppUser", Schema = "public")]
public class AppUser
{
    [Key, Column("AppUserID")]  public int     AppUserID    { get; set; }
    [Column("Username")]        public string  Username     { get; set; } = string.Empty;
    [Column("PasswordHash")]    public string  PasswordHash { get; set; } = string.Empty;
    [Column("Role")]            public string  Role         { get; set; } = string.Empty;
    [Column("NamaLengkap")]     public string  NamaLengkap  { get; set; } = string.Empty;
    [Column("IsActive")]        public bool    IsActive     { get; set; } = true;
    [Column("CreatedAt")]       public DateTime? CreatedAt  { get; set; }
    [Column("UpdatedAt")]       public DateTime? UpdatedAt  { get; set; }

    /// <summary>
    /// Hash password menggunakan BCrypt dengan work factor 12.
    /// Setiap hash mengandung salt unik yang di-generate secara random — aman.
    /// </summary>
    public static string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    /// <summary>
    /// Verifikasi password terhadap hash yang tersimpan.
    /// Juga mendukung hash lama format PPID_v1 (SHA256) untuk migrasi bertahap.
    /// </summary>
    public bool VerifyPassword(string password)
    {
        // Hash baru menggunakan BCrypt
        if (PasswordHash.StartsWith("$2"))
            return BCrypt.Net.BCrypt.Verify(password, PasswordHash);

        // Hash lama (format PPID_v1) — masih diterima tapi akan di-upgrade saat login
        if (PasswordHash.StartsWith("PPID_v1:"))
            return VerifyLegacyPassword(password);

        return false;
    }

    /// <summary>
    /// Cek apakah hash sudah menggunakan BCrypt (format baru).
    /// Digunakan untuk upgrade hash lama ke BCrypt saat user berhasil login.
    /// </summary>
    public bool IsLegacyHash => !PasswordHash.StartsWith("$2");

    // ── Private helper ────────────────────────────────────────────────────

    private bool VerifyLegacyPassword(string password)
    {
        // Reproduksi hash SHA256 lama untuk perbandingan
        var bytes    = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("PPID_DLH_JKT_2025" + password));
        var expected = "PPID_v1:" + Convert.ToHexString(bytes);
        return PasswordHash == expected;
    }
}
