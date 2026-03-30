using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    public partial class AddNimAndColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Kolom NIM di PribadiPPID ──────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "NIM",
                schema: "public",
                table: "PribadiPPID",
                type: "text",
                nullable: true);

            // ── 2. Kolom BatasWaktu, TanggalSelesai, Pengampu di PermohonanPPID
            //       (jika belum ada dari migration sebelumnya) ─────────────────
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
                            ADD COLUMN ""BatasWaktu"" date,
                            ADD COLUMN ""TanggalSelesai"" date,
                            ADD COLUMN ""Pengampu"" text;
                    END IF;
                END$$;
            ");

            // ── 3. Kolom NIM di PribadiPPID (guard duplikat) ─────────────────
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

            // ── 4. Status baru 14 & 15 (guard duplikat) ──────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO public.""StatusPPID"" (""StatusPPIDID"", ""NamaStatusPPID"")
                VALUES
                    (14, 'Menunggu Verifikasi Kasubkel'),
                    (15, 'Pengisian Feedback Pemohon')
                ON CONFLICT (""StatusPPIDID"") DO NOTHING;
            ");

            // ── 5. Seed user kasubkepeg & kasubkdi ───────────────────────────
            // Hash digenerate dengan AppUser.HashPassword("kasubkepeg123") dll.
            // Jalankan snippet berikut di csharp repl jika ingin regenerate:
            //   AppUser.HashPassword("kasubkepeg123")  → paste di bawah
            //   AppUser.HashPassword("kasubkdi123")
            migrationBuilder.Sql(@"
                INSERT INTO public.""AppUser"" (""Username"", ""PasswordHash"", ""Role"", ""NamaLengkap"", ""IsActive"")
                VALUES
                    ('kasubkepeg',
                     'PPID_v1:' || UPPER(ENCODE(
                         SHA256(CONVERT_TO('PPID_DLH_JKT_2025kasubkepeg123', 'UTF8')), 'hex')),
                     'KasubkelKepegawaian', 'Kasubkel Kepegawaian', true),
                    ('kasubkdi',
                     'PPID_v1:' || UPPER(ENCODE(
                         SHA256(CONVERT_TO('PPID_DLH_JKT_2025kasubkdi123', 'UTF8')), 'hex')),
                     'KasubkelKDI', 'Kasubkel KDI', true)
                ON CONFLICT (""Username"") DO NOTHING;
            ");

            // ── 6. Backfill BatasWaktu dari TanggalPermohonan + 14 hari ──────
            migrationBuilder.Sql(@"
                UPDATE public.""PermohonanPPID""
                SET ""BatasWaktu"" = ""TanggalPermohonan"" + INTERVAL '14 days'
                WHERE ""BatasWaktu"" IS NULL
                  AND ""TanggalPermohonan"" IS NOT NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.""PribadiPPID"" DROP COLUMN IF EXISTS ""NIM"";
                ALTER TABLE public.""PermohonanPPID"" DROP COLUMN IF EXISTS ""BatasWaktu"";
                ALTER TABLE public.""PermohonanPPID"" DROP COLUMN IF EXISTS ""TanggalSelesai"";
                ALTER TABLE public.""PermohonanPPID"" DROP COLUMN IF EXISTS ""Pengampu"";
                DELETE FROM public.""StatusPPID"" WHERE ""StatusPPIDID"" IN (14, 15);
                DELETE FROM public.""AppUser"" WHERE ""Username"" IN ('kasubkepeg', 'kasubkdi');
            ");
        }
    }
}
