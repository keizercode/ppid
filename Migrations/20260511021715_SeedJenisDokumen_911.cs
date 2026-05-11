using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class SeedJenisDokumen_911 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
{
    // Gunakan raw SQL IF NOT EXISTS agar aman di production yang kolom sudah ada
    migrationBuilder.Sql(@"
        ALTER TABLE public.""PermohonanPPID""
            ADD COLUMN IF NOT EXISTS ""AlasanBatal""    text,
            ADD COLUMN IF NOT EXISTS ""DibatalkanAt""   timestamp with time zone,
            ADD COLUMN IF NOT EXISTS ""DibatalkanOleh"" text;
    ");

    migrationBuilder.InsertData(
        schema: "public",
        table: "JenisDokumenPPID",
        columns: new[] { "JenisDokumenPPIDID", "CreatedAt", "IsActive", "NamaJenisDokumenPPID", "UpdatedAt" },
        values: new object[,]
        {
            { 9,  null, true, "Data Hasil Observasi",       null },
            { 10, null, true, "Data Hasil Wawancara",       null },
            { 11, null, true, "Data Hasil Permintaan Data", null }
        });

    migrationBuilder.InsertData(
        schema: "public",
        table: "StatusPPID",
        columns: new[] { "StatusPPIDID", "CreatedAt", "NamaStatusPPID", "UpdatedAt" },
        values: new object[] { 16, null, "Dibatalkan", null });
}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "JenisDokumenPPID",
                keyColumn: "JenisDokumenPPIDID",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "JenisDokumenPPID",
                keyColumn: "JenisDokumenPPIDID",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "JenisDokumenPPID",
                keyColumn: "JenisDokumenPPIDID",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "public",
                table: "StatusPPID",
                keyColumn: "StatusPPIDID",
                keyValue: 16);

            migrationBuilder.DropColumn(
                name: "AlasanBatal",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropColumn(
                name: "DibatalkanAt",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropColumn(
                name: "DibatalkanOleh",
                schema: "public",
                table: "PermohonanPPID");
        }
    }
}
