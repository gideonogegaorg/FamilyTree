using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.Family.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoKeysAndTreeCardViewMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoKey",
                table: "UserProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TreeCardViewMode",
                table: "UserProfiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoKey",
                table: "FamilyMembers",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoKey",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "TreeCardViewMode",
                table: "UserProfiles");

            migrationBuilder.DropColumn(
                name: "PhotoKey",
                table: "FamilyMembers");
        }
    }
}