using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.FamilyTree.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentFamilyTreeIdToUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CurrentFamilyTreeId",
                table: "UserProfiles",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentFamilyTreeId",
                table: "UserProfiles");
        }
    }
}