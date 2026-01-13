using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PerfectKeyV1.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelCodeToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HotelCode",
                table: "DATA_Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HotelCode",
                table: "DATA_Users");
        }
    }
}
