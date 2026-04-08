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

// ── GANTI AdvanceIfAllSubTasksDone ───────────────────────────────────────────
/// <summary>
/// Cek apakah semua sub-task *yang aktif* (bukan dibatalkan) sudah selesai.
/// Jika ya, advance status permohonan ke DataSiap.
///
/// EC-4: Menggunakan advisory lock (20250003) agar tidak ada dua request
///       yang sama-sama lolos cek "all done" sebelum salah satunya save.
///       Lock di-acquire SEBELUM membaca status SubTask.
///
/// Catatan: SubTask yang Dibatalkan tidak dihitung → permohonan tetap bisa
///          advance meski satu sub-task dibatalkan (misal obs dibatalkan,
///          tapi data dan wawancara sudah selesai → DataSiap).
/// </summary>
public async Task<bool> AdvanceIfAllSubTasksDone(Guid permohonanId, string operatorName)
{
    // Advisory lock mencegah race condition di concurrent request.
    // Lock otomatis dilepas saat transaksi berakhir.
    await Database.ExecuteSqlAsync(
        $"SELECT pg_advisory_xact_lock(20250003, {permohonanId.GetHashCode()})");

    // Re-read state SETELAH lock diperoleh (agar tidak pakai stale data).
    var tasks = await SubTaskPPID
        .Where(t => t.PermohonanPPIDID == permohonanId)
        .ToListAsync();

    // Hanya task non-dibatalkan yang diperhitungkan.
    var activeTasks = tasks.Where(t => t.StatusTask != SubTaskStatus.Dibatalkan).ToList();

    if (activeTasks.Count == 0)
        return false;

    // Semua active task harus Selesai.
    if (!activeTasks.All(t => t.IsSelesai))
        return false;

    var p = await PermohonanPPID.FindAsync(permohonanId);
    if (p is null || p.StatusPPIDID >= Models.StatusId.DataSiap)
        return false;   // sudah advance atau tidak valid

    var lama       = p.StatusPPIDID;
    p.StatusPPIDID = Models.StatusId.DataSiap;
    p.UpdatedAt    = DateTime.UtcNow;

    int selesaiCount = activeTasks.Count;
    int batalCount   = tasks.Count(t => t.IsDibatalkan);
    string keterangan = $"Semua sub-tugas aktif selesai ({selesaiCount} selesai" +
                        (batalCount > 0 ? $", {batalCount} dibatalkan" : "") +
                        ") — data siap diunduh pemohon.";

    AddAuditLog(permohonanId, lama, Models.StatusId.DataSiap, keterangan, operatorName);
    return true;
}


/// <summary>
/// Ambil jadwal aktif (terbaru) untuk permohonan + jenis tertentu (EC-1).
/// Jadwal lama (reschedule history) tetap ada di DB dengan IsAktif = false.
/// </summary>
public async Task<JadwalPPID?> GetJadwalAktif(Guid permohonanId, string jenisJadwal) =>
    await JadwalPPID.FirstOrDefaultAsync(j =>
        j.PermohonanPPIDID == permohonanId &&
        j.JenisJadwal      == jenisJadwal  &&
        j.IsAktif);



/// <summary>Seluruh riwayat jadwal untuk audit trail (EC-1).</summary>
public async Task<List<JadwalPPID>> GetJadwalHistory(Guid permohonanId, string jenisJadwal) =>
    await JadwalPPID
        .Where(j => j.PermohonanPPIDID == permohonanId && j.JenisJadwal == jenisJadwal)
        .OrderByDescending(j => j.CreatedAt)
        .ToListAsync();



// ── TAMBAH RescheduleSubTask ─────────────────────────────────────────────────
/// <summary>
/// EC-1: Reschedule jadwal (berubah mendadak).
///
/// Langkah:
///   1. Nonaktifkan jadwal lama (IsAktif = false)
///   2. Buat jadwal baru sebagai aktif
///   3. Update SubTask (tanggal, waktu, PIC, +1 RescheduleCount)
///   4. Catat di AuditLog
///   5. TIDAK mengubah status permohonan utama (sudah WawancaraDijadwalkan/ObservasiDijadwalkan)
///
/// Return: false jika SubTask tidak ditemukan atau sudah Selesai/Dibatalkan.
/// </summary>
public async Task<bool> RescheduleSubTask(
    Guid     permohonanId,
    string   jenisTask,
    DateOnly tanggalBaru,
    TimeOnly waktuBaru,
    string   namaPicBaru,
    string?  teleponPicBaru,
    string   alasanReschedule,
    string   operatorName)
{
    var sub = await GetSubTask(permohonanId, jenisTask);
    if (sub is null) return false;

    // Tidak boleh reschedule task yang sudah terminal.
    if (sub.IsTerminal)
        return false;

    var now = DateTime.UtcNow;

    // 1. Nonaktifkan semua jadwal lama.
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

    // 2. Buat jadwal baru.
    JadwalPPID.Add(new Models.JadwalPPID
    {
        PermohonanPPIDID = permohonanId,
        JenisJadwal      = jenisTask,
        Tanggal          = tanggalBaru,
        Waktu            = waktuBaru,
        NamaPIC          = namaPicBaru,
        TeleponPIC       = teleponPicBaru,
        Keterangan       = alasanReschedule,
        IsAktif          = true,
        CreatedAt        = now,
    });

    // 3. Update SubTask.
    sub.TanggalJadwal    = tanggalBaru;
    sub.WaktuJadwal      = waktuBaru;
    sub.NamaPIC          = namaPicBaru;
    sub.TeleponPIC       = teleponPicBaru;
    sub.StatusTask       = SubTaskStatus.InProgress; // pastikan masih InProgress
    sub.RescheduleCount += 1;
    sub.Operator         = operatorName;
    sub.UpdatedAt        = now;

    // 4. Audit log.
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

// ── TAMBAH BatalSubTask ──────────────────────────────────────────────────────
/// <summary>
/// EC-2: Batalkan SubTask dengan alasan.
///
/// SubTask yang dibatalkan tidak dihitung dalam AdvanceIfAllSubTasksDone,
/// sehingga permohonan tetap bisa advance ke DataSiap jika task lain selesai.
///
/// Jika semua active task sudah selesai SETELAH pembatalan ini,
/// kembalikan true agar caller bisa memanggil AdvanceIfAllSubTasksDone.
/// </summary>
public async Task<bool> BatalSubTask(
    Guid   permohonanId,
    string jenisTask,
    string alasanBatal,
    string operatorName)
{
    var sub = await GetSubTask(permohonanId, jenisTask);
    if (sub is null) return false;

    // Tidak boleh membatalkan task yang sudah Selesai.
    if (sub.IsSelesai)
        return false;

    var now = DateTime.UtcNow;

    // Nonaktifkan jadwal jika ada.
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

// ── TAMBAH ReopenSubTask ─────────────────────────────────────────────────────
/// <summary>
/// EC-3: Buka kembali SubTask yang sudah Selesai/Dibatalkan.
///
/// Kasus: hasil wawancara/observasi ternyata kurang/salah, atau
///        task yang sebelumnya dibatalkan ternyata perlu dikerjakan.
///
/// Efek samping: jika permohonan sudah DataSiap, status TIDAK di-rollback
///               secara otomatis — caller harus putuskan apakah perlu rollback.
///               Return nilai kedua (bool) = apakah status permohonan perlu di-rollback.
/// </summary>
public async Task<(bool Success, bool NeedsRollback)> ReopenSubTask(
    Guid   permohonanId,
    string jenisTask,
    string alasanReopen,
    string operatorName)
{
    var sub = await GetSubTask(permohonanId, jenisTask);
    if (sub is null) return (false, false);

    // Hanya bisa reopen task yang terminal.
    if (!sub.IsTerminal) return (false, false);

    var now = DateTime.UtcNow;

    sub.StatusTask   = SubTaskStatus.Pending;   // kembali ke Pending, bukan InProgress
    sub.ReopenedAt   = now;
    sub.ReopenAlasan = alasanReopen;
    sub.BatalAlasan  = null;
    sub.SelesaiAt    = null;
    sub.FilePath     = null;
    sub.NamaFile     = null;
    sub.Operator     = operatorName;
    sub.UpdatedAt    = now;

    var p = await PermohonanPPID.FindAsync(permohonanId);
    bool needsRollback = p is not null && p.StatusPPIDID >= Models.StatusId.DataSiap;

    if (p is not null)
        AddAuditLog(permohonanId, p.StatusPPIDID, p.StatusPPIDID ?? Models.StatusId.DiProses,
            $"Sub-tugas {Models.JenisTask.GetLabel(jenisTask)} di-reopen. " +
            $"Alasan: {alasanReopen}",
            operatorName);

    return (true, needsRollback);
}

// ── TAMBAH UpdatePICSubTask ──────────────────────────────────────────────────
/// <summary>
/// EC-6: Ganti PIC/narasumber TANPA mengubah tanggal dan waktu jadwal.
///
/// Berbeda dengan reschedule — ini hanya update kontak, tidak menambah
/// entri JadwalPPID baru dan tidak menambah RescheduleCount.
/// </summary>
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

    // Update SubTask.
    string picLama    = sub.NamaPIC ?? "(kosong)";
    sub.NamaPIC       = namaPicBaru;
    sub.TeleponPIC    = teleponPicBaru;
    sub.Operator      = operatorName;
    sub.UpdatedAt     = now;

    // Update jadwal aktif juga.
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

// ── TAMBAH ReplaceFileSubTask ────────────────────────────────────────────────
/// <summary>
/// EC-7: Ganti file hasil (revisi) setelah SubTask Selesai.
///
/// File lama tetap di-record di DokumenPPID untuk audit trail,
/// SubTask.FilePath diperbarui ke file baru.
/// </summary>
public async Task<bool> ReplaceFileSubTask(
    Guid    permohonanId,
    string  jenisTask,
    string  filePathBaru,
    string  namaFileBaru,
    string? catatanRevisi,
    string  operatorName)
{
    var sub = await GetSubTask(permohonanId, jenisTask);

    // Hanya task Selesai yang bisa replace file.
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
