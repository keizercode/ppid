using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    public partial class AddTeleponPIC : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TeleponPIC di JadwalPPID
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'JadwalPPID'
                          AND column_name  = 'TeleponPIC'
                    ) THEN
                        ALTER TABLE public.""JadwalPPID""
                            ADD COLUMN ""TeleponPIC"" text;
                    END IF;
                END$$;
            ");

            // TeleponPIC di SubTaskPPID
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'SubTaskPPID'
                          AND column_name  = 'TeleponPIC'
                    ) THEN
                        ALTER TABLE public.""SubTaskPPID""
                            ADD COLUMN ""TeleponPIC"" text;
                    END IF;
                END$$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.""JadwalPPID"" DROP COLUMN IF EXISTS ""TeleponPIC"";
                ALTER TABLE public.""SubTaskPPID"" DROP COLUMN IF EXISTS ""TeleponPIC"";
            ");
        }
    }
}
