using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PdfProcessorApi.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UploaedAt",
                table: "DocumentMetadataEntries",
                newName: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "DocumentMetadataEntries",
                newName: "UploaedAt");
        }
    }
}
