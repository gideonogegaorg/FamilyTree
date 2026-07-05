using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.FamilyTree.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLineageModeToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LineageMode",
                table: "UserProfiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LineageMode",
                table: "UserProfiles");
        }
    }
}