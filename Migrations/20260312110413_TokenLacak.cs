using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class TokenLacak : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tambah kolom TokenLacak ────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "TokenLacak",
                schema: "public",
                table: "PermohonanPPID",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // ── 2. Index untuk lookup cepat ───────────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_PermohonanPPID_TokenLacak",
                schema: "public",
                table: "PermohonanPPID",
                column: "TokenLacak");

            // ── 3. Backfill token untuk data lama ─────────────────────────────
            // Menggunakan md5(random()) — tidak butuh extension apapun.
            // Hasilnya diambil 6 karakter dari hex md5, lalu diubah ke uppercase.
            // Karakter hex (0-9, A-F) sudah cukup aman untuk keperluan ini.
            migrationBuilder.Sql(@"
                UPDATE public.""PermohonanPPID""
                SET ""TokenLacak"" = UPPER(SUBSTRING(MD5(RANDOM()::TEXT || CLOCK_TIMESTAMP()::TEXT), 1, 6))
                WHERE ""TokenLacak"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PermohonanPPID_TokenLacak",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropColumn(
                name: "TokenLacak",
                schema: "public",
                table: "PermohonanPPID");
        }
    }
}
