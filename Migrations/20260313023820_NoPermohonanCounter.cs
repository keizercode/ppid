using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class NoPermohonanCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Buat tabel counter ─────────────────────────────────────────
            // Satu baris per tahun. LastSeq adalah nilai terakhir yang sudah
            // diterbitkan — bukan nilai berikutnya.
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS public."NoPermohonanCounter" (
                    "Year"    INTEGER NOT NULL,
                    "LastSeq" INTEGER NOT NULL DEFAULT 0,
                    CONSTRAINT "PK_NoPermohonanCounter" PRIMARY KEY ("Year")
                );
                """);

            // ── 2. Backfill dari data yang sudah ada ──────────────────────────
            // Baca MAX sequence per tahun langsung dari NoPermohonan (sumber
            // kebenaran), bukan dari kolom Sequance yang bisa null/salah.
            //
            // Format NoPermohonan: PPD/YYYY/NNNN
            //   SPLIT_PART('PPD/2026/0007', '/', 2) → '2026'
            //   SPLIT_PART('PPD/2026/0007', '/', 3) → '0007'
            //
            // ON CONFLICT: jika row tahun ini sudah ada (jalankan migrasi dua
            // kali), ambil nilai terbesar agar tidak mundur.
            migrationBuilder.Sql("""
                INSERT INTO public."NoPermohonanCounter" ("Year", "LastSeq")
                SELECT
                    CAST(SPLIT_PART("NoPermohonan", '/', 2) AS INTEGER) AS "Year",
                    MAX(CAST(SPLIT_PART("NoPermohonan", '/', 3) AS INTEGER)) AS "LastSeq"
                FROM public."PermohonanPPID"
                WHERE "NoPermohonan" ~ '^PPD/\d{4}/\d+$'
                GROUP BY SPLIT_PART("NoPermohonan", '/', 2)
                ON CONFLICT ("Year") DO UPDATE
                    SET "LastSeq" = GREATEST(
                        public."NoPermohonanCounter"."LastSeq",
                        EXCLUDED."LastSeq"
                    );
                """);

            // ── 3. Seed tahun berjalan jika belum ada ─────────────────────────
            // Mencegah row kosong untuk tahun baru yang belum punya permohonan.
            migrationBuilder.Sql("""
                INSERT INTO public."NoPermohonanCounter" ("Year", "LastSeq")
                VALUES (DATE_PART('year', NOW())::INTEGER, 0)
                ON CONFLICT ("Year") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS public."NoPermohonanCounter";
                """);
        }
    }
}
