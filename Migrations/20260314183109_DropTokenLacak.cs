using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class DropTokenLacak : Migration
    {
        /// <summary>
        /// TokenLacak was introduced as a second factor because NoPermohonan was sequential
        /// and therefore guessable by enumeration. Now that NoPermohonan is cryptographically
        /// random (see GenerateNoPermohonan in AppDbContext), the column is redundant — the
        /// public number itself is the secret. Keeping it would be dead weight and a potential
        /// source of confusion.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop index first, then column (Npgsql enforces this order)
            migrationBuilder.DropIndex(
                name: "IX_PermohonanPPID_TokenLacak",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropColumn(
                name: "TokenLacak",
                schema: "public",
                table: "PermohonanPPID");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore column + index for rollback compatibility.
            // Existing rows will have NULL; a manual backfill is required if rolling back to
            // the previous sequential-number scheme.
            migrationBuilder.AddColumn<string>(
                name: "TokenLacak",
                schema: "public",
                table: "PermohonanPPID",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermohonanPPID_TokenLacak",
                schema: "public",
                table: "PermohonanPPID",
                column: "TokenLacak");
        }
    }
}
