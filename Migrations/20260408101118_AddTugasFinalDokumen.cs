using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    public partial class AddTugasFinalDokumen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. JenisDokumen baru: TugasFinal (ID = 8) ─────────────────
            migrationBuilder.Sql(@"
                INSERT INTO public.""JenisDokumenPPID"" (""JenisDokumenPPIDID"", ""NamaJenisDokumenPPID"", ""IsActive"")
                VALUES (8, 'Tugas / Laporan Final Pemohon', true)
                ON CONFLICT (""JenisDokumenPPIDID"") DO UPDATE
                    SET ""NamaJenisDokumenPPID"" = EXCLUDED.""NamaJenisDokumenPPID"",
                        ""IsActive""             = EXCLUDED.""IsActive"";
            ");

            // ── 2. Pastikan kolom UploadTugas pada FixSeedDataConflicts sudah ada ─
            // (DokumenPPID tabel tidak perlu kolom baru — reuse skema yang ada)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM public.""JenisDokumenPPID"" WHERE ""JenisDokumenPPIDID"" = 8;
            ");
        }
    }
}
