using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMO.FamilyTree.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDOBToDateOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""FamilyMembers"" ALTER COLUMN ""DOB"" TYPE date USING ""DOB""::date;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""FamilyMembers"" ALTER COLUMN ""DOB"" TYPE timestamp with time zone USING ""DOB""::timestamp with time zone;");
        }
    }
}