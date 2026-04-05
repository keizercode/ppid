using Microsoft.EntityFrameworkCore;
using PermintaanData.Models;

namespace PermintaanData.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Pribadi>              Pribadi              { get; set; }
    public DbSet<PribadiPPID>          PribadiPPID          { get; set; }
    public DbSet<PermohonanPPID>       PermohonanPPID       { get; set; }
    public DbSet<PermohonanPPIDDetail> PermohonanPPIDDetail { get; set; }
    public DbSet<Keperluan>            Keperluan            { get; set; }
    public DbSet<StatusPPID>           StatusPPID           { get; set; }
    public DbSet<DokumenPPID>          DokumenPPID          { get; set; }
    public DbSet<JenisDokumenPPID>     JenisDokumenPPID     { get; set; }
    public DbSet<JadwalPPID>           JadwalPPID           { get; set; }
    public DbSet<AuditLogPPID>         AuditLog             { get; set; }
    public DbSet<AppUser>              AppUsers             { get; set; }
    public DbSet<SubTaskPPID>          SubTaskPPID          { get; set; }

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
            .HasOne(e => e.Status).WithMany()
            .HasForeignKey(e => e.StatusPPIDID);
        m.Entity<PermohonanPPID>()
            .HasMany(e => e.Detail).WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);
        m.Entity<PermohonanPPID>()
            .HasMany(e => e.Dokumen).WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);
        m.Entity<PermohonanPPID>()
            .HasMany(e => e.Jadwal).WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);
        m.Entity<PermohonanPPID>()
            .HasMany(e => e.AuditLog).WithOne(e => e.Permohonan)
            .HasForeignKey(e => e.PermohonanPPIDID);
        m.Entity<PermohonanPPID>()
            .HasIndex(e => e.NoPermohonan).IsUnique();

        // ── SubTaskPPID ───────────────────────────────────────────────────
        m.Entity<SubTaskPPID>()
            .HasOne(e => e.Permohonan).WithMany()
            .HasForeignKey(e => e.PermohonanPPIDID)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<SubTaskPPID>()
            .HasIndex(e => e.PermohonanPPIDID);
        m.Entity<SubTaskPPID>()
            .HasIndex(e => new { e.PermohonanPPIDID, e.JenisTask });

        // ── AppUser ───────────────────────────────────────────────────────
        m.Entity<AppUser>()
            .HasIndex(e => e.Username).IsUnique();

        // ── SEED DATA ─────────────────────────────────────────────────────
        // CATATAN PENTING: HasData() di sini HARUS SYNC dengan migration snapshot.
        // Jangan tambah/ubah seed di sini tanpa membuat migration baru.
        // Gunakan migration SQL ON CONFLICT DO NOTHING untuk data operasional.
        SeedStatusPPID(m);
        SeedKeperluan(m);
        SeedJenisDokumen(m);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SEED DATA — dikelola via EF Core migrations
    // ════════════════════════════════════════════════════════════════════════

    private static void SeedStatusPPID(ModelBuilder m) =>
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

    private static void SeedKeperluan(ModelBuilder m) =>
        m.Entity<Keperluan>().HasData(
            new Keperluan { KeperluanID = 1, NamaKeperluan = "Observasi" },
            new Keperluan { KeperluanID = 2, NamaKeperluan = "Permintaan Data" },
            new Keperluan { KeperluanID = 3, NamaKeperluan = "Wawancara" }
        );

    private static void SeedJenisDokumen(ModelBuilder m) =>
        m.Entity<JenisDokumenPPID>().HasData(
            new JenisDokumenPPID { JenisDokumenPPIDID = 1, NamaJenisDokumenPPID = "KTP",                        IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 2, NamaJenisDokumenPPID = "Surat Permohonan",           IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 3, NamaJenisDokumenPPID = "Proposal Penelitian",        IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 4, NamaJenisDokumenPPID = "Akta Notaris",               IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 5, NamaJenisDokumenPPID = "Dokumen Identifikasi (TTD)", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 6, NamaJenisDokumenPPID = "Surat Izin",                 IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 7, NamaJenisDokumenPPID = "Data Hasil",                 IsActive = true }
        );

    // ════════════════════════════════════════════════════════════════════════
    // NOPERMOHONAN GENERATOR
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generate nomor permohonan unik secara atomic menggunakan advisory lock PostgreSQL.
    /// Format: MHS687592/PPID/III/2026 (Kepegawaian) | UMM687592/PPID/III/2026 (Umum)
    /// </summary>
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
            // Advisory lock per tahun — mencegah race condition pada counter
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

            const int maxAttempts = 10;
            string noPermohonan   = string.Empty;

            for (int attempt = 1; ; attempt++)
            {
                var digits   = NoPermohonanToken.GenerateDigits();
                noPermohonan = $"{prefix}{digits}/PPID/{romanMonth}/{year}";

                bool taken = await PermohonanPPID.AnyAsync(p => p.NoPermohonan == noPermohonan);
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
    // HELPER METHODS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>Batas waktu default: tanggal permohonan + 14 hari kalender.</summary>
    public static DateOnly HitungBatasWaktu(DateOnly tanggalPermohonan) =>
        tanggalPermohonan.AddDays(14);

    /// <summary>Tambahkan entri audit log ke context (belum disimpan ke DB).</summary>
    public void AddAuditLog(
        Guid permohonanId, int? statusLama, int statusBaru,
        string keterangan, string operatorName)
    {
        AuditLog.Add(new AuditLogPPID
        {
            PermohonanPPIDID = permohonanId,
            StatusLama       = statusLama,
            StatusBaru       = statusBaru,
            Keterangan       = keterangan,
            Operator         = operatorName,
            CreatedAt        = DateTime.UtcNow
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    // SUBTASK PARALLEL HELPERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Buat sub-task paralel saat KDI menerima disposisi.
    /// Idempotent — tidak membuat duplikat jika dipanggil ulang.
    /// </summary>
    public void CreateSubTasks(
        Guid permohonanId, bool perluData, bool perluObs,
        bool perluWaw, string operatorName)
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
    /// Kembalikan true jika berhasil advance.
    /// </summary>
    public async Task<bool> AdvanceIfAllSubTasksDone(Guid permohonanId, string operatorName)
    {
        var tasks = await SubTaskPPID
            .Where(t => t.PermohonanPPIDID == permohonanId)
            .ToListAsync();

        if (tasks.Count == 0 || !tasks.All(t => t.StatusTask == Models.SubTaskStatus.Selesai))
            return false;

        var p = await PermohonanPPID.FindAsync(permohonanId);
        if (p is null || p.StatusPPIDID >= Models.StatusId.DataSiap)
            return false;

        var lama       = p.StatusPPIDID;
        p.StatusPPIDID = Models.StatusId.DataSiap;
        p.UpdatedAt    = DateTime.UtcNow;

        AddAuditLog(
            permohonanId, lama, Models.StatusId.DataSiap,
            $"Semua sub-tugas selesai ({tasks.Count}/{tasks.Count}) — data siap diunduh pemohon.",
            operatorName);

        return true;
    }

    /// <summary>Ambil satu SubTask berdasarkan permohonan dan jenis.</summary>
    public async Task<SubTaskPPID?> GetSubTask(Guid permohonanId, string jenisTask) =>
        await SubTaskPPID.FirstOrDefaultAsync(t =>
            t.PermohonanPPIDID == permohonanId &&
            t.JenisTask        == jenisTask);

    // ════════════════════════════════════════════════════════════════════════
    // DASHBOARD MONTHLY STATS
    // ════════════════════════════════════════════════════════════════════════

    public async Task<List<MonthlyStatRow>> GetMonthlyStats()
    {
        const int months = 12;
        var from         = DateTime.UtcNow.AddMonths(-(months - 1));
        var fromDate     = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);

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

public record MonthlyStatRow
{
    public string Label   { get; init; } = string.Empty;
    public int    Total   { get; init; }
    public int    Proses  { get; init; }
    public int    Selesai { get; init; }
}
