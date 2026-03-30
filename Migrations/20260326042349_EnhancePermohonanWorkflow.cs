using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    public partial class EnhancePermohonanWorkflow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Kolom baru di PermohonanPPID
            migrationBuilder.AddColumn<DateOnly>(
                name: "BatasWaktu",
                schema: "public",
                table: "PermohonanPPID",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TanggalSelesai",
                schema: "public",
                table: "PermohonanPPID",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pengampu",
                schema: "public",
                table: "PermohonanPPID",
                type: "text",
                nullable: true);

            // ── 2. Status baru
            migrationBuilder.InsertData(
                schema: "public",
                table: "StatusPPID",
                columns: new[] { "StatusPPIDID", "CreatedAt", "NamaStatusPPID", "UpdatedAt" },
                values: new object[,]
                {
                    { 14, null, "Menunggu Verifikasi Kasubkel", null },
                    { 15, null, "Pengisian Feedback Pemohon",   null }
                });

            // ── 3. Seed user baru
            // Password hash untuk: kasubkepeg123, kasubkdi123
            migrationBuilder.Sql(@"
                INSERT INTO public.""AppUser"" (""Username"", ""PasswordHash"", ""Role"", ""NamaLengkap"", ""IsActive"")
                VALUES
                    ('kasubkepeg',
                     'PPID_v1:' || UPPER(ENCODE(SHA256(('PPID_DLH_JKT_2025kasubkepeg123')::bytea), 'hex')),
                     'KasubkelKepegawaian', 'Kasubkel Kepegawaian', true),
                    ('kasubkdi',
                     'PPID_v1:' || UPPER(ENCODE(SHA256(('PPID_DLH_JKT_2025kasubkdi123')::bytea), 'hex')),
                     'KasubkelKDI', 'Kasubkel KDI', true)
                ON CONFLICT (""Username"") DO NOTHING;
            ");

            // ── 4. Backfill BatasWaktu (10 hari kerja dari tanggal permohonan)
            migrationBuilder.Sql(@"
                UPDATE public.""PermohonanPPID""
                SET ""BatasWaktu"" = ""TanggalPermohonan"" + INTERVAL '14 days'
                WHERE ""BatasWaktu"" IS NULL AND ""TanggalPermohonan"" IS NOT NULL;
            ");

            // ── 5. Migrate status: permohonan yang sudah di IdentifikasiAwal
            //       dan belum ada verifikasi → set ke MenungguVerifikasi(14)
            //       hanya jika masih di tahap awal (belum ada surat izin)
            migrationBuilder.Sql(@"
                -- Permohonan yang masih nunggu surat izin dianggap sudah melalui verifikasi
                -- Tidak perlu migrasi status karena flow sudah berjalan
                -- Hanya update nama tampilan saja via status seed di atas
                SELECT 1; -- no-op
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "BatasWaktu", schema: "public", table: "PermohonanPPID");
            migrationBuilder.DropColumn(name: "TanggalSelesai", schema: "public", table: "PermohonanPPID");
            migrationBuilder.DropColumn(name: "Pengampu", schema: "public", table: "PermohonanPPID");

            migrationBuilder.DeleteData(schema: "public", table: "StatusPPID", keyColumn: "StatusPPIDID", keyValues: new object[] { 14, 15 });
            migrationBuilder.Sql(@"DELETE FROM public.""AppUser"" WHERE ""Username"" IN ('kasubkepeg','kasubkdi');");
        }
    }
}
