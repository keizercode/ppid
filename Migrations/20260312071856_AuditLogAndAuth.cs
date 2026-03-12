using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogAndAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Tabel AuditLogPPID ─────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "AuditLogPPID",
                schema: "public",
                columns: table => new
                {
                    AuditLogID = table.Column<Guid>(type: "uuid", nullable: false),
                    PermohonanPPIDID = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusLama = table.Column<int>(type: "integer", nullable: true),
                    StatusBaru = table.Column<int>(type: "integer", nullable: true),
                    Keterangan = table.Column<string>(type: "text", nullable: true),
                    Operator = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogPPID", x => x.AuditLogID);
                    table.ForeignKey(
                        name: "FK_AuditLogPPID_PermohonanPPID_PermohonanPPIDID",
                        column: x => x.PermohonanPPIDID,
                        principalSchema: "public",
                        principalTable: "PermohonanPPID",
                        principalColumn: "PermohonanPPIDID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogPPID_PermohonanPPIDID",
                schema: "public",
                table: "AuditLogPPID",
                column: "PermohonanPPIDID");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogPPID_CreatedAt",
                schema: "public",
                table: "AuditLogPPID",
                column: "CreatedAt");

            // ── 2. Tabel AppUser ──────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "AppUser",
                schema: "public",
                columns: table => new
                {
                    AppUserID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    NamaLengkap = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppUser", x => x.AppUserID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUser_Username",
                schema: "public",
                table: "AppUser",
                column: "Username",
                unique: true);

            // ── 3. Unique index NoPermohonan ──────────────────────────────────
            // Cek dulu apakah sudah ada index ini (agar tidak error kalau migration dijalankan ulang)
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

            // ── 4. Seed default users ─────────────────────────────────────────
            // PasswordHash = SHA-256("PPID_DLH_JKT_2025" + password), prefix "PPID_v1:"
            // Gunakan AppUser.HashPassword(password) untuk generate hash baru.
            migrationBuilder.Sql(@"
                INSERT INTO public.""AppUser"" (""Username"", ""PasswordHash"", ""Role"", ""NamaLengkap"", ""IsActive"")
                VALUES
                    ('loket',
                     'PPID_v1:95EDA02C6F17D32F74088B0D32402439A7312500A1D46891111FF1B20F864F83',
                     'Loket', 'Petugas Loket', true),

                    ('kepegawaian',
                     'PPID_v1:ED8718E7F7645AFABF98B3924D86410342D35D474292B51CCD8F1F4953DB1E2E',
                     'Kepegawaian', 'Subkelompok Kepegawaian', true),

                    ('kdi',
                     'PPID_v1:B15B33F10F54FBDB3E1A76BBE63712F069A81B5BA417B8A1E5ED41F65252F824',
                     'KDI', 'Petugas KDI', true),

                    ('produsen',
                     'PPID_v1:9C2E44362826AB11EED179D28CA7A51C6BA9D2A9BEA561B38FA2EE0A6EAD917A',
                     'ProdusenData', 'Produsen Data', true),

                    ('admin',
                     'PPID_v1:81A9C5C1602F933DB6636C8B1A6AFACCDE5DC085C706FA68CD10820D1F22D685',
                     'Admin', 'Administrator', true)
                ON CONFLICT (""Username"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM public.""AppUser"" WHERE ""Username"" IN ('loket','kepegawaian','kdi','produsen','admin');");

            migrationBuilder.DropIndex(
                name: "IX_PermohonanPPID_NoPermohonan",
                schema: "public",
                table: "PermohonanPPID");

            migrationBuilder.DropTable(name: "AuditLogPPID", schema: "public");
            migrationBuilder.DropTable(name: "AppUser", schema: "public");
        }
    }
}
