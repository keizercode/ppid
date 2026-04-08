using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <summary>
    /// Menangani seluruh siklus hidup SubTask yang sebelumnya tidak tercakup:
    ///   EC-1  Reschedule (jadwal berubah mendadak)
    ///   EC-2  Batal SubTask (narasumber/lokasi bermasalah)
    ///   EC-3  Reopen SubTask (hasil perlu dikerjakan ulang)
    ///   EC-4  Race condition di AdvanceIfAllSubTasksDone (advisory lock sudah ada di query,
    ///          tapi SubTask perlu kolom versi untuk optimistic check)
    ///   EC-6  Ganti PIC tanpa reschedule tanggal
    ///   EC-7  Ganti file hasil (revisi)
    ///   EC-8  Overdue tracking
    ///
    /// Kolom baru:
    ///   SubTaskPPID.BatalAlasan        → alasan pembatalan (EC-2)
    ///   SubTaskPPID.RescheduleCount    → berapa kali sudah dijadwalkan ulang (EC-1)
    ///   SubTaskPPID.ReopenedAt         → kapan terakhir di-reopen (EC-3)
    ///   SubTaskPPID.ReopenAlasan       → alasan reopen (EC-3)
    ///   SubTaskPPID.RowVersion         → untuk optimistic concurrency (EC-4)
    ///   JadwalPPID.Keterangan          → alasan reschedule / catatan perubahan jadwal
    ///   JadwalPPID.IsAktif             → hanya jadwal aktif terbaru yg digunakan
    ///
    ///   Status baru: Dibatalkan = 3 (SubTaskStatus)
    /// </summary>
    public partial class AddSubTaskLifecycle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. SubTaskPPID: kolom lifecycle ──────────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'SubTaskPPID'
                          AND column_name  = 'BatalAlasan'
                    ) THEN
                        ALTER TABLE public.""SubTaskPPID""
                            ADD COLUMN ""BatalAlasan""     text,
                            ADD COLUMN ""RescheduleCount"" integer NOT NULL DEFAULT 0,
                            ADD COLUMN ""ReopenedAt""      timestamptz,
                            ADD COLUMN ""ReopenAlasan""    text,
                            ADD COLUMN ""RowVersion""      bigint NOT NULL DEFAULT 0;
                    END IF;
                END$$;
            ");

            // ── 2. JadwalPPID: keterangan & IsAktif ──────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'JadwalPPID'
                          AND column_name  = 'Keterangan'
                    ) THEN
                        ALTER TABLE public.""JadwalPPID""
                            ADD COLUMN ""Keterangan"" text,
                            ADD COLUMN ""IsAktif""    boolean NOT NULL DEFAULT true;
                    END IF;
                END$$;
            ");

            // ── 3. Backfill: semua jadwal yang ada dianggap aktif ─────────────
            // Untuk setiap PermohonanPPIDID + JenisJadwal, hanya yang terbaru = aktif.
            migrationBuilder.Sql(@"
                UPDATE public.""JadwalPPID"" j
                SET ""IsAktif"" = (j.""JadwalPPIDID"" = latest.""JadwalPPIDID"")
                FROM (
                    SELECT DISTINCT ON (""PermohonanPPIDID"", ""JenisJadwal"")
                           ""JadwalPPIDID"",
                           ""PermohonanPPIDID"",
                           ""JenisJadwal""
                    FROM public.""JadwalPPID""
                    ORDER BY ""PermohonanPPIDID"", ""JenisJadwal"", ""CreatedAt"" DESC
                ) AS latest
                WHERE j.""PermohonanPPIDID"" = latest.""PermohonanPPIDID""
                  AND j.""JenisJadwal""       = latest.""JenisJadwal"";
            ");

            // ── 4. Index untuk query jadwal aktif ─────────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND tablename  = 'JadwalPPID'
                          AND indexname  = 'IX_JadwalPPID_IsAktif'
                    ) THEN
                        CREATE INDEX ""IX_JadwalPPID_IsAktif""
                        ON public.""JadwalPPID"" (""PermohonanPPIDID"", ""JenisJadwal"", ""IsAktif"");
                    END IF;
                END$$;
            ");

            // ── 5. Status baru: Dibatalkan = 3 ───────────────────────────────
            // SubTaskStatus adalah integer konstanta di kode, bukan tabel DB.
            // Kolom StatusTask tetap integer; nilai 3 = Dibatalkan ditangani di C#.

            // ── 6. Seed keterangan untuk reschedule yang sudah ada ────────────
            // Data lama tidak punya keterangan → isi dengan teks default agar
            // query yang filter keterangan NOT NULL tidak salah.
            // (Tidak wajib, tapi rapi untuk audit trail.)
            migrationBuilder.Sql(@"
                UPDATE public.""JadwalPPID""
                SET ""Keterangan"" = 'Jadwal awal (migrasi data lama)'
                WHERE ""Keterangan"" IS NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS public.""IX_JadwalPPID_IsAktif"";

                ALTER TABLE public.""SubTaskPPID""
                    DROP COLUMN IF EXISTS ""BatalAlasan"",
                    DROP COLUMN IF EXISTS ""RescheduleCount"",
                    DROP COLUMN IF EXISTS ""ReopenedAt"",
                    DROP COLUMN IF EXISTS ""ReopenAlasan"",
                    DROP COLUMN IF EXISTS ""RowVersion"";

                ALTER TABLE public.""JadwalPPID""
                    DROP COLUMN IF EXISTS ""Keterangan"",
                    DROP COLUMN IF EXISTS ""IsAktif"";
            ");
        }
    }
}
