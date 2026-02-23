using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.Family.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueUserIdPerFamilyTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear duplicate (FamilyTreeId, UserId): keep one per pair (smallest Id), clear UserId on the rest.
            migrationBuilder.Sql(@"
                UPDATE ""FamilyMembers"" m
                SET ""UserId"" = NULL
                WHERE m.""UserId"" IS NOT NULL
                  AND EXISTS (
                    SELECT 1 FROM ""FamilyMembers"" m2
                    WHERE m2.""FamilyTreeId"" = m.""FamilyTreeId"" AND m2.""UserId"" = m.""UserId"" AND m2.""Id"" < m.""Id""
                  );");
            migrationBuilder.CreateIndex(
                name: "IX_FamilyMembers_FamilyTreeId_UserId",
                table: "FamilyMembers",
                columns: new[] { "FamilyTreeId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FamilyMembers_FamilyTreeId_UserId",
                table: "FamilyMembers");
        }
    }
}
