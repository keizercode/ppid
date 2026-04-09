using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <summary>
    /// Perubahan:
    ///   1. Tambah tabel FeedbackTaskPPID — feedback pemohon per jenis tugas
    ///      (Observasi, PermintaanData, Wawancara), diterima Kasubkel Kepegawaian.
    ///   2. Tambah LokasiJenis + LokasiDetail ke JadwalPPID dan SubTaskPPID
    ///      sehingga petugas bisa mencatat online/offline + link atau nama ruangan.
    /// </summary>
    public partial class AddFeedbackTaskAndLokasi : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tabel FeedbackTaskPPID ─────────────────────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS public.""FeedbackTaskPPID"" (
                    ""FeedbackTaskID""   uuid        NOT NULL DEFAULT gen_random_uuid(),
                    ""PermohonanPPIDID"" uuid        NOT NULL,
                    ""JenisTask""        text        NOT NULL,
                    ""NilaiKepuasan""    integer     NOT NULL DEFAULT 0,
                    ""Catatan""          text,
                    ""FileLaporan""      text,
                    ""NamaFile""         text,
                    ""CreatedAt""        timestamptz NOT NULL DEFAULT NOW(),
                    CONSTRAINT ""PK_FeedbackTaskPPID"" PRIMARY KEY (""FeedbackTaskID""),
                    CONSTRAINT ""FK_FeedbackTaskPPID_PermohonanPPID""
                        FOREIGN KEY (""PermohonanPPIDID"")
                        REFERENCES public.""PermohonanPPID""(""PermohonanPPIDID"")
                        ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_FeedbackTaskPPID_PermohonanPPIDID""
                    ON public.""FeedbackTaskPPID""(""PermohonanPPIDID"");
                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_FeedbackTaskPPID_PermohonanTask""
                    ON public.""FeedbackTaskPPID""(""PermohonanPPIDID"", ""JenisTask"");
            ");

            // ── 2. Kolom lokasi di JadwalPPID ─────────────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'JadwalPPID'
                          AND column_name  = 'LokasiJenis'
                    ) THEN
                        ALTER TABLE public.""JadwalPPID""
                            ADD COLUMN ""LokasiJenis""  text,
                            ADD COLUMN ""LokasiDetail"" text;
                    END IF;
                END$$;
            ");

            // ── 3. Kolom lokasi di SubTaskPPID ────────────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'SubTaskPPID'
                          AND column_name  = 'LokasiJenis'
                    ) THEN
                        ALTER TABLE public.""SubTaskPPID""
                            ADD COLUMN ""LokasiJenis""  text,
                            ADD COLUMN ""LokasiDetail"" text;
                    END IF;
                END$$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS public.""FeedbackTaskPPID"";
                ALTER TABLE public.""JadwalPPID""
                    DROP COLUMN IF EXISTS ""LokasiJenis"",
                    DROP COLUMN IF EXISTS ""LokasiDetail"";
                ALTER TABLE public.""SubTaskPPID""
                    DROP COLUMN IF EXISTS ""LokasiJenis"",
                    DROP COLUMN IF EXISTS ""LokasiDetail"";
            ");
        }
    }
}
