using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    public partial class AddTeleponPengampu : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name   = 'PermohonanPPID'
                          AND column_name  = 'TeleponPengampu'
                    ) THEN
                        ALTER TABLE public.""PermohonanPPID""
                            ADD COLUMN ""TeleponPengampu"" text;
                    END IF;
                END$$;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.""PermohonanPPID""
                    DROP COLUMN IF EXISTS ""TeleponPengampu"";
            ");
        }
    }
}
