using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.Family.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSiblingRelationshipsAndOrphans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RelationshipType: 0 = Parent, 1 = Sibling, 2 = Couple. Remove all Sibling (1) relationships.
            migrationBuilder.Sql(@"
                DELETE FROM ""FamilyMemberRelationships""
                WHERE ""RelationshipType"" = 1;");

            // Remove orphan members: no remaining relationships and not linked to a user (UserId IS NULL).
            migrationBuilder.Sql(@"
                DELETE FROM ""FamilyMembers"" m
                WHERE (m.""UserId"" IS NULL OR m.""UserId"" = '')
                  AND NOT EXISTS (
                    SELECT 1 FROM ""FamilyMemberRelationships"" r
                    WHERE r.""FromMemberId"" = m.""Id"" OR r.""ToMemberId"" = m.""Id""
                  );");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No way to restore deleted sibling relationships or orphan members.
        }
    }
}
