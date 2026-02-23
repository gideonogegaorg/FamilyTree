using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GMO.Family.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyMembersAndRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FamilyMembers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyTreeId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NickName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DOB = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BirthOrder = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyMembers_FamilyTrees_FamilyTreeId",
                        column: x => x.FamilyTreeId,
                        principalTable: "FamilyTrees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyMemberRelationships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyTreeId = table.Column<long>(type: "bigint", nullable: false),
                    FromMemberId = table.Column<long>(type: "bigint", nullable: false),
                    ToMemberId = table.Column<long>(type: "bigint", nullable: false),
                    RelationshipType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyMemberRelationships", x => x.Id);
                    table.CheckConstraint("CK_FamilyMemberRelationship_FromNotTo", "\"FromMemberId\" != \"ToMemberId\"");
                    table.ForeignKey(
                        name: "FK_FamilyMemberRelationships_FamilyMembers_FromMemberId",
                        column: x => x.FromMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMemberRelationships_FamilyMembers_ToMemberId",
                        column: x => x.ToMemberId,
                        principalTable: "FamilyMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FamilyMemberRelationships_FamilyTrees_FamilyTreeId",
                        column: x => x.FamilyTreeId,
                        principalTable: "FamilyTrees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMemberRelationships_FamilyTreeId",
                table: "FamilyMemberRelationships",
                column: "FamilyTreeId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMemberRelationships_FromMemberId_ToMemberId_Relations~",
                table: "FamilyMemberRelationships",
                columns: new[] { "FromMemberId", "ToMemberId", "RelationshipType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMemberRelationships_ToMemberId",
                table: "FamilyMemberRelationships",
                column: "ToMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_FamilyTreeId",
                table: "FamilyMembers",
                column: "FamilyTreeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FamilyMemberRelationships");

            migrationBuilder.DropTable(
                name: "FamilyMembers");
        }
    }
}
