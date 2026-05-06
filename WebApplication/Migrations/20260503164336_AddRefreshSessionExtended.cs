using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshSessionExtended : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "refresh_session_extended",
                table: "user",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "refresh_session_extended",
                table: "user");
        }
    }
}
