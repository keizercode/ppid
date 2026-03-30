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
            new StatusPPID { StatusPPIDID = 1,  NamaStatusPPID = "Baru" },
            new StatusPPID { StatusPPIDID = 2,  NamaStatusPPID = "Terdaftar" },
            new StatusPPID { StatusPPIDID = 3,  NamaStatusPPID = "Identifikasi Awal" },
            new StatusPPID { StatusPPIDID = 4,  NamaStatusPPID = "Menunggu Surat Izin" },
            new StatusPPID { StatusPPIDID = 5,  NamaStatusPPID = "Surat Izin Terbit" },
            new StatusPPID { StatusPPIDID = 6,  NamaStatusPPID = "Didisposisi" },
            new StatusPPID { StatusPPIDID = 7,  NamaStatusPPID = "Sedang Diproses" },
            new StatusPPID { StatusPPIDID = 8,  NamaStatusPPID = "Observasi Dijadwalkan" },
            new StatusPPID { StatusPPIDID = 9,  NamaStatusPPID = "Observasi Selesai" },
            new StatusPPID { StatusPPIDID = 10, NamaStatusPPID = "Data Siap" },
            new StatusPPID { StatusPPIDID = 11, NamaStatusPPID = "Selesai" },
            new StatusPPID { StatusPPIDID = 12, NamaStatusPPID = "Wawancara Dijadwalkan" },
            new StatusPPID { StatusPPIDID = 13, NamaStatusPPID = "Wawancara Selesai" },
            new StatusPPID { StatusPPIDID = 14, NamaStatusPPID = "Menunggu Verifikasi Kasubkel" },
            new StatusPPID { StatusPPIDID = 15, NamaStatusPPID = "Pengisian Feedback Pemohon" }
        );

        m.Entity<Keperluan>().HasData(
            new Keperluan { KeperluanID = 1, NamaKeperluan = "Observasi" },
            new Keperluan { KeperluanID = 2, NamaKeperluan = "Permintaan Data" },
            new Keperluan { KeperluanID = 3, NamaKeperluan = "Wawancara" }
        );

        m.Entity<JenisDokumenPPID>().HasData(
            new JenisDokumenPPID { JenisDokumenPPIDID = 1, NamaJenisDokumenPPID = "KTP",                         IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 2, NamaJenisDokumenPPID = "Surat Permohonan",            IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 3, NamaJenisDokumenPPID = "Proposal Penelitian",         IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 4, NamaJenisDokumenPPID = "Akta Notaris",                IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 5, NamaJenisDokumenPPID = "Dokumen Identifikasi (TTD)",  IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 6, NamaJenisDokumenPPID = "Surat Izin",                  IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 7, NamaJenisDokumenPPID = "Data Hasil",                  IsActive = true }
        );
    }

    // ════════════════════════════════════════════════════════════════════════
    // GenerateNoPermohonan — FORMAT BARU: MHS687592/PPID/III/2026
    //
    // Prefix:
    //   MHS = Mahasiswa  (LoketJenis.Kepegawaian)
    //   UMM = Umum       (LoketJenis.Umum)
    //
    // Struktur:
    //   [PREFIX][6 digit random]/PPID/[Bulan Romawi]/[Tahun]
    //
    // Collision guard:
    //   pg_advisory_xact_lock + UNIQUE index sebagai safety net.
    // ════════════════════════════════════════════════════════════════════════

    public async Task<(string NoPermohonan, int Sequence)> GenerateNoPermohonan(
        string loketJenis = LoketJenis.Kepegawaian)
    {
        var now       = DateTime.UtcNow;
        var year      = now.Year;
        var prefix    = LoketJenis.GetPrefix(loketJenis);
        var romanMonth = NoPermohonanToken.GetRomanMonth(now.Month);

        await using var tx = await Database.BeginTransactionAsync();
        try
        {
            // Serialize antar transaksi di instance Postgres yang sama
            await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(20250002)");

            // ── Increment sequence counter ────────────────────────────────
            await Database.ExecuteSqlRawAsync($"""
                INSERT INTO public."NoPermohonanCounter" ("Year", "LastSeq")
                VALUES ({year}, 1)
                ON CONFLICT ("Year") DO UPDATE
                    SET "LastSeq" = public."NoPermohonanCounter"."LastSeq" + 1;
                """);

            var nextSeq = await Database
                .SqlQueryRaw<int>($"""
                    SELECT "LastSeq" AS "Value"
                    FROM public."NoPermohonanCounter"
                    WHERE "Year" = {year}
                    """)
                .FirstAsync();

            // ── Generate NoPermohonan dengan retry on collision ───────────
            string noPermohonan;
            const int maxAttempts = 10;
            for (int attempt = 1; ; attempt++)
            {
                var digits = NoPermohonanToken.GenerateDigits();
                noPermohonan = $"{prefix}{digits}/PPID/{romanMonth}/{year}";

                bool taken = await PermohonanPPID
                    .AnyAsync(p => p.NoPermohonan == noPermohonan);

                if (!taken) break;

                if (attempt >= maxAttempts)
                    throw new InvalidOperationException(
                        $"Gagal membuat NoPermohonan unik setelah {maxAttempts} percobaan.");
            }

            await tx.CommitAsync();
            return (noPermohonan, nextSeq);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ── Helper: hitung batas waktu (10 hari kerja ≈ 14 hari kalender) ───────

    public static DateOnly HitungBatasWaktu(DateOnly tanggalPermohonan)
    {
        // Sederhana: +14 hari kalender. Untuk perhitungan hari kerja sejati,
        // perlu tabel libur nasional — ini bisa dikembangkan kemudian.
        return tanggalPermohonan.AddDays(14);
    }

    // ── Helper Audit Log ──────────────────────────────────────────────────

    public void AddAuditLog(Guid permohonanId, int? statusLama, int statusBaru,
        string keterangan, string operator_)
    {
        AuditLog.Add(new AuditLogPPID
        {
            PermohonanPPIDID = permohonanId,
            StatusLama       = statusLama,
            StatusBaru       = statusBaru,
            Keterangan       = keterangan,
            Operator         = operator_,
            CreatedAt        = DateTime.UtcNow
        });
    }

    // ── Dashboard Monthly Stats ──────────────────────────────────────────────

    /// <summary>
    /// Query stat bulanan untuk chart dashboard — 12 bulan terakhir.
    /// Dikembalikan sebagai list anonim yang bisa di-serialize ke JSON.
    /// </summary>
    public async Task<List<MonthlyStatRow>> GetMonthlyStats(int months = 12)
    {
        var from = DateTime.UtcNow.AddMonths(-(months - 1));
        var fromDate = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var raw = await PermohonanPPID
            .AsNoTracking()
            .Where(p => p.CratedAt >= fromDate)
            .Select(p => new
            {
                p.CratedAt,
                p.StatusPPIDID
            })
            .ToListAsync();

        // Group in memory (simpler than complex EF Core date grouping)
        var groups = raw
            .GroupBy(p => new { p.CratedAt!.Value.Year, p.CratedAt!.Value.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyStatRow
            {
                Label   = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yy"),
                Total   = g.Count(),
                Proses  = g.Count(p => StatusId.IsProses(p.StatusPPIDID)),
                Selesai = g.Count(p => StatusId.IsSelesai(p.StatusPPIDID))
            })
            .ToList();

        return groups;
    }
}

public record MonthlyStatRow(string Label = "", int Total = 0, int Proses = 0, int Selesai = 0)
{
    public MonthlyStatRow() : this("", 0, 0, 0) { }
    public string Label   { get; init; } = Label;
    public int    Total   { get; init; } = Total;
    public int    Proses  { get; init; } = Proses;
    public int    Selesai { get; init; } = Selesai;
}

// ════════════════════════════════════════════════════════════════════════════
// AppUser
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

    public static string HashPassword(string password)
    {
        const string salt = "PPID_DLH_JKT_2025";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        return "PPID_v1:" + Convert.ToHexString(bytes);
    }

    public bool VerifyPassword(string password)
        => PasswordHash == HashPassword(password);
}
