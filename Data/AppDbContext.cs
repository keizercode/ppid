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

        // ── Seed: StatusPPID ──────────────────────────────────────────────────
        m.Entity<StatusPPID>().HasData(
            new StatusPPID { StatusPPIDID =  1, NamaStatusPPID = "Baru" },
            new StatusPPID { StatusPPIDID =  2, NamaStatusPPID = "Terdaftar" },
            new StatusPPID { StatusPPIDID =  3, NamaStatusPPID = "Identifikasi Awal" },
            new StatusPPID { StatusPPIDID =  4, NamaStatusPPID = "Menunggu Surat Izin" },
            new StatusPPID { StatusPPIDID =  5, NamaStatusPPID = "Surat Izin Terbit" },
            new StatusPPID { StatusPPIDID =  6, NamaStatusPPID = "Didisposisi" },
            new StatusPPID { StatusPPIDID =  7, NamaStatusPPID = "Sedang Diproses" },
            new StatusPPID { StatusPPIDID =  8, NamaStatusPPID = "Observasi Dijadwalkan" },
            new StatusPPID { StatusPPIDID =  9, NamaStatusPPID = "Observasi Selesai" },
            new StatusPPID { StatusPPIDID = 10, NamaStatusPPID = "Data Siap" },
            new StatusPPID { StatusPPIDID = 11, NamaStatusPPID = "Selesai" }
        );

        m.Entity<Keperluan>().HasData(
            new Keperluan { KeperluanID = 1, NamaKeperluan = "Observasi" },
            new Keperluan { KeperluanID = 2, NamaKeperluan = "Permintaan Data" },
            new Keperluan { KeperluanID = 3, NamaKeperluan = "Wawancara" }
        );

        m.Entity<JenisDokumenPPID>().HasData(
            new JenisDokumenPPID { JenisDokumenPPIDID = 1, NamaJenisDokumenPPID = "KTP",                        IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 2, NamaJenisDokumenPPID = "Surat Permohonan",           IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 3, NamaJenisDokumenPPID = "Proposal Penelitian",        IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 4, NamaJenisDokumenPPID = "Akta Notaris",               IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 5, NamaJenisDokumenPPID = "Dokumen Identifikasi (TTD)", IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 6, NamaJenisDokumenPPID = "Surat Izin",                 IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 7, NamaJenisDokumenPPID = "Data Hasil",                 IsActive = true }
        );
    }

    public async Task<string> GenerateNoPermohonan()
    {
        var year  = DateTime.UtcNow.Year;
        var count = await PermohonanPPID
            .CountAsync(p => p.CratedAt != null && p.CratedAt.Value.Year == year);
        return $"PPD/{year}/{(count + 1):D4}";
    }
}
