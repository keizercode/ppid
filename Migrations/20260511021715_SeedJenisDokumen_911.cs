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
    migrationBuilder.Sql(@"
        ALTER TABLE public.""PermohonanPPID""
            ADD COLUMN IF NOT EXISTS ""AlasanBatal""    text,
            ADD COLUMN IF NOT EXISTS ""DibatalkanAt""   timestamp with time zone,
            ADD COLUMN IF NOT EXISTS ""DibatalkanOleh"" text;
    ");

    migrationBuilder.Sql(@"
        INSERT INTO public.""JenisDokumenPPID"" (""JenisDokumenPPIDID"", ""CreatedAt"", ""IsActive"", ""NamaJenisDokumenPPID"", ""UpdatedAt"")
        VALUES
            (9,  NULL, TRUE, 'Data Hasil Observasi',       NULL),
            (10, NULL, TRUE, 'Data Hasil Wawancara',       NULL),
            (11, NULL, TRUE, 'Data Hasil Permintaan Data', NULL)
        ON CONFLICT (""JenisDokumenPPIDID"") DO NOTHING;
    ");

    migrationBuilder.Sql(@"
        INSERT INTO public.""StatusPPID"" (""StatusPPIDID"", ""CreatedAt"", ""NamaStatusPPID"", ""UpdatedAt"")
        VALUES (16, NULL, 'Dibatalkan', NULL)
        ON CONFLICT (""StatusPPIDID"") DO NOTHING;
    ");
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
