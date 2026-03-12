using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Models;

namespace PermintaanData.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Pribadi> Pribadi { get; set; }
    public DbSet<PribadiPPID> PribadiPPID { get; set; }
    public DbSet<PermohonanPPID> PermohonanPPID { get; set; }
    public DbSet<PermohonanPPIDDetail> PermohonanPPIDDetail { get; set; }
    public DbSet<Keperluan> Keperluan { get; set; }
    public DbSet<StatusPPID> StatusPPID { get; set; }
    public DbSet<DokumenPPID> DokumenPPID { get; set; }
    public DbSet<JenisDokumenPPID> JenisDokumenPPID { get; set; }
    public DbSet<JadwalPPID> JadwalPPID { get; set; }
    public DbSet<AuditLogPPID> AuditLog { get; set; }
    public DbSet<AppUser> AppUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Pribadi>().HasKey(e => e.PribadiID);
        m.Entity<Pribadi>()
            .HasOne(e => e.PribadiPPID)
            .WithOne(e => e.Pribadi)
            .HasForeignKey<PribadiPPID>(e => e.PribadiID);

        m.Entity<PermohonanPPID>()
            .HasOne(e => e.Status)
            .WithMany()
            .HasForeignKey(e => e.StatusPPIDID);

        m.Entity<PermohonanPPID>()
            .HasMany(e => e.Detail)
            .WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);

        m.Entity<PermohonanPPID>()
            .HasMany(e => e.Dokumen)
            .WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);

        m.Entity<PermohonanPPID>()
            .HasMany(e => e.Jadwal)
            .WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);

        m.Entity<PermohonanPPID>()
            .HasMany(e => e.AuditLog)
            .WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);

        m.Entity<PermohonanPPID>()
            .HasIndex(e => e.NoPermohonan)
            .IsUnique();

        m.Entity<AppUser>()
            .HasIndex(e => e.Username)
            .IsUnique();

        // ── Seed: StatusPPID ──────────────────────────────────────────────
        m.Entity<StatusPPID>().HasData(
            new StatusPPID { StatusPPIDID = 1, NamaStatusPPID = "Baru" },
            new StatusPPID { StatusPPIDID = 2, NamaStatusPPID = "Terdaftar" },
            new StatusPPID { StatusPPIDID = 3, NamaStatusPPID = "Identifikasi Awal" },
            new StatusPPID { StatusPPIDID = 4, NamaStatusPPID = "Menunggu Surat Izin" },
            new StatusPPID { StatusPPIDID = 5, NamaStatusPPID = "Surat Izin Terbit" },
            new StatusPPID { StatusPPIDID = 6, NamaStatusPPID = "Didisposisi" },
            new StatusPPID { StatusPPIDID = 7, NamaStatusPPID = "Sedang Diproses" },
            new StatusPPID { StatusPPIDID = 8, NamaStatusPPID = "Observasi Dijadwalkan" },
            new StatusPPID { StatusPPIDID = 9, NamaStatusPPID = "Observasi Selesai" },
            new StatusPPID { StatusPPIDID = 10, NamaStatusPPID = "Data Siap" },
            new StatusPPID { StatusPPIDID = 11, NamaStatusPPID = "Selesai" },
            new StatusPPID { StatusPPIDID = 12, NamaStatusPPID = "Wawancara Dijadwalkan" },
            new StatusPPID { StatusPPIDID = 13, NamaStatusPPID = "Wawancara Selesai" }
        );

        m.Entity<Keperluan>().HasData(
            new Keperluan { KeperluanID = 1, NamaKeperluan = "Observasi" },
            new Keperluan { KeperluanID = 2, NamaKeperluan = "Permintaan Data" },
            new Keperluan { KeperluanID = 3, NamaKeperluan = "Wawancara" }
        );

        m.Entity<JenisDokumenPPID>().HasData(
            new JenisDokumenPPID { JenisDokumenPPIDID = 1, NamaJenisDokumenPPID = "KTP", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 2, NamaJenisDokumenPPID = "Surat Permohonan", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 3, NamaJenisDokumenPPID = "Proposal Penelitian", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 4, NamaJenisDokumenPPID = "Akta Notaris", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 5, NamaJenisDokumenPPID = "Dokumen Identifikasi (TTD)", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 6, NamaJenisDokumenPPID = "Surat Izin", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 7, NamaJenisDokumenPPID = "Data Hasil", IsActive = true }
        );
    }

    // ════════════════════════════════════════════════════════════════════════
    // GenerateNoPermohonan — atomic dengan transaksi
    // ════════════════════════════════════════════════════════════════════════

    public async Task<string> GenerateNoPermohonan()
    {
        var year = DateTime.UtcNow.Year;

        await using var tx = await Database.BeginTransactionAsync();
        try
        {
            var lastSeq = await PermohonanPPID
                .Where(p => p.CratedAt != null && p.CratedAt.Value.Year == year)
                .OrderByDescending(p => p.Sequance)
                .Select(p => p.Sequance)
                .FirstOrDefaultAsync() ?? 0;

            var nextSeq = lastSeq + 1;
            await tx.CommitAsync();

            return $"PPD/{year}/{nextSeq:D4}";
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── Helper Audit Log ──────────────────────────────────────────────────────

    public void AddAuditLog(Guid permohonanId, int? statusLama, int statusBaru,
        string keterangan, string operator_)
    {
        AuditLog.Add(new AuditLogPPID
        {
            PermohonanPPIDID = permohonanId,
            StatusLama = statusLama,
            StatusBaru = statusBaru,
            Keterangan = keterangan,
            Operator = operator_,
            CreatedAt = DateTime.UtcNow
        });
    }
}

// ════════════════════════════════════════════════════════════════════════════
// AppUser — simple auth tanpa BCrypt (pakai SHA-256 + salt bawaan .NET)
// Tidak perlu install package tambahan sama sekali.
// ════════════════════════════════════════════════════════════════════════════

[System.ComponentModel.DataAnnotations.Schema.Table("AppUser", Schema = "public")]
public class AppUser
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.Column("AppUserID")]
    public int AppUserID { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("Username")]
    public string Username { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.Column("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.Column("Role")]
    public string Role { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.Column("NamaLengkap")]
    public string NamaLengkap { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Schema.Column("IsActive")]
    public bool IsActive { get; set; } = true;

    [System.ComponentModel.DataAnnotations.Schema.Column("CreatedAt")]
    public DateTime? CreatedAt { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("UpdatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Hash password dengan SHA-256 + salt tetap.
    /// Format simpan: "PPID_v1:{hash}"
    /// </summary>
    public static string HashPassword(string password)
    {
        // Salt tetap — cukup untuk proyek internal.
        // Jika ingin lebih aman, simpan salt per-user di kolom terpisah.
        const string salt = "PPID_DLH_JKT_2025";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        return "PPID_v1:" + Convert.ToHexString(bytes);
    }

    public bool VerifyPassword(string password)
        => PasswordHash == HashPassword(password);
}
