using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.Family.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTreePathModeToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TreePathMode",
                table: "UserProfiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TreePathMode",
                table: "UserProfiles");
        }
    }
}
