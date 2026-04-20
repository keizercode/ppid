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
    public DbSet<FeedbackTaskPPID>     FeedbackTaskPPID     { get; set; }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Pribadi>().HasKey(e => e.PribadiID);
        m.Entity<Pribadi>()
            .HasOne(e => e.PribadiPPID)
            .WithOne(e => e.Pribadi)
            .HasForeignKey<PribadiPPID>(e => e.PribadiID);

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

        m.Entity<FeedbackTaskPPID>()
            .HasOne(e => e.Permohonan).WithMany()
            .HasForeignKey(e => e.PermohonanPPIDID)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<FeedbackTaskPPID>()
            .HasIndex(e => e.PermohonanPPIDID);
        m.Entity<FeedbackTaskPPID>()
            .HasIndex(e => new { e.PermohonanPPIDID, e.JenisTask }).IsUnique();

        m.Entity<SubTaskPPID>()
            .HasOne(e => e.Permohonan).WithMany()
            .HasForeignKey(e => e.PermohonanPPIDID)
            .OnDelete(DeleteBehavior.Cascade);
        m.Entity<SubTaskPPID>()
            .HasIndex(e => e.PermohonanPPIDID);
        m.Entity<SubTaskPPID>()
            .HasIndex(e => new { e.PermohonanPPIDID, e.JenisTask });

        m.Entity<AppUser>()
            .HasIndex(e => e.Username).IsUnique();

        SeedStatusPPID(m);
        SeedKeperluan(m);
        SeedJenisDokumen(m);
    }

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
            new StatusPPID { StatusPPIDID = 15, NamaStatusPPID = "Pengisian Feedback Pemohon" },
            new StatusPPID { StatusPPIDID = 16, NamaStatusPPID = "Dibatalkan" }
        );

    private static void SeedKeperluan(ModelBuilder m) =>
        m.Entity<Keperluan>().HasData(
            new Keperluan { KeperluanID = 1, NamaKeperluan = "Observasi" },
            new Keperluan { KeperluanID = 2, NamaKeperluan = "Permintaan Data" },
            new Keperluan { KeperluanID = 3, NamaKeperluan = "Wawancara" }
        );

    private static void SeedJenisDokumen(ModelBuilder m) =>
        m.Entity<JenisDokumenPPID>().HasData(
            new JenisDokumenPPID { JenisDokumenPPIDID = 1, NamaJenisDokumenPPID = "KTP",                          IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 2, NamaJenisDokumenPPID = "Surat Permohonan",             IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 3, NamaJenisDokumenPPID = "Proposal Penelitian",          IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 4, NamaJenisDokumenPPID = "Akta Notaris",                 IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 5, NamaJenisDokumenPPID = "Dokumen Identifikasi (TTD)",   IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 6, NamaJenisDokumenPPID = "Surat Izin",                   IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 7, NamaJenisDokumenPPID = "Data Hasil",                   IsActive = true },
            new JenisDokumenPPID { JenisDokumenPPIDID = 8, NamaJenisDokumenPPID = "Tugas / Laporan Final Pemohon",IsActive = true }  // ← NEW
        );

    // ════════════════════════════════════════════════════════════════════════
    // NOPERMOHONAN GENERATOR
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

    public static DateOnly HitungBatasWaktu(DateOnly tanggalPermohonan) =>
        tanggalPermohonan.AddDays(14);

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

    // ════════════════════════════════════════════════════════════════════════
    // BUG FIX EC-CORE-1: AdvanceIfAllSubTasksDone
    //
    // BUG LAMA: guard pakai `p.StatusPPIDID >= StatusId.DataSiap`
    //   - DataSiap = 10, WawancaraDijadwalkan = 12, WawancaraSelesai = 13
    //   - 12 >= 10 = TRUE → return false → status TIDAK PERNAH naik ke DataSiap
    //   - Inilah penyebab status di lacak tidak update dan panel admin stuck
    //
    // FIX: gunakan whitelist status terminal yang benar, bukan perbandingan angka.
    //   Status 12-15 bukan "sudah selesai" — itu status intermediate yang
    //   nomor ID-nya kebetulan lebih besar dari DataSiap(10) karena ditambah belakangan.
    // ════════════════════════════════════════════════════════════════════════
    public async Task<bool> AdvanceIfAllSubTasksDone(Guid permohonanId, string operatorName)
{
    // ── Advisory lock: mencegah race condition ────────────────────────────
    // Menggunakan dua komponen 32-bit dari GUID bytes untuk membentuk int64 unik.
    // Lebih aman dari GetHashCode() yang dapat menghasilkan collision antar GUID berbeda.
    var bytes = permohonanId.ToByteArray();
    long lockKey = (long)BitConverter.ToUInt32(bytes, 0) << 32
                 | BitConverter.ToUInt32(bytes, 8);

    await Database.ExecuteSqlAsync(
        $"SELECT pg_advisory_xact_lock({lockKey})");

    // ── Re-read SETELAH lock diperoleh ───────────────────────────────────
    var tasks = await SubTaskPPID
        .Where(t => t.PermohonanPPIDID == permohonanId)
        .ToListAsync();

    // Hanya task yang tidak dibatalkan yang diperhitungkan
    var activeTasks = tasks.Where(t => t.StatusTask != SubTaskStatus.Dibatalkan).ToList();

    if (activeTasks.Count == 0)
        return false;

    // Semua active task harus Selesai
    if (!activeTasks.All(t => t.IsSelesai))
        return false;

    var p = await PermohonanPPID.FindAsync(permohonanId);

    // ── Guard: hanya advance jika belum di status terminal ────────────────
    // JANGAN gunakan >= DataSiap karena:
    //   WawancaraDijadwalkan(12) dan WawancaraSelesai(13) > DataSiap(10) secara angka
    //   tapi BUKAN terminal state — ini adalah bug EC-CORE-1 yang sudah diperbaiki.
    if (p is null
        || p.StatusPPIDID == Models.StatusId.DataSiap
        || p.StatusPPIDID == Models.StatusId.FeedbackPemohon
        || p.StatusPPIDID == Models.StatusId.Selesai)
        return false;

    var lama       = p.StatusPPIDID;
    p.StatusPPIDID = Models.StatusId.DataSiap;
    p.UpdatedAt    = DateTime.UtcNow;

    int selesaiCount = activeTasks.Count;
    int batalCount   = tasks.Count(t => t.IsDibatalkan);

    // Detail per jenis task untuk audit log
    var detailPerJenis = activeTasks
        .Select(t => $"{Models.JenisTask.GetLabel(t.JenisTask)} (oleh: {t.Operator ?? "—"})")
        .ToList();

    string keterangan = $"Semua sub-tugas paralel selesai ({selesaiCount} selesai" +
                        (batalCount > 0 ? $", {batalCount} dibatalkan" : "") +
                        $"): {string.Join("; ", detailPerJenis)}. " +
                        "Status otomatis maju ke Data Siap.";

    AddAuditLog(permohonanId, lama, Models.StatusId.DataSiap, keterangan, operatorName);
    return true;
}

    public async Task<JadwalPPID?> GetJadwalAktif(Guid permohonanId, string jenisJadwal) =>
        await JadwalPPID.FirstOrDefaultAsync(j =>
            j.PermohonanPPIDID == permohonanId &&
            j.JenisJadwal      == jenisJadwal  &&
            j.IsAktif);

    public async Task<List<JadwalPPID>> GetJadwalHistory(Guid permohonanId, string jenisJadwal) =>
        await JadwalPPID
            .Where(j => j.PermohonanPPIDID == permohonanId && j.JenisJadwal == jenisJadwal)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

    public async Task<bool> RescheduleSubTask(
        Guid     permohonanId,
        string   jenisTask,
        DateOnly tanggalBaru,
        TimeOnly waktuBaru,
        string   namaPicBaru,
        string?  teleponPicBaru,
        string?  lokasiJenis,
        string?  lokasiDetail,
        string   alasanReschedule,
        string   operatorName)
    {
        var sub = await GetSubTask(permohonanId, jenisTask);
        if (sub is null) return false;
        if (sub.IsTerminal) return false;

        var now = DateTime.UtcNow;

        var jadwalLama = await JadwalPPID
            .Where(j => j.PermohonanPPIDID == permohonanId &&
                        j.JenisJadwal      == jenisTask    &&
                        j.IsAktif)
            .ToListAsync();

        foreach (var j in jadwalLama)
        {
            j.IsAktif   = false;
            j.UpdatedAt = now;
        }

        JadwalPPID.Add(new Models.JadwalPPID
        {
            PermohonanPPIDID = permohonanId,
            JenisJadwal      = jenisTask,
            Tanggal          = tanggalBaru,
            Waktu            = waktuBaru,
            NamaPIC          = namaPicBaru,
            TeleponPIC       = teleponPicBaru,
            LokasiJenis      = lokasiJenis,
            LokasiDetail     = lokasiDetail,
            Keterangan       = alasanReschedule,
            IsAktif          = true,
            CreatedAt        = now,
        });

        sub.TanggalJadwal    = tanggalBaru;
        sub.WaktuJadwal      = waktuBaru;
        sub.NamaPIC          = namaPicBaru;
        sub.TeleponPIC       = teleponPicBaru;
        sub.LokasiJenis      = lokasiJenis;
        sub.LokasiDetail     = lokasiDetail;
        sub.StatusTask       = SubTaskStatus.InProgress;
        sub.RescheduleCount += 1;
        sub.Operator         = operatorName;
        sub.UpdatedAt        = now;

        var p = await PermohonanPPID.FindAsync(permohonanId);
        if (p is not null)
            AddAuditLog(permohonanId, p.StatusPPIDID, p.StatusPPIDID ?? Models.StatusId.DiProses,
                $"Reschedule {Models.JenisTask.GetLabel(jenisTask)} " +
                $"(ke-{sub.RescheduleCount}): " +
                $"{tanggalBaru:dd MMM yyyy} pukul {waktuBaru:HH:mm}, " +
                $"PIC: {namaPicBaru}. Alasan: {alasanReschedule}",
                operatorName);

        return true;
    }


public async Task<bool> BatalSubTask(
    Guid   permohonanId,
    string jenisTask,
    string alasanBatal,
    string operatorName)
{
    var sub = await GetSubTask(permohonanId, jenisTask);
    if (sub is null) return false;

    // DIHAPUS: if (sub.IsSelesai) return false;
    // Pembatalan task Selesai diizinkan — controller yang memvalidasi per jenis task.

    if (sub.IsDibatalkan) return false; // sudah dibatalkan, tidak perlu ulang

    var now = DateTime.UtcNow;

    var jadwalAktif = await JadwalPPID
        .Where(j => j.PermohonanPPIDID == permohonanId &&
                    j.JenisJadwal      == jenisTask    &&
                    j.IsAktif)
        .ToListAsync();
    foreach (var j in jadwalAktif)
    {
        j.IsAktif   = false;
        j.UpdatedAt = now;
    }

    sub.StatusTask  = SubTaskStatus.Dibatalkan;
    sub.BatalAlasan = alasanBatal;
    sub.Operator    = operatorName;
    sub.UpdatedAt   = now;

    var p = await PermohonanPPID.FindAsync(permohonanId);
    if (p is not null)
        AddAuditLog(permohonanId, p.StatusPPIDID, p.StatusPPIDID ?? Models.StatusId.DiProses,
            $"Sub-tugas {Models.JenisTask.GetLabel(jenisTask)} dibatalkan. Alasan: {alasanBatal}",
            operatorName);

    return true;
}

public async Task<(bool Success, string? ErrorMessage)> BatalkanPermohonan(
    Guid   permohonanId,
    string alasanBatal,
    string operatorName)
{
    // ── Load DENGAN navigation property Status ────────────────────────────
    // BUG FIX: FindAsync tidak load p.Status → error message berisi "null".
    // Pakai FirstOrDefaultAsync + Include agar nama status tersedia.
    var p = await PermohonanPPID
        .Include(x => x.Status)
        .FirstOrDefaultAsync(x => x.PermohonanPPIDID == permohonanId);

    if (p is null)
        return (false, "Permohonan tidak ditemukan.");

    if (!Models.StatusId.IsBatalkanAllowed(p.StatusPPIDID))
        return (false,
            $"Pembatalan tidak diizinkan pada status " +
            $"'<strong>{p.Status?.NamaStatusPPID ?? Models.StatusId.GetStepLabel(p.StatusPPIDID)}</strong>'. " +
            "Hanya permohonan yang belum diproses (≤ Menunggu Surat Izin) yang dapat dibatalkan.");

    var now  = DateTime.UtcNow;
    var lama = p.StatusPPIDID;

    p.StatusPPIDID   = Models.StatusId.Dibatalkan;
    p.AlasanBatal    = alasanBatal;
    p.DibatalkanAt   = now;
    p.DibatalkanOleh = operatorName;
    p.UpdatedAt      = now;

    // Batalkan semua sub-tugas aktif
    var activeTasks = await SubTaskPPID
        .Where(t => t.PermohonanPPIDID == permohonanId
                 && t.StatusTask != Models.SubTaskStatus.Dibatalkan)
        .ToListAsync();

    foreach (var task in activeTasks)
    {
        task.StatusTask  = Models.SubTaskStatus.Dibatalkan;
        task.BatalAlasan = $"Permohonan dibatalkan oleh Loket: {alasanBatal}";
        task.Operator    = operatorName;
        task.UpdatedAt   = now;
    }

    // Nonaktifkan semua jadwal aktif
    var activeJadwal = await JadwalPPID
        .Where(j => j.PermohonanPPIDID == permohonanId && j.IsAktif)
        .ToListAsync();

    foreach (var j in activeJadwal)
    {
        j.IsAktif   = false;
        j.UpdatedAt = now;
    }

    string keterangan = $"Permohonan dibatalkan. Alasan: {alasanBatal}.";
    if (activeTasks.Count > 0)
        keterangan += $" {activeTasks.Count} sub-tugas ikut dibatalkan.";
    if (activeJadwal.Count > 0)
        keterangan += $" {activeJadwal.Count} jadwal dinonaktifkan.";

    AddAuditLog(permohonanId, lama, Models.StatusId.Dibatalkan, keterangan, operatorName);
    return (true, null);
}

    // ════════════════════════════════════════════════════════════════════════
    // BUG FIX EC-CORE-2: ReopenSubTask needsRollback
    //
    // BUG LAMA: `p.StatusPPIDID >= Models.StatusId.DataSiap`
    //   - Sama dengan bug di AdvanceIfAllSubTasksDone
    //   - WawancaraDijadwalkan(12) >= DataSiap(10) = TRUE
    //   - Reopen meminta konfirmasi rollback padahal status belum DataSiap
    //   - Jika operator tidak tahu harus centang apa → form error → reopen gagal
    //
    // FIX: whitelist status yang benar-benar perlu di-rollback.
    // ════════════════════════════════════════════════════════════════════════
    public async Task<(bool Success, bool NeedsRollback)> ReopenSubTask(
        Guid   permohonanId,
        string jenisTask,
        string alasanReopen,
        string operatorName)
    {
        var sub = await GetSubTask(permohonanId, jenisTask);
        if (sub is null) return (false, false);
        if (!sub.IsTerminal) return (false, false);

        var now = DateTime.UtcNow;

        sub.StatusTask   = SubTaskStatus.Pending;
        sub.ReopenedAt   = now;
        sub.ReopenAlasan = alasanReopen;
        sub.BatalAlasan  = null;
        sub.SelesaiAt    = null;
        sub.FilePath     = null;
        sub.NamaFile     = null;
        sub.Operator     = operatorName;
        sub.UpdatedAt    = now;

        var p = await PermohonanPPID.FindAsync(permohonanId);

        // FIX: hanya rollback jika memang sudah di status DataSiap/FeedbackPemohon/Selesai.
        // BUKAN pakai >= DataSiap karena akan salah deteksi status 12-15.
        bool needsRollback = p is not null && (
            p.StatusPPIDID == Models.StatusId.DataSiap ||
            p.StatusPPIDID == Models.StatusId.FeedbackPemohon ||
            p.StatusPPIDID == Models.StatusId.Selesai);

        if (p is not null)
            AddAuditLog(permohonanId, p.StatusPPIDID, p.StatusPPIDID ?? Models.StatusId.DiProses,
                $"Sub-tugas {Models.JenisTask.GetLabel(jenisTask)} di-reopen. " +
                $"Alasan: {alasanReopen}",
                operatorName);

        return (true, needsRollback);
    }

    public async Task<bool> UpdatePICSubTask(
        Guid    permohonanId,
        string  jenisTask,
        string  namaPicBaru,
        string? teleponPicBaru,
        string? catatanPerubahan,
        string  operatorName)
    {
        var sub = await GetSubTask(permohonanId, jenisTask);
        if (sub is null || sub.IsTerminal) return false;

        var now = DateTime.UtcNow;

        string picLama    = sub.NamaPIC ?? "(kosong)";
        sub.NamaPIC       = namaPicBaru;
        sub.TeleponPIC    = teleponPicBaru;
        sub.Operator      = operatorName;
        sub.UpdatedAt     = now;

        var jadwalAktif = await GetJadwalAktif(permohonanId, jenisTask);
        if (jadwalAktif is not null)
        {
            jadwalAktif.NamaPIC    = namaPicBaru;
            jadwalAktif.TeleponPIC = teleponPicBaru;
            if (!string.IsNullOrEmpty(catatanPerubahan))
                jadwalAktif.Keterangan = $"[Ganti PIC] {catatanPerubahan}";
            jadwalAktif.UpdatedAt  = now;
        }

        var p = await PermohonanPPID.FindAsync(permohonanId);
        if (p is not null)
            AddAuditLog(permohonanId, p.StatusPPIDID, p.StatusPPIDID ?? Models.StatusId.DiProses,
                $"PIC {Models.JenisTask.GetLabel(jenisTask)} diganti: " +
                $"{picLama} → {namaPicBaru}" +
                (string.IsNullOrEmpty(catatanPerubahan) ? "" : $". Catatan: {catatanPerubahan}"),
                operatorName);

        return true;
    }

    public async Task<bool> ReplaceFileSubTask(
        Guid    permohonanId,
        string  jenisTask,
        string  filePathBaru,
        string  namaFileBaru,
        string? catatanRevisi,
        string  operatorName)
    {
        var sub = await GetSubTask(permohonanId, jenisTask);
        if (sub is null || !sub.IsSelesai) return false;

        var now = DateTime.UtcNow;

        sub.FilePath  = filePathBaru;
        sub.NamaFile  = namaFileBaru;
        sub.Catatan   = string.IsNullOrEmpty(catatanRevisi)
            ? sub.Catatan
            : $"[Revisi {now:dd MMM HH:mm}] {catatanRevisi}";
        sub.Operator  = operatorName;
        sub.UpdatedAt = now;

        var p = await PermohonanPPID.FindAsync(permohonanId);
        if (p is not null)
            AddAuditLog(permohonanId, p.StatusPPIDID, p.StatusPPIDID ?? Models.StatusId.DataSiap,
                $"File hasil {Models.JenisTask.GetLabel(jenisTask)} direvisi: " +
                $"{namaFileBaru}" +
                (string.IsNullOrEmpty(catatanRevisi) ? "" : $". Catatan: {catatanRevisi}"),
                operatorName);

        return true;
    }

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
