using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.FamilyTree.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfDeath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DOD",
                table: "FamilyMembers",
                type: "date",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FamilyMember_DOD_After_DOB",
                table: "FamilyMembers",
                sql: "\"DOD\" IS NULL OR \"DOB\" IS NULL OR \"DOD\" >= \"DOB\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FamilyMember_DOD_After_DOB",
                table: "FamilyMembers");

            migrationBuilder.DropColumn(
                name: "DOD",
                table: "FamilyMembers");
        }
    }
}