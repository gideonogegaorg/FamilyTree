using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.Family.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakeOwnerIdRequiredAndAddFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove any family trees without an owner so we can make OwnerId required
            migrationBuilder.Sql("DELETE FROM \"FamilyTrees\" WHERE \"OwnerId\" IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "FamilyTrees",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyTrees_AspNetUsers_OwnerId",
                table: "FamilyTrees",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyTrees_AspNetUsers_OwnerId",
                table: "FamilyTrees");

            migrationBuilder.AlterColumn<string>(
                name: "OwnerId",
                table: "FamilyTrees",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(450)",
                oldMaxLength: 450);
        }
    }
}
