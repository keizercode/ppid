using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class WawancaraRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Kolom baru di PermohonanPPID ────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "NamaProdusenData",
                schema: "public",
                table: "PermohonanPPID",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoketJenis",
                schema: "public",
                table: "PermohonanPPID",
                type: "text",
                nullable: true);

            // ── Kolom JenisJadwal di JadwalPPID ─────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "JenisJadwal",
                schema: "public",
                table: "JadwalPPID",
                type: "text",
                nullable: false,
                defaultValue: "Observasi");

            // ── Status baru: Wawancara Dijadwalkan (12) & Selesai (13) ───────
            migrationBuilder.InsertData(
                schema: "public",
                table: "StatusPPID",
                columns: new[] { "StatusPPIDID", "CreatedAt", "NamaStatusPPID", "UpdatedAt" },
                values: new object[,]
                {
                    { 12, null, "Wawancara Dijadwalkan", null },
                    { 13, null, "Wawancara Selesai",     null }
                });

            // ── Migrasi data lama: set LoketJenis berdasarkan KategoriPemohon ─
            migrationBuilder.Sql(@"
                UPDATE public.""PermohonanPPID""
                SET ""LoketJenis"" = CASE
                    WHEN ""KategoriPemohon"" = 'Mahasiswa' THEN 'Kepegawaian'
                    ELSE 'Umum'
                END
                WHERE ""LoketJenis"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "StatusPPID",
                keyColumn: "StatusPPIDID",
                keyValues: new object[] { 12, 13 });

            migrationBuilder.DropColumn(
                name: "NamaProdusenData",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropColumn(
                name: "LoketJenis",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropColumn(
                name: "JenisJadwal",
                schema: "public",
                table: "JadwalPPID");
        }
    }
}
