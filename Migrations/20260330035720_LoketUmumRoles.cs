using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable

namespace PermintaanData.Migrations
{
    public partial class LoketUmumRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed user LoketUmum dan KasubkelUmum
            migrationBuilder.Sql(@"
                INSERT INTO public.""AppUser"" (""Username"", ""PasswordHash"", ""Role"", ""NamaLengkap"", ""IsActive"")
                VALUES
                    ('loketumum',
                     'PPID_v1:' || UPPER(ENCODE(SHA256(CONVERT_TO('PPID_DLH_JKT_2025loketumum123','UTF8')),'hex')),
                     'LoketUmum', 'Petugas Loket Umum', true),
                    ('kasubkelumum',
                     'PPID_v1:' || UPPER(ENCODE(SHA256(CONVERT_TO('PPID_DLH_JKT_2025kasubkelumum123','UTF8')),'hex')),
                     'KasubkelUmum', 'Kasubkel Umum', true)
                ON CONFLICT (""Username"") DO NOTHING;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM public.""AppUser""
                WHERE ""Username"" IN ('loketumum','kasubkelumum');
            ");
        }
    }
}
