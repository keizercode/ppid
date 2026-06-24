using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PermintaanData.Migrations
{
    /// <inheritdoc />
    public partial class AddSumberRegistrasi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SumberRegistrasi",
                schema: "public",
                table: "PermohonanPPID",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SumberRegistrasi",
                schema: "public",
                table: "PermohonanPPID");
        }
    }
}
