using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PermintaanData.Models;

namespace PermintaanData.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Pribadi>               Pribadi               { get; set; }
    public DbSet<PribadiPPID>           PribadiPPID           { get; set; }
    public DbSet<PermohonanPPID>        PermohonanPPID        { get; set; }
    public DbSet<PermohonanPPIDDetail>  PermohonanPPIDDetail  { get; set; }
    public DbSet<Keperluan>             Keperluan             { get; set; }
    public DbSet<StatusPPID>            StatusPPID            { get; set; }
    public DbSet<DokumenPPID>           DokumenPPID           { get; set; }
    public DbSet<JenisDokumenPPID>      JenisDokumenPPID      { get; set; }
    public DbSet<JadwalPPID>            JadwalPPID            { get; set; }
    public DbSet<AuditLogPPID>          AuditLog              { get; set; }
    public DbSet<AppUser>               AppUsers              { get; set; }
    public DbSet<SubTaskPPID>           SubTaskPPID           { get; set; }

    protected override void OnModelCreating(ModelBuilder m)
    {
        // ── Pribadi ───────────────────────────────────────────────────────
        m.Entity<Pribadi>().HasKey(e => e.PribadiID);
        m.Entity<Pribadi>()
            .HasOne(e => e.PribadiPPID)
            .WithOne(e => e.Pribadi)
            .HasForeignKey<PribadiPPID>(e => e.PribadiID);

        // ── PermohonanPPID ────────────────────────────────────────────────
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

        // ── SubTaskPPID ───────────────────────────────────────────────────
        m.Entity<SubTaskPPID>()
            .HasOne(e => e.Permohonan)
            .WithMany()
            .HasForeignKey(e => e.PermohonanPPIDID)
            .OnDelete(DeleteBehavior.Cascade);

        m.Entity<SubTaskPPID>()
            .HasIndex(e => e.PermohonanPPIDID);

        m.Entity<SubTaskPPID>()
            .HasIndex(e => new { e.PermohonanPPIDID, e.JenisTask });

        // ── AppUser ───────────────────────────────────────────────────────
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
    // GenerateNoPermohonan
    // ════════════════════════════════════════════════════════════════════════

    public async Task<(string NoPermohonan, int Sequence)> GenerateNoPermohonan(
        string loketJenis = LoketJenis.Kepegawaian)
    {
        var now        = DateTime.UtcNow;
        var year       = now.Year;
        var prefix     = LoketJenis.GetPrefix(loketJenis);
        var romanMonth = NoPermohonanToken.GetRomanMonth(now.Month);

        await using var tx = await Database.BeginTransactionAsync();
        try
        {
            await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(20250002)");

            await Database.ExecuteSqlAsync($"""
                INSERT INTO public."NoPermohonanCounter" ("Year", "LastSeq")
                VALUES ({year}, 1)
                ON CONFLICT ("Year") DO UPDATE
                    SET "LastSeq" = public."NoPermohonanCounter"."LastSeq" + 1;
                """);

            var nextSeq = await Database
            .SqlQuery<int>($"""
                SELECT "LastSeq" AS "Value"
                FROM public."NoPermohonanCounter"
                WHERE "Year" = {year}
            """)
            .FirstAsync();

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

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    public static DateOnly HitungBatasWaktu(DateOnly tanggalPermohonan)
        => tanggalPermohonan.AddDays(14);

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

    // ════════════════════════════════════════════════════════════════════════
    // SubTask Parallel Helpers
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Buat sub-task paralel saat KDI terima disposisi.
    /// Idempotent — aman dipanggil ulang; tidak akan buat duplikat.
    /// </summary>
    public void CreateSubTasks(
        Guid   permohonanId,
        bool   perluData,
        bool   perluObs,
        bool   perluWaw,
        string operatorName)
    {
        var now = DateTime.UtcNow;

        if (perluData)
            SubTaskPPID.Add(new SubTaskPPID
            {
                PermohonanPPIDID = permohonanId,
                JenisTask        = Models.JenisTask.PermintaanData,
                StatusTask       = Models.SubTaskStatus.Pending,
                Operator         = operatorName,
                CreatedAt        = now
            });

        if (perluObs)
            SubTaskPPID.Add(new SubTaskPPID
            {
                PermohonanPPIDID = permohonanId,
                JenisTask        = Models.JenisTask.Observasi,
                StatusTask       = Models.SubTaskStatus.Pending,
                Operator         = operatorName,
                CreatedAt        = now
            });

        if (perluWaw)
            SubTaskPPID.Add(new SubTaskPPID
            {
                PermohonanPPIDID = permohonanId,
                JenisTask        = Models.JenisTask.Wawancara,
                StatusTask       = Models.SubTaskStatus.Pending,
                Operator         = operatorName,
                CreatedAt        = now
            });
    }

    /// <summary>
    /// Cek apakah semua sub-task selesai; jika ya, advance status ke DataSiap.
    /// Return true jika berhasil advance — caller wajib SaveChangesAsync() setelahnya.
    /// </summary>
    public async Task<bool> AdvanceIfAllSubTasksDone(Guid permohonanId, string operatorName)
    {
        var tasks = await SubTaskPPID
            .Where(t => t.PermohonanPPIDID == permohonanId)
            .ToListAsync();

        // Tidak ada subtask → tidak pakai sistem paralel, jangan auto-advance
        if (!tasks.Any())
            return false;

        if (!tasks.All(t => t.StatusTask == Models.SubTaskStatus.Selesai))
            return false;

        var p = await PermohonanPPID.FindAsync(permohonanId);
        if (p == null || p.StatusPPIDID >= Models.StatusId.DataSiap)
            return false;

        var lama       = p.StatusPPIDID;
        p.StatusPPIDID = Models.StatusId.DataSiap;
        p.UpdatedAt    = DateTime.UtcNow;

        AddAuditLog(
            permohonanId,
            lama,
            Models.StatusId.DataSiap,
            $"Semua sub-tugas selesai ({tasks.Count}/{tasks.Count}) — " +
            $"data siap diunduh pemohon.",
            operatorName
        );

        return true;
    }

    /// <summary>Ambil satu SubTask berdasarkan permohonan dan jenis.</summary>
    public async Task<SubTaskPPID?> GetSubTask(Guid permohonanId, string jenisTask)
        => await SubTaskPPID
               .FirstOrDefaultAsync(t =>
                   t.PermohonanPPIDID == permohonanId &&
                   t.JenisTask        == jenisTask);

    // ════════════════════════════════════════════════════════════════════════
    // Dashboard Monthly Stats
    // ════════════════════════════════════════════════════════════════════════

    public async Task<List<MonthlyStatRow>> GetMonthlyStats(int months = 12)
    {
        var from     = DateTime.UtcNow.AddMonths(-(months - 1));
        var fromDate = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var raw = await PermohonanPPID
            .AsNoTracking()
            .Where(p => p.CratedAt >= fromDate)
            .Select(p => new { p.CratedAt, p.StatusPPIDID })
            .ToListAsync();

        return raw
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
