using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PerfectKeyV1.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateElementTypeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElementElementTypes_ElementId",
                table: "ElementElementTypes");

            migrationBuilder.CreateIndex(
                name: "IX_ElementTypes_Name",
                table: "ElementTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ElementElementTypes_ElementId_ElementTypeId",
                table: "ElementElementTypes",
                columns: new[] { "ElementId", "ElementTypeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ElementTypes_Name",
                table: "ElementTypes");

            migrationBuilder.DropIndex(
                name: "IX_ElementElementTypes_ElementId_ElementTypeId",
                table: "ElementElementTypes");

            migrationBuilder.CreateIndex(
                name: "IX_ElementElementTypes_ElementId",
                table: "ElementElementTypes",
                column: "ElementId");
        }
    }
}
