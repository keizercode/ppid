using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    public partial class AddSubTaskParallel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
{
    // ── 1. CREATE TABLE ─────────────────────────────────────────────
    migrationBuilder.CreateTable(
        name: "SubTaskPPID",
        schema: "public",
        columns: table => new
        {
            SubTaskID        = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
            PermohonanPPIDID = table.Column<Guid>(type: "uuid", nullable: false),
            JenisTask        = table.Column<string>(type: "text", nullable: false),
            StatusTask       = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
            FilePath         = table.Column<string>(type: "text", nullable: true),
            NamaFile         = table.Column<string>(type: "text", nullable: true),
            Catatan          = table.Column<string>(type: "text", nullable: true),
            NamaPIC          = table.Column<string>(type: "text", nullable: true),
            TanggalJadwal    = table.Column<DateOnly>(type: "date", nullable: true),
            WaktuJadwal      = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
            Operator         = table.Column<string>(type: "text", nullable: true),
            CreatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
            SelesaiAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            UpdatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_SubTaskPPID", x => x.SubTaskID);
            table.ForeignKey(
                name: "FK_SubTaskPPID_PermohonanPPID_PermohonanPPIDID",
                column: x => x.PermohonanPPIDID,
                principalSchema: "public",
                principalTable: "PermohonanPPID",
                principalColumn: "PermohonanPPIDID",
                onDelete: ReferentialAction.Cascade);
        });

    migrationBuilder.CreateIndex(
        name: "IX_SubTaskPPID_PermohonanPPIDID",
        schema: "public",
        table: "SubTaskPPID",
        column: "PermohonanPPIDID");

    migrationBuilder.CreateIndex(
        name: "IX_SubTaskPPID_PermohonanPPIDID_JenisTask",
        schema: "public",
        table: "SubTaskPPID",
        columns: new[] { "PermohonanPPIDID", "JenisTask" });

    // ── 2. BACKFILL DATA ────────────────────────────────────────────
    migrationBuilder.Sql(@"

        -- PermintaanData
        INSERT INTO public.""SubTaskPPID""
            (""SubTaskID"",""PermohonanPPIDID"",""JenisTask"",""StatusTask"",""Operator"",""CreatedAt"")
        SELECT
            gen_random_uuid(),
            p.""PermohonanPPIDID"",
            'PermintaanData',
            CASE WHEN p.""StatusPPIDID"" >= 10 THEN 2 ELSE 1 END,
            'Migration',
            COALESCE(p.""UpdatedAt"", p.""CratedAt"", NOW())
        FROM public.""PermohonanPPID"" p
        WHERE p.""IsPermintaanData"" = true
          AND p.""StatusPPIDID"" >= 6
        ON CONFLICT DO NOTHING;

        -- Observasi
        INSERT INTO public.""SubTaskPPID""
            (""SubTaskID"",""PermohonanPPIDID"",""JenisTask"",""StatusTask"",""Operator"",""CreatedAt"")
        SELECT
            gen_random_uuid(),
            p.""PermohonanPPIDID"",
            'Observasi',
            CASE
                WHEN p.""StatusPPIDID"" >= 9 THEN 2
                WHEN p.""StatusPPIDID"" = 8 THEN 1
                ELSE 1
            END,
            'Migration',
            COALESCE(p.""UpdatedAt"", p.""CratedAt"", NOW())
        FROM public.""PermohonanPPID"" p
        WHERE p.""IsObservasi"" = true
          AND p.""StatusPPIDID"" >= 6
        ON CONFLICT DO NOTHING;

        -- Wawancara
        INSERT INTO public.""SubTaskPPID""
            (""SubTaskID"",""PermohonanPPIDID"",""JenisTask"",""StatusTask"",""Operator"",""CreatedAt"")
        SELECT
            gen_random_uuid(),
            p.""PermohonanPPIDID"",
            'Wawancara',
            CASE
                WHEN p.""StatusPPIDID"" = 13 THEN 2
                WHEN p.""StatusPPIDID"" = 12 THEN 1
                ELSE 1
            END,
            'Migration',
            COALESCE(p.""UpdatedAt"", p.""CratedAt"", NOW())
        FROM public.""PermohonanPPID"" p
        WHERE p.""IsWawancara"" = true
          AND p.""StatusPPIDID"" >= 6
        ON CONFLICT DO NOTHING;

        -- Update dari JadwalPPID
        UPDATE public.""SubTaskPPID"" st
        SET
            ""TanggalJadwal"" = j.""Tanggal"",
            ""WaktuJadwal""   = j.""Waktu"",
            ""NamaPIC""       = j.""NamaPIC"",
            ""UpdatedAt""     = NOW()
        FROM public.""JadwalPPID"" j
        WHERE j.""PermohonanPPIDID"" = st.""PermohonanPPIDID""
          AND j.""JenisJadwal""      = st.""JenisTask""
          AND st.""TanggalJadwal"" IS NULL;

        -- Update dari DokumenPPID
        UPDATE public.""SubTaskPPID"" st
        SET
            ""FilePath""  = d.""UploadDokumenPPID"",
            ""NamaFile""  = d.""NamaDokumenPPID"",
            ""UpdatedAt"" = NOW()
        FROM public.""DokumenPPID"" d
        WHERE d.""PermohonanPPIDID""   = st.""PermohonanPPIDID""
          AND d.""JenisDokumenPPIDID"" = 7
          AND st.""FilePath"" IS NULL
          AND st.""JenisTask"" IN ('PermintaanData', 'Wawancara');

    ");
}

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SubTaskPPID", schema: "public");
        }
    }
}
