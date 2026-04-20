using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class AddBatalkanPermohonan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tambah kolom ke PermohonanPPID ─────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "AlasanBatal",
                schema: "public",
                table: "PermohonanPPID",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DibatalkanAt",
                schema: "public",
                table: "PermohonanPPID",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DibatalkanOleh",
                schema: "public",
                table: "PermohonanPPID",
                type: "text",
                nullable: true);

            // ── 2. Tambah status baru ─────────────────────────────────────────────
            migrationBuilder.InsertData(
                schema: "public",
                table: "StatusPPID",
                columns: new[] { "StatusPPIDID", "CreatedAt", "NamaStatusPPID", "UpdatedAt" },
                values: new object[,]
                {
                    { 16, null, "Dibatalkan", null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
