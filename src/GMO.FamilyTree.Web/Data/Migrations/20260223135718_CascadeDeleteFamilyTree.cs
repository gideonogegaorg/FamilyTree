using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.FamilyTree.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteFamilyTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_FromMemberId",
                table: "FamilyMemberRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_ToMemberId",
                table: "FamilyMemberRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyTrees_FamilyTreeId",
                table: "FamilyMemberRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMembers_FamilyTrees_FamilyTreeId",
                table: "FamilyMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_FromMemberId",
                table: "FamilyMemberRelationships",
                column: "FromMemberId",
                principalTable: "FamilyMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_ToMemberId",
                table: "FamilyMemberRelationships",
                column: "ToMemberId",
                principalTable: "FamilyMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyTrees_FamilyTreeId",
                table: "FamilyMemberRelationships",
                column: "FamilyTreeId",
                principalTable: "FamilyTrees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMembers_FamilyTrees_FamilyTreeId",
                table: "FamilyMembers",
                column: "FamilyTreeId",
                principalTable: "FamilyTrees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_FromMemberId",
                table: "FamilyMemberRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_ToMemberId",
                table: "FamilyMemberRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyTrees_FamilyTreeId",
                table: "FamilyMemberRelationships");

            migrationBuilder.DropForeignKey(
                name: "FK_FamilyMembers_FamilyTrees_FamilyTreeId",
                table: "FamilyMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_FromMemberId",
                table: "FamilyMemberRelationships",
                column: "FromMemberId",
                principalTable: "FamilyMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyMembers_ToMemberId",
                table: "FamilyMemberRelationships",
                column: "ToMemberId",
                principalTable: "FamilyMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMemberRelationships_FamilyTrees_FamilyTreeId",
                table: "FamilyMemberRelationships",
                column: "FamilyTreeId",
                principalTable: "FamilyTrees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FamilyMembers_FamilyTrees_FamilyTreeId",
                table: "FamilyMembers",
                column: "FamilyTreeId",
                principalTable: "FamilyTrees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}