# Database Setup and Configuration

PostgreSQL setup, seeding, and maintenance for the Family Tree application.

---

## Database Configuration

### Connection String

The database connection is configured in `appsettings.json` (note: this file is git ignored for security reasons).

**Typical Connection String Format:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=family;Username=family;Password=your_password"
  }
}
```

### Finding Your Connection String

`appsettings.json` is git ignored. Check local `appsettings.json` or env vars (`DB_HOST`, `DB_NAME`, `DB_USER`, `DB_PASSWORD`). Common defaults: Host `localhost`, Database `family`, User `family`, Port `5432`. See [testing-environment.md](testing-environment.md#finding-your-database-connection) for more options.

### Environment Variables

For production deployments, use environment variables:

```bash
DB_HOST=localhost
DB_NAME=family
DB_USER=family
DB_PASSWORD=your_password
```

**Note**: The actual connection details may vary based on your local setup. Check your `appsettings.json` for the exact values.

---

## Database Schema

### Key Tables

| Table | Purpose | Key Columns |
|---|---|---|
| `AspNetUsers` | User accounts | `Id`, `Email`, `UserName` |
| `FamilyTrees` | Trees owned by users | `Id`, `Uid`, `Name`, `OwnerId` |
| `FamilyMembers` | Members in a tree | `Id`, `FamilyTreeId`, `Name`, `NickName`, `DOB`, `DOD`, `BirthOrder`, `IsMale`, `UserId`, `PhotoKey` |
| `FamilyMemberRelationships` | Edges between members | `FromMemberId`, `ToMemberId`, `RelationshipType` (`Parent` / `Couple`) |
| `FamilyTreeAccesses` | Collaborators | `FamilyTreeId`, `UserId`, `Role` |
| `FamilyTreeInvites` | Pending link/email invites | `Token`, `Email`, `Role`, `ExpiresAt` |
| `UserProfiles` | User preferences | `UserId`, `TreeViewOrientation`, `LineageMode`, `PhotoKey`, `TreeCardViewMode`, `CurrentFamilyTreeId` |

`DOD` (date of death) is optional; DB check enforces `DOD >= DOB` when both are set (migration `20260719183527_AddDateOfDeath`).

### Entity Framework Migrations

```bash
# From repo root (design-time factory uses local appsettings)
dotnet ef migrations add MigrationName --project src/GMO.FamilyTree.Web
dotnet ef database update --project src/GMO.FamilyTree.Web
dotnet ef migrations remove --project src/GMO.FamilyTree.Web
```

---

## Data Seeding

### Seeding Strategy

Seed data is **deterministic SQL** in the UI test project. Default tree IDs (overridable via `_seed_vars` in the script):

| Tree ID | Name | Purpose |
|---|---|---|
| **1** | 3-Gen Test Tree | Primary layout/UI seed (**25** members, IDs 50–74) |
| **2** | Empty Tree | Empty-tree flows |
| **3** | Single Member Tree | One member (ID 75) |
| **4** | Large Tree (6 Gen) | Stress / large-layout spot checks |

Owner preference: `test@example.com` if present, otherwise the first `AspNetUsers` row. **"Me"** is member **56** on tree **1** (`UserId` + sample `DOB` set by the script).

### Seed Data Location

```
tst/GMO.FamilyTree.Web.UiTests/Data/seed_trees.sql
```

### Seed Data Structure (primary tree IDs 50–74)

```
Grandparents: Paternal Grandma/Grandpa (50–51), Maternal Grandma + Grandpa 1/2 (52–54),
              plus same-sex/extra partners (68–70)
Parents:      Father (55), Mother (57), Fathers Brother (58) + FB Wife 1/2 (59–60),
              Mothers HalfSib (61) + husbands (66–67)
Focus:        Me (56) — linked to seed owner
Cousins:      Cousin 1–3 (62–64), Wife2 Only Child (65)
Single-own:   SingleOwnWife/Husband/Other/Child (71–74) — extra couple forest
```

Layout expectations for this tree: [`tree-layout-reference-tables.md`](tree-layout-reference-tables.md).
### Running the Seed Script

**Full steps:** [`testing-environment.md`](testing-environment.md#2-run-seed-script)

```bash
psql -h localhost -p 5432 -U family -d family -f tst/GMO.FamilyTree.Web.UiTests/Data/seed_trees.sql
```

#### What the Script Does

1. Creates trees **1–4** (primary, empty, single, large)
2. Inserts **25** members on tree **1** (IDs 50–74) plus single/large-tree members
3. Links **"Me" (56)** to the seed owner (`test@example.com` preferred)
4. Creates parent and couple rows in `FamilyMemberRelationships`
5. Advances Postgres sequences; **idempotent** (safe to re-run)

#### Verification

```sql
SELECT COUNT(*) AS family_members FROM "FamilyMembers" WHERE "FamilyTreeId" = 1;  -- expect 25
SELECT fm."Name", u."Email"
FROM "FamilyMembers" fm
JOIN "AspNetUsers" u ON fm."UserId" = u."Id"
WHERE fm."FamilyTreeId" = 1 AND fm."Id" = 56;
SELECT COUNT(*) AS relationships FROM "FamilyMemberRelationships" WHERE "FamilyTreeId" = 1;
```

---

## Test Account Setup

### User Account Creation

Since there's no way to recover passwords for pre-existing users, you need to create a new user account through the UI if you don't know existing account details.

#### Create User via Web UI

1. Open `http://localhost:5229` (anonymous users see the **landing** page)
2. **Sign up** / register with email + password (confirm email if required by your local config)
3. After sign-in you land on `/Home/Index`

Registration no longer collects “Male” / “This is me” checkboxes. Link to seed **"Me"** via SQL (below) or create members in the UI.

### Link User to Seeded Family Data

The seed script links the preferred owner to **"Me" (56)** on tree **1**. To attach a different account:

```sql
UPDATE "FamilyMembers"
SET "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'your-email@example.com')
WHERE "Id" = 56 AND "FamilyTreeId" = 1;
```

#### Test Authentication

The application provides a test authentication endpoint for automated testing:

```csharp
// POST /TestAuth/SignIn
// Automatically signs in the first available user
```

**Usage in Tests:**
```csharp
private async Task AuthenticateAsync()
{
    await _page.GotoAsync(_fixture.ServerAddress + "/TestAuth/SignIn");
}
```

**Note**: The test authentication endpoint signs in the first available user account, which may not be the user you just created. For consistent testing, create a dedicated test user and link it to the seeded data.

---

## Database Maintenance

### Backup Procedures

#### Automated Backups

```bash
# Create backup
pg_dump -h localhost -U familytree_user -d FamilyTree > backup_$(date +%Y%m%d).sql

# Restore from backup
psql -h localhost -U familytree_user -d FamilyTree < backup_20240224.sql
```

#### Application-Level Backups

```bash
# Using Entity Framework
dotnet ef database update 0  # Reset to initial state
dotnet ef database update    # Re-apply all migrations
```

### Data Validation

#### Consistency Checks

```sql
-- Orphaned relationship endpoints
SELECT r.*
FROM "FamilyMemberRelationships" r
LEFT JOIN "FamilyMembers" f ON f."Id" = r."FromMemberId"
LEFT JOIN "FamilyMembers" t ON t."Id" = r."ToMemberId"
WHERE f."Id" IS NULL OR t."Id" IS NULL;

-- Duplicate edges
SELECT "FromMemberId", "ToMemberId", "RelationshipType", COUNT(*) AS cnt
FROM "FamilyMemberRelationships"
GROUP BY "FromMemberId", "ToMemberId", "RelationshipType"
HAVING COUNT(*) > 1;
```

#### Data Integrity

```sql
-- Children per parent (RelationshipType Parent = 0)
SELECT
    fm."Name",
    COUNT(r."ToMemberId") AS child_count
FROM "FamilyMembers" fm
LEFT JOIN "FamilyMemberRelationships" r
  ON r."FromMemberId" = fm."Id" AND r."RelationshipType" = 0
WHERE fm."FamilyTreeId" = 1
GROUP BY fm."Id", fm."Name"
ORDER BY fm."Name";
```

---

## PostgreSQL MCP Integration

In Cursor, the postgres MCP tool is typically **read-only `query`**. Point it at the same DB as local `appsettings.json` (often `family` / `family` on `localhost:5432` via docker compose).

```sql
-- Example: primary seed members
SELECT "Id", "Name", "NickName", "DOB", "DOD", "IsMale"
FROM "FamilyMembers"
WHERE "FamilyTreeId" = 1
ORDER BY "Id";
```

Do not invent multi-tool MCP APIs in scripts; use `psql` for writes (INSERT/UPDATE/DELETE) per project conventions.

---

## Environment Setup

#### Local PostgreSQL

Prefer Docker:

```bash
docker compose up -d
```

Defaults: host `localhost:5432`, database/user `family`, password `family`. Migrations apply on app startup.

Manual install (optional): create a database/user that matches your `appsettings.json` connection string.

### Production Environment

#### Connection Pooling

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=FamilyTree;Username=familytree_user;Password=your_password;Pooling=true;Max Pool Size=100"
  }
}
```

#### Security Considerations

- Use environment variables for sensitive data
- Enable SSL connections in production
- Regular database backups
- Limited database user permissions
- Monitor connection pooling

---

## Troubleshooting

**Connection**: Check PostgreSQL / docker compose, test `psql`, verify connection string. **Migrations**: `dotnet ef migrations list --project src/GMO.FamilyTree.Web`. **Seeding**: Verify `FamilyMembers` / `FamilyMemberRelationships` for `FamilyTreeId = 1` (see [testing-environment.md](testing-environment.md)).

```sql
SELECT COUNT(*) FROM "FamilyMembers" WHERE "FamilyTreeId" = 1;
SELECT COUNT(*) FROM "FamilyMemberRelationships" WHERE "FamilyTreeId" = 1;
SELECT "Id", "Name" FROM "FamilyMembers"
WHERE "FamilyTreeId" = 1 AND "Name" IN ('Paternal Grandpa', 'Maternal Grandma', 'Me');
```

Useful indexes already exist from EF migrations (`FamilyTreeId`, relationship from/to). Prefer `EXPLAIN ANALYZE` on real queries rather than inventing `Generation` indexes (that column does not exist).

---

## Related Documentation

- [UI Testing Approach](ui-testing-approach.md) - How tests use the seeded data
- [Tree Layout Orientation](tree-layout-orientation.md) - Visual rank system that uses the data
- [Service Configuration](configure-service.md) - Application service setup

---

*This documentation should be updated when database schema changes or new seeding requirements are introduced.*
