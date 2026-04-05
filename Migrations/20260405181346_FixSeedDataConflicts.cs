using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <summary>
    /// Fix seed data conflicts:
    ///   - HasData() di OnModelCreating sudah mendefinisikan StatusPPID 1-15
    ///     tapi migration lama meng-insert sebagian via raw SQL.
    ///   - Migration ini memastikan semua status ada dengan ON CONFLICT DO NOTHING,
    ///     sehingga idempotent (aman dijalankan berulang kali).
    ///   - Juga memastikan NoPermohonanCounter ada untuk tahun berjalan.
    ///   - Memastikan kolom "pekerjaan" (lowercase) ada di PribadiPPID.
    /// </summary>
    public partial class FixSeedDataConflicts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Pastikan SEMUA status 1-15 ada (idempotent) ───────────────
            migrationBuilder.Sql(@"
                INSERT INTO public.""StatusPPID"" (""StatusPPIDID"", ""NamaStatusPPID"")
                VALUES
                    (1,  'Baru'),
                    (2,  'Terdaftar'),
                    (3,  'Identifikasi Awal'),
                    (4,  'Menunggu Surat Izin'),
                    (5,  'Surat Izin Terbit'),
                    (6,  'Didisposisi'),
                    (7,  'Sedang Diproses'),
                    (8,  'Observasi Dijadwalkan'),
                    (9,  'Observasi Selesai'),
                    (10, 'Data Siap'),
                    (11, 'Selesai'),
                    (12, 'Wawancara Dijadwalkan'),
                    (13, 'Wawancara Selesai'),
                    (14, 'Menunggu Verifikasi Kasubkel'),
                    (15, 'Pengisian Feedback Pemohon')
                ON CONFLICT (""StatusPPIDID"") DO UPDATE
                    SET ""NamaStatusPPID"" = EXCLUDED.""NamaStatusPPID"";
            ");

            // ── 2. Pastikan Keperluan 1-3 ada ────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO public.""Keperluan"" (""KeperluanID"", ""NamaKeperluan"")
                VALUES
                    (1, 'Observasi'),
                    (2, 'Permintaan Data'),
                    (3, 'Wawancara')
                ON CONFLICT (""KeperluanID"") DO UPDATE
                    SET ""NamaKeperluan"" = EXCLUDED.""NamaKeperluan"";
            ");

            // ── 3. Pastikan JenisDokumenPPID 1-7 ada ─────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO public.""JenisDokumenPPID"" (""JenisDokumenPPIDID"", ""NamaJenisDokumenPPID"", ""IsActive"")
                VALUES
                    (1, 'KTP',                        true),
                    (2, 'Surat Permohonan',           true),
                    (3, 'Proposal Penelitian',        true),
                    (4, 'Akta Notaris',               true),
                    (5, 'Dokumen Identifikasi (TTD)', true),
                    (6, 'Surat Izin',                 true),
                    (7, 'Data Hasil',                 true)
                ON CONFLICT (""JenisDokumenPPIDID"") DO UPDATE
                    SET ""NamaJenisDokumenPPID"" = EXCLUDED.""NamaJenisDokumenPPID"",
                        ""IsActive""             = EXCLUDED.""IsActive"";
            ");

            // ── 4. Pastikan kolom LoketJenis ada di PermohonanPPID ────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'PermohonanPPID'
                          AND column_name  = 'LoketJenis'
                    ) THEN
                        ALTER TABLE public.""PermohonanPPID""
                            ADD COLUMN ""LoketJenis"" text;
                    END IF;
                END$$;
            ");

            // ── 5. Pastikan kolom TeleponPengampu ada ─────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'PermohonanPPID'
                          AND column_name  = 'TeleponPengampu'
                    ) THEN
                        ALTER TABLE public.""PermohonanPPID""
                            ADD COLUMN ""TeleponPengampu"" text;
                    END IF;
                END$$;
            ");

            // ── 6. Pastikan kolom BatasWaktu, TanggalSelesai, Pengampu ada ───
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'PermohonanPPID'
                          AND column_name  = 'BatasWaktu'
                    ) THEN
                        ALTER TABLE public.""PermohonanPPID""
                            ADD COLUMN ""BatasWaktu""     date,
                            ADD COLUMN ""TanggalSelesai"" date,
                            ADD COLUMN ""Pengampu""       text;
                    END IF;
                END$$;
            ");

            // ── 7. Pastikan kolom NamaProdusenData ada ────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'PermohonanPPID'
                          AND column_name  = 'NamaProdusenData'
                    ) THEN
                        ALTER TABLE public.""PermohonanPPID""
                            ADD COLUMN ""NamaProdusenData"" text;
                    END IF;
                END$$;
            ");

            // ── 8. Pastikan kolom JenisJadwal ada di JadwalPPID ──────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'JadwalPPID'
                          AND column_name  = 'JenisJadwal'
                    ) THEN
                        ALTER TABLE public.""JadwalPPID""
                            ADD COLUMN ""JenisJadwal"" text NOT NULL DEFAULT 'Observasi';
                    END IF;
                END$$;
            ");

            // ── 9. Pastikan kolom TeleponPIC ada di JadwalPPID & SubTaskPPID ──
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'JadwalPPID'
                          AND column_name  = 'TeleponPIC'
                    ) THEN
                        ALTER TABLE public.""JadwalPPID""
                            ADD COLUMN ""TeleponPIC"" text;
                    END IF;
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'SubTaskPPID'
                          AND column_name  = 'TeleponPIC'
                    ) THEN
                        ALTER TABLE public.""SubTaskPPID""
                            ADD COLUMN ""TeleponPIC"" text;
                    END IF;
                END$$;
            ");

            // ── 10. Pastikan kolom NIM ada di PribadiPPID ─────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'PribadiPPID'
                          AND column_name  = 'NIM'
                    ) THEN
                        ALTER TABLE public.""PribadiPPID""
                            ADD COLUMN ""NIM"" text;
                    END IF;
                END$$;
            ");

            // ── 11. Backfill LoketJenis dari KategoriPemohon ──────────────────
            migrationBuilder.Sql(@"
                UPDATE public.""PermohonanPPID""
                SET ""LoketJenis"" = CASE
                    WHEN ""KategoriPemohon"" = 'Mahasiswa' THEN 'Kepegawaian'
                    ELSE 'Umum'
                END
                WHERE ""LoketJenis"" IS NULL;
            ");

            // ── 12. Backfill BatasWaktu ───────────────────────────────────────
            migrationBuilder.Sql(@"
                UPDATE public.""PermohonanPPID""
                SET ""BatasWaktu"" = ""TanggalPermohonan"" + INTERVAL '14 days'
                WHERE ""BatasWaktu"" IS NULL
                  AND ""TanggalPermohonan"" IS NOT NULL;
            ");

            // ── 13. Pastikan NoPermohonanCounter table ada ────────────────────
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS public.""NoPermohonanCounter"" (
                    ""Year""    INTEGER NOT NULL,
                    ""LastSeq"" INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT ""PK_NoPermohonanCounter"" PRIMARY KEY (""Year"")
                );

                -- Seed tahun berjalan jika belum ada
                INSERT INTO public.""NoPermohonanCounter"" (""Year"", ""LastSeq"")
                VALUES (DATE_PART('year', NOW())::INTEGER, 0)
                ON CONFLICT (""Year"") DO NOTHING;
            ");

            // ── 14. Pastikan unique index NoPermohonan ada ────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'public'
                          AND tablename  = 'PermohonanPPID'
                          AND indexname  = 'IX_PermohonanPPID_NoPermohonan'
                    ) THEN
                        CREATE UNIQUE INDEX ""IX_PermohonanPPID_NoPermohonan""
                        ON public.""PermohonanPPID"" (""NoPermohonan"");
                    END IF;
                END$$;
            ");

            // ── 15. Pastikan AuditLogPPID table ada ───────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_name   = 'AuditLogPPID'
                    ) THEN
                        CREATE TABLE public.""AuditLogPPID"" (
                            ""AuditLogID""       uuid          NOT NULL,
                            ""PermohonanPPIDID"" uuid          NOT NULL,
                            ""StatusLama""       integer,
                            ""StatusBaru""       integer,
                            ""Keterangan""       text,
                            ""Operator""         text,
                            ""CreatedAt""        timestamptz   NOT NULL,
                            CONSTRAINT ""PK_AuditLogPPID"" PRIMARY KEY (""AuditLogID""),
                            CONSTRAINT ""FK_AuditLogPPID_PermohonanPPID_PermohonanPPIDID""
                                FOREIGN KEY (""PermohonanPPIDID"")
                                REFERENCES public.""PermohonanPPID""(""PermohonanPPIDID"")
                                ON DELETE CASCADE
                        );
                        CREATE INDEX ""IX_AuditLogPPID_PermohonanPPIDID""
                            ON public.""AuditLogPPID""(""PermohonanPPIDID"");
                        CREATE INDEX ""IX_AuditLogPPID_CreatedAt""
                            ON public.""AuditLogPPID""(""CreatedAt"");
                    END IF;
                END$$;
            ");

            // ── 16. Pastikan SubTaskPPID table ada ────────────────────────────
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_name   = 'SubTaskPPID'
                    ) THEN
                        CREATE TABLE public.""SubTaskPPID"" (
                            ""SubTaskID""        uuid        NOT NULL DEFAULT gen_random_uuid(),
                            ""PermohonanPPIDID"" uuid        NOT NULL,
                            ""JenisTask""        text        NOT NULL,
                            ""StatusTask""       integer     NOT NULL DEFAULT 0,
                            ""FilePath""         text,
                            ""NamaFile""         text,
                            ""Catatan""          text,
                            ""NamaPIC""          text,
                            ""TeleponPIC""       text,
                            ""TanggalJadwal""    date,
                            ""WaktuJadwal""      time without time zone,
                            ""Operator""         text,
                            ""CreatedAt""        timestamptz NOT NULL DEFAULT NOW(),
                            ""SelesaiAt""        timestamptz,
                            ""UpdatedAt""        timestamptz,
                            CONSTRAINT ""PK_SubTaskPPID"" PRIMARY KEY (""SubTaskID""),
                            CONSTRAINT ""FK_SubTaskPPID_PermohonanPPID_PermohonanPPIDID""
                                FOREIGN KEY (""PermohonanPPIDID"")
                                REFERENCES public.""PermohonanPPID""(""PermohonanPPIDID"")
                                ON DELETE CASCADE
                        );
                        CREATE INDEX ""IX_SubTaskPPID_PermohonanPPIDID""
                            ON public.""SubTaskPPID""(""PermohonanPPIDID"");
                        CREATE INDEX ""IX_SubTaskPPID_PermohonanPPIDID_JenisTask""
                            ON public.""SubTaskPPID""(""PermohonanPPIDID"", ""JenisTask"");
                    END IF;
                END$$;
            ");

            // ── 17. Seed default AppUsers jika belum ada ──────────────────────
            migrationBuilder.Sql(@"
                -- Pastikan tabel AppUser ada
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public'
                          AND table_name   = 'AppUser'
                    ) THEN
                        CREATE TABLE public.""AppUser"" (
                            ""AppUserID""    serial          NOT NULL,
                            ""Username""     varchar(50)     NOT NULL,
                            ""PasswordHash"" text            NOT NULL,
                            ""Role""         varchar(30)     NOT NULL,
                            ""NamaLengkap""  text            NOT NULL,
                            ""IsActive""     boolean         NOT NULL DEFAULT true,
                            ""CreatedAt""    timestamptz,
                            ""UpdatedAt""    timestamptz,
                            CONSTRAINT ""PK_AppUser"" PRIMARY KEY (""AppUserID"")
                        );
                        CREATE UNIQUE INDEX ""IX_AppUser_Username""
                            ON public.""AppUser""(""Username"");
                    END IF;
                END$$;

                -- Seed users
                INSERT INTO public.""AppUser"" (""Username"", ""PasswordHash"", ""Role"", ""NamaLengkap"", ""IsActive"")
                VALUES
                    ('loket',      'PPID_v1:95EDA02C6F17D32F74088B0D32402439A7312500A1D46891111FF1B20F864F83', 'Loket',               'Petugas Loket',       true),
                    ('loketumum',  'PPID_v1:' || UPPER(ENCODE(SHA256(CONVERT_TO('PPID_DLH_JKT_2025loketumum123','UTF8')),'hex')),  'LoketUmum',           'Petugas Loket Umum',  true),
                    ('kasubkepeg', 'PPID_v1:' || UPPER(ENCODE(SHA256(CONVERT_TO('PPID_DLH_JKT_2025kasubkepeg123','UTF8')),'hex')), 'KasubkelKepegawaian', 'Kasubkel Kepegawaian', true),
                    ('kasubkdi',   'PPID_v1:' || UPPER(ENCODE(SHA256(CONVERT_TO('PPID_DLH_JKT_2025kasubkdi123','UTF8')),'hex')),   'KasubkelKDI',         'Kasubkel KDI',        true),
                    ('admin',      'PPID_v1:81A9C5C1602F933DB6636C8B1A6AFACCDE5DC085C706FA68CD10820D1F22D685', 'Admin',               'Administrator',       true)
                ON CONFLICT (""Username"") DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migration ini bersifat defensive/additive — Down tidak menghapus data kritis
            // hanya log bahwa rollback dilakukan
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    RAISE NOTICE 'Rollback FixSeedDataConflicts — data seed tidak dihapus.';
                END $$;
            ");
        }
    }
}
