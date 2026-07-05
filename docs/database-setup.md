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
| `FamilyMembers` | Family member data | `Id`, `Name`, `IsMale`, `Generation`, `PhotoKey` |
| `FamilyRelationships` | Member relationships | `ParentId`, `ChildId`, `RelationshipType` |
| `UserProfiles` | User preferences | `UserId`, `TreeViewOrientation`, `LineageMode`, `PhotoKey`, `TreeCardViewMode` |

### Entity Framework Migrations

The application uses Entity Framework Core for database schema management:

```bash
# Create new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Remove last migration
dotnet ef migrations remove
```

---

## Data Seeding

### Seeding Strategy

The application uses a **deterministic seeding approach** with SQL scripts located in the test project. The main seed script creates a complete 3-generation family tree with 16 family members.

### Seed Data Location

The seed script is located at:
```
tst/GMO.FamilyTree.Web.UiTests/Data/seed_trees.sql
```

### Seed Data Structure

#### Family Member Hierarchy (IDs 50-65)

```sql
-- Grandparents (Generation 1, IDs 50-54)
- Paternal Grandma (Id: 50, female)
- Paternal Grandpa (Id: 51, male) 
- Maternal Grandma (Id: 52, female)
- Maternal Grandpa 1 (Id: 53, male)
- Maternal Grandpa 2 (Id: 54, male)

-- Parents (Generation 2, IDs 55-61)
- Father (Id: 55, male)
- Me (Id: 56, male) - Linked to first user account
- Mother (Id: 57, female)
- Fathers Brother (Id: 58, male)
- FB Wife 1 (Id: 59, female) - Partner of Fathers Brother
- FB Wife 2 (Id: 60, female) - Partner of Fathers Brother
- Mothers HalfSib (Id: 61, male)

-- Children (Generation 3, IDs 62-65)
- Cousin 1 (Id: 62, male) - Child of Fathers Brother + FB Wife 1
- Cousin 2 (Id: 63, male) - Child of Fathers Brother + FB Wife 2
- Cousin 3 (Id: 64, male) - Child of Mothers HalfSib
- Wife2 Only Child (Id: 65, male) - Partner of Mothers HalfSib
```

### Running the Seed Script

**For complete setup instructions, see [`testing-environment.md`](testing-environment.md#run-seed-script)**

**Quick Reference:**
```bash
# Navigate to project root
cd c:\_Git\gideonogega\Family

# Run the seed script (update connection details as needed)
psql -h localhost -p 5432 -U family -d family -f tst/GMO.FamilyTree.Web.UiTests/Data/seed_trees.sql
```

**Note**: For detailed troubleshooting and alternative approaches, see the testing environment documentation.

#### What the Script Does

1. **Creates Family Tree**: Creates tree ID 9 with name '3-Gen Test Tree'
2. **Inserts Family Members**: Adds all 16 family members (IDs 50-65)
3. **Links User Account**: Automatically links "Me" (ID 56) to the first user account
4. **Creates Relationships**: Sets up all parent-child and couple relationships
5. **Updates Sequence**: Advances the ID sequence for future additions
6. **Idempotent**: Safe to re-run multiple times

#### Verification

```sql
-- Verify family members were created
SELECT COUNT(*) as family_members FROM "FamilyMembers" WHERE "FamilyTreeId" = 9;

-- Verify user was linked to "Me"
SELECT fm."Name", u."UserName" 
FROM "FamilyMembers" fm
JOIN "AspNetUsers" u ON fm."UserId" = u."Id"
WHERE fm."FamilyTreeId" = 9 AND fm."Id" = 56;

-- Verify relationships
SELECT COUNT(*) as relationships FROM "FamilyMemberRelationships" WHERE "FamilyTreeId" = 9;
```

---

## Test Account Setup

### User Account Creation

Since there's no way to recover passwords for pre-existing users, you need to create a new user account through the UI if you don't know existing account details.

#### Create User via Web UI

1. **Navigate to the application**: Open `http://localhost:5229`
2. **Register new account**: Click "Create a free account"
3. **Fill registration form**:
   - Email: `test@familytree.local` (or any email you prefer)
   - Password: `Test123!` (or any password you prefer)
   - Confirm password: `Test123!`
   - Check "Male" (optional)
   - Check "This is me (link to my account)" (important for linking)
4. **Submit registration**: Click "Register" button

#### Verify User Account

After registration, you should be logged in and can see your user profile in the top navigation.

### Link User to Seeded Family Data

The seed script automatically links the first user account to "Me" (ID: 56). If you create a new user account, you'll need to manually link it:

```sql
-- Link your new user to "Me" family member
UPDATE "FamilyMembers" 
SET "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "UserName" = 'your-email@example.com')
WHERE "Id" = 56 AND "FamilyTreeId" = 9;
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
-- Check for orphaned relationships
SELECT * FROM "FamilyRelationships" fr
LEFT JOIN "FamilyMembers" fm ON fr."ParentId" = fm."Id"
WHERE fm."Id" IS NULL;

-- Check for duplicate relationships
SELECT "ParentId", "ChildId", COUNT(*) as Count
FROM "FamilyRelationships"
GROUP BY "ParentId", "ChildId"
HAVING COUNT(*) > 1;
```

#### Data Integrity

```sql
-- Verify family member hierarchy
SELECT 
    fm."Name",
    fm."Generation",
    COUNT(fr."ChildId") as ChildCount
FROM "FamilyMembers" fm
LEFT JOIN "FamilyRelationships" fr ON fm."Id" = fr."ParentId"
GROUP BY fm."Id", fm."Name", fm."Generation"
ORDER BY fm."Generation", fm."Name";
```

---

## PostgreSQL MCP Integration

### MCP Server Configuration

The application uses a PostgreSQL MCP server for database operations:

#### Connection Setup

```javascript
// MCP Server Configuration
{
  "name": "postgres",
  "version": "1.0.0",
  "connection": {
    "host": "localhost",
    "port": 5432,
    "database": "FamilyTree",
    "user": "familytree_user",
    "password": "your_password"
  }
}
```

#### Available Operations

The MCP server provides these database operations:

| Operation | Purpose | Example |
|---|---|---|
| `query` | Execute read-only SQL queries | `SELECT * FROM FamilyMembers` |
| `list_databases` | List available databases | - |
| `get_schema` | Get database schema information | Tables, columns, relationships |
| `describe_table` | Get detailed table structure | `FamilyMembers` table details |
| `get_table_data` | Retrieve table data with filtering | Get specific family members |

#### Usage Examples

```javascript
// Query family members
await mcp2_query({
  sql: "SELECT * FROM FamilyMembers WHERE Generation = 2"
});

// Get schema information
await mcp0_get_schema({
  objectType: "tables"
});

// Get specific table data
await mcp0_get_table_data({
  tableName: "FamilyMembers",
  whereClause: "IsMale = true",
  limit: 10
});
```

---

## Environment Setup

#### Local PostgreSQL

```bash
# Install PostgreSQL (Ubuntu/Debian)
sudo apt-get install postgresql postgresql-contrib

# Create database and user
sudo -u postgres psql
CREATE DATABASE FamilyTree;
CREATE USER familytree_user WITH PASSWORD 'your_password';
GRANT ALL PRIVILEGES ON DATABASE FamilyTree TO familytree_user;
\q
```

#### Docker Setup

Use the repo-root [docker-compose.yml](../docker-compose.yml) (PostgreSQL + MinIO for local S3):

```bash
docker compose up -d
```

| Service | Connection |
|---------|------------|
| PostgreSQL | `localhost:5432`, user/db `family`, password `family` |
| MinIO (S3) | `http://localhost:9000`, bucket `gideonogega-internal` |

Migrations run automatically on app startup (`dotnet run`).

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

**Connection**: Check PostgreSQL service, test psql, verify connection string. **Migrations**: `dotnet ef migrations list`; reset with `database drop` then `update` (destructive). **Seeding**: Verify `FamilyMembers` / `FamilyRelationships` counts (see [testing-environment.md](testing-environment.md)).

#### Connection

```bash
# Check PostgreSQL service status
sudo systemctl status postgresql

# Test connection
psql -h localhost -U familytree_user -d FamilyTree

# Check connection string
echo $DB_CONNECTION_STRING
```

#### Migrations

```bash
# Check pending migrations
dotnet ef migrations list

# Reset database (caution: deletes all data)
dotnet ef database drop
dotnet ef database update
```

#### Seeding

```sql
-- Check if seed data exists
SELECT COUNT(*) FROM "FamilyMembers";
SELECT COUNT(*) FROM "FamilyRelationships";

-- Verify specific family members
SELECT * FROM "FamilyMembers" WHERE "Name" IN ('Paternal Grandpa', 'Maternal Grandma');
```

### Performance Issues

#### Query Optimization

```sql
-- Add indexes for common queries
CREATE INDEX IF NOT EXISTS idx_family_members_generation 
ON "FamilyMembers"("Generation");

CREATE INDEX IF NOT EXISTS idx_relationships_parent 
ON "FamilyRelationships"("ParentId");

-- Analyze query performance
EXPLAIN ANALYZE SELECT * FROM "FamilyMembers" WHERE "Generation" = 2;
```

---

## Related Documentation

- [UI Testing Approach](ui-testing-approach.md) - How tests use the seeded data
- [Tree Layout Orientation](tree-layout-orientation.md) - Visual rank system that uses the data
- [Service Configuration](configure-service.md) - Application service setup

---

*This documentation should be updated when database schema changes or new seeding requirements are introduced.*
