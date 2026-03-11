using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "JenisDokumenPPID",
                schema: "public",
                columns: table => new
                {
                    JenisDokumenPPIDID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NamaJenisDokumenPPID = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JenisDokumenPPID", x => x.JenisDokumenPPIDID);
                });

            migrationBuilder.CreateTable(
                name: "Keperluan",
                schema: "public",
                columns: table => new
                {
                    KeperluanID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NamaKeperluan = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keperluan", x => x.KeperluanID);
                });

            migrationBuilder.CreateTable(
                name: "Pribadi",
                schema: "public",
                columns: table => new
                {
                    PribadiID = table.Column<Guid>(type: "uuid", nullable: false),
                    NIK = table.Column<string>(type: "text", nullable: true),
                    Nama = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Alamat = table.Column<string>(type: "text", nullable: true),
                    RT = table.Column<string>(type: "text", nullable: true),
                    RW = table.Column<string>(type: "text", nullable: true),
                    KelurahanID = table.Column<string>(type: "text", nullable: true),
                    KecamatanID = table.Column<string>(type: "text", nullable: true),
                    KabupatenID = table.Column<string>(type: "text", nullable: true),
                    NamaKelurahan = table.Column<string>(type: "text", nullable: true),
                    NamaKecamatan = table.Column<string>(type: "text", nullable: true),
                    NamaKabupaten = table.Column<string>(type: "text", nullable: true),
                    Telepon = table.Column<string>(type: "text", nullable: true),
                    Kelamin = table.Column<bool>(type: "boolean", nullable: true),
                    IsKendaraan = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pribadi", x => x.PribadiID);
                });

            migrationBuilder.CreateTable(
                name: "StatusPPID",
                schema: "public",
                columns: table => new
                {
                    StatusPPIDID = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NamaStatusPPID = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusPPID", x => x.StatusPPIDID);
                });

            migrationBuilder.CreateTable(
                name: "PribadiPPID",
                schema: "public",
                columns: table => new
                {
                    PribadiPPIDID = table.Column<Guid>(type: "uuid", nullable: false),
                    PribadiID = table.Column<Guid>(type: "uuid", nullable: true),
                    ProvinsiID = table.Column<string>(type: "text", nullable: true),
                    NamaProvinsi = table.Column<string>(type: "text", nullable: true),
                    Lembaga = table.Column<string>(type: "text", nullable: true),
                    Fakultas = table.Column<string>(type: "text", nullable: true),
                    Jurusan = table.Column<string>(type: "text", nullable: true),
                    pekerjaan = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PribadiPPID", x => x.PribadiPPIDID);
                    table.ForeignKey(
                        name: "FK_PribadiPPID_Pribadi_PribadiID",
                        column: x => x.PribadiID,
                        principalSchema: "public",
                        principalTable: "Pribadi",
                        principalColumn: "PribadiID");
                });

            migrationBuilder.CreateTable(
                name: "PermohonanPPID",
                schema: "public",
                columns: table => new
                {
                    PermohonanPPIDID = table.Column<Guid>(type: "uuid", nullable: false),
                    PribadiID = table.Column<Guid>(type: "uuid", nullable: true),
                    NoPermohonan = table.Column<string>(type: "text", nullable: true),
                    KategoriPemohon = table.Column<string>(type: "text", nullable: true),
                    NoSuratPermohonan = table.Column<string>(type: "text", nullable: true),
                    TanggalPermohonan = table.Column<DateOnly>(type: "date", nullable: true),
                    JudulPenelitian = table.Column<string>(type: "text", nullable: true),
                    LatarBelakang = table.Column<string>(type: "text", nullable: true),
                    TujuanPermohonan = table.Column<string>(type: "text", nullable: true),
                    IsObservasi = table.Column<bool>(type: "boolean", nullable: false),
                    IsWawancara = table.Column<bool>(type: "boolean", nullable: false),
                    IsPermintaanData = table.Column<bool>(type: "boolean", nullable: false),
                    StatusPPIDID = table.Column<int>(type: "integer", nullable: true),
                    Sequance = table.Column<int>(type: "integer", nullable: true),
                    CratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BidangID = table.Column<Guid>(type: "uuid", nullable: true),
                    NamaBidang = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermohonanPPID", x => x.PermohonanPPIDID);
                    table.ForeignKey(
                        name: "FK_PermohonanPPID_Pribadi_PribadiID",
                        column: x => x.PribadiID,
                        principalSchema: "public",
                        principalTable: "Pribadi",
                        principalColumn: "PribadiID");
                    table.ForeignKey(
                        name: "FK_PermohonanPPID_StatusPPID_StatusPPIDID",
                        column: x => x.StatusPPIDID,
                        principalSchema: "public",
                        principalTable: "StatusPPID",
                        principalColumn: "StatusPPIDID");
                });

            migrationBuilder.CreateTable(
                name: "DokumenPPID",
                schema: "public",
                columns: table => new
                {
                    DokumenPPIDID = table.Column<Guid>(type: "uuid", nullable: false),
                    NamaDokumenPPID = table.Column<string>(type: "text", nullable: true),
                    PermohonanPPIDID = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadDokumenPPID = table.Column<string>(type: "text", nullable: true),
                    JenisDokumenPPIDID = table.Column<int>(type: "integer", nullable: true),
                    NamaJenisDokumenPPID = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokumenPPID", x => x.DokumenPPIDID);
                    table.ForeignKey(
                        name: "FK_DokumenPPID_JenisDokumenPPID_JenisDokumenPPIDID",
                        column: x => x.JenisDokumenPPIDID,
                        principalSchema: "public",
                        principalTable: "JenisDokumenPPID",
                        principalColumn: "JenisDokumenPPIDID");
                    table.ForeignKey(
                        name: "FK_DokumenPPID_PermohonanPPID_PermohonanPPIDID",
                        column: x => x.PermohonanPPIDID,
                        principalSchema: "public",
                        principalTable: "PermohonanPPID",
                        principalColumn: "PermohonanPPIDID");
                });

            migrationBuilder.CreateTable(
                name: "JadwalPPID",
                schema: "public",
                columns: table => new
                {
                    JadwalPPIDID = table.Column<Guid>(type: "uuid", nullable: false),
                    PermohonanPPIDID = table.Column<Guid>(type: "uuid", nullable: true),
                    Tanggal = table.Column<DateOnly>(type: "date", nullable: true),
                    Waktu = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    NamaPIC = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JadwalPPID", x => x.JadwalPPIDID);
                    table.ForeignKey(
                        name: "FK_JadwalPPID_PermohonanPPID_PermohonanPPIDID",
                        column: x => x.PermohonanPPIDID,
                        principalSchema: "public",
                        principalTable: "PermohonanPPID",
                        principalColumn: "PermohonanPPIDID");
                });

            migrationBuilder.CreateTable(
                name: "PermohonanPPIDDetail",
                schema: "public",
                columns: table => new
                {
                    PermohonanPPIDDetailID = table.Column<Guid>(type: "uuid", nullable: false),
                    PermohonanPPIDID = table.Column<Guid>(type: "uuid", nullable: true),
                    KeperluanID = table.Column<int>(type: "integer", nullable: true),
                    DetailKeperluan = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermohonanPPIDDetail", x => x.PermohonanPPIDDetailID);
                    table.ForeignKey(
                        name: "FK_PermohonanPPIDDetail_Keperluan_KeperluanID",
                        column: x => x.KeperluanID,
                        principalSchema: "public",
                        principalTable: "Keperluan",
                        principalColumn: "KeperluanID");
                    table.ForeignKey(
                        name: "FK_PermohonanPPIDDetail_PermohonanPPID_PermohonanPPIDID",
                        column: x => x.PermohonanPPIDID,
                        principalSchema: "public",
                        principalTable: "PermohonanPPID",
                        principalColumn: "PermohonanPPIDID");
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "JenisDokumenPPID",
                columns: new[] { "JenisDokumenPPIDID", "CreatedAt", "IsActive", "NamaJenisDokumenPPID", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, null, true, "KTP", null },
                    { 2, null, true, "Surat Permohonan", null },
                    { 3, null, true, "Proposal Penelitian", null },
                    { 4, null, true, "Akta Notaris", null },
                    { 5, null, true, "Dokumen Identifikasi (TTD)", null },
                    { 6, null, true, "Surat Izin", null },
                    { 7, null, true, "Data Hasil", null }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "Keperluan",
                columns: new[] { "KeperluanID", "CreatedAt", "NamaKeperluan", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, null, "Observasi", null },
                    { 2, null, "Permintaan Data", null },
                    { 3, null, "Wawancara", null }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "StatusPPID",
                columns: new[] { "StatusPPIDID", "CreatedAt", "NamaStatusPPID", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, null, "Baru", null },
                    { 2, null, "Terdaftar", null },
                    { 3, null, "Identifikasi Awal", null },
                    { 4, null, "Menunggu Surat Izin", null },
                    { 5, null, "Surat Izin Terbit", null },
                    { 6, null, "Didisposisi", null },
                    { 7, null, "Sedang Diproses", null },
                    { 8, null, "Observasi Dijadwalkan", null },
                    { 9, null, "Observasi Selesai", null },
                    { 10, null, "Data Siap", null },
                    { 11, null, "Selesai", null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DokumenPPID_JenisDokumenPPIDID",
                schema: "public",
                table: "DokumenPPID",
                column: "JenisDokumenPPIDID");

            migrationBuilder.CreateIndex(
                name: "IX_DokumenPPID_PermohonanPPIDID",
                schema: "public",
                table: "DokumenPPID",
                column: "PermohonanPPIDID");

            migrationBuilder.CreateIndex(
                name: "IX_JadwalPPID_PermohonanPPIDID",
                schema: "public",
                table: "JadwalPPID",
                column: "PermohonanPPIDID");

            migrationBuilder.CreateIndex(
                name: "IX_PermohonanPPID_PribadiID",
                schema: "public",
                table: "PermohonanPPID",
                column: "PribadiID");

            migrationBuilder.CreateIndex(
                name: "IX_PermohonanPPID_StatusPPIDID",
                schema: "public",
                table: "PermohonanPPID",
                column: "StatusPPIDID");

            migrationBuilder.CreateIndex(
                name: "IX_PermohonanPPIDDetail_KeperluanID",
                schema: "public",
                table: "PermohonanPPIDDetail",
                column: "KeperluanID");

            migrationBuilder.CreateIndex(
                name: "IX_PermohonanPPIDDetail_PermohonanPPIDID",
                schema: "public",
                table: "PermohonanPPIDDetail",
                column: "PermohonanPPIDID");

            migrationBuilder.CreateIndex(
                name: "IX_PribadiPPID_PribadiID",
                schema: "public",
                table: "PribadiPPID",
                column: "PribadiID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DokumenPPID",
                schema: "public");

            migrationBuilder.DropTable(
                name: "JadwalPPID",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PermohonanPPIDDetail",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PribadiPPID",
                schema: "public");

            migrationBuilder.DropTable(
                name: "JenisDokumenPPID",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Keperluan",
                schema: "public");

            migrationBuilder.DropTable(
                name: "PermohonanPPID",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Pribadi",
                schema: "public");

            migrationBuilder.DropTable(
                name: "StatusPPID",
                schema: "public");
        }
    }
}
