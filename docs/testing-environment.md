# Testing Environment Setup

Database seeding, test accounts, and MCP configuration for the Family Tree application.

---

## Overview

- **Database** with seeded family data
- **Test account** for authentication
- **MCP server** (optional) for database operations

---

## Prerequisites

### Software Requirements

- **.NET 10.0** or later
- **Docker** (recommended for local Postgres + S3-compatible MinIO)
- **PostgreSQL** 15 or later (if not using Docker)
- **Node.js** (for MCP server if applicable)
- **Playwright** browsers (Chrome, Firefox, Safari)

### Local Docker stack (Postgres + S3)

From the repo root:

```bash
docker compose up -d
```

| Service | URL | Credentials |
|---------|-----|-------------|
| PostgreSQL | `localhost:5432` | `family` / `family`, db `family` |
| MinIO (S3 API) | `http://localhost:9000` | bucket `gideonogega-internal` |
| MinIO console | `http://localhost:9001` | `minioadmin` / `minioadmin` |

`dotnet run` launch profiles default to **Local** photo storage (no MinIO required). Integration tests use in-process storage and do not require MinIO. To test S3 locally, run `docker compose up -d` and set `Photos__Provider=S3`.

### Tools Required

```bash
# .NET CLI tools
dotnet tool install --global dotnet-ef
dotnet tool install --global dotnet-aspnet-codegenerator

# PostgreSQL client tools
sudo apt-get install postgresql-client  # Ubuntu/Debian
# or
brew install postgresql                   # macOS
```

---

## Database Setup for Testing

### Prerequisites

- **PostgreSQL database**: Must be already configured and running
- **Database connection**: Check your local `appsettings.json` for connection details (file is git ignored)
- **IMPORTANT**: Seed data must be loaded for meaningful UI validation.

**CRITICAL**: UI layout validation needs the primary **26-member** tree from `seed_trees.sql` (tree ID **1**, members 50–74, 76). Without it you only see a sparse tree and cannot validate:
- Visual rank positioning (0, 0.5, 1, 1.5, 2, …)
- Half-rank spouse positioning
- Multi-generation / forest layout
- Lineage mode differences (Paternal vs Maternal)

### Finding Your Database Connection

Since `appsettings.json` is git ignored, you have several options:

**Option 1: Use MCP Server (Recommended)**
- MCP server uses the application's connection rights
- No need for manual database credentials
- Works for read-only operations and data validation

**Option 2: Check Local Configuration**
```bash
# Check your local appsettings.json
cat src/GMO.FamilyTree.Web/appsettings.json

# Or check if using environment variables
echo $DB_HOST $DB_NAME $DB_USER $DB_PASSWORD
```

**Option 3: Direct psql (requires credentials)**
```bash
# Only if you know the database credentials
psql -h localhost -p 5432 -U family -d family -c "SELECT version();"
```

**Common default values** (but verify in your local config):
- Host: `localhost`
- Database: `family`
- User: `family`
- Port: `5432`

### 1. Create Test Database (Optional)

If you want a separate test database:

```bash
# Connect to PostgreSQL as superuser
sudo -u postgres psql

# Create test database (optional)
CREATE DATABASE Family_Test;
CREATE USER test_user WITH PASSWORD 'test_password';
GRANT ALL PRIVILEGES ON DATABASE Family_Test TO test_user;
\q
```

### 2. Run Seed Script

Direct psql requires credentials (see [database-setup.md](database-setup.md)); appsettings.json is git ignored. Check if seed data exists, or use the application UI / MCP for data. To check state:

```bash
psql -h localhost -p 5432 -U family -d family -c "SELECT COUNT(*) FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 1;" 2>/dev/null || echo "Database connection failed - check credentials"
```

If you cannot run the seed script, create members via the UI or configure database access.

### 3. Verify Seed Data

```sql
-- Primary tree members (expect 26)
SELECT COUNT(*) AS family_members FROM "FamilyMembers" WHERE "FamilyTreeId" = 1;

SELECT COUNT(*) AS relationships FROM "FamilyMemberRelationships" WHERE "FamilyTreeId" = 1;

SELECT "Id", "Name" FROM "FamilyTrees" WHERE "Id" IN (1, 2, 3, 4, 5);
```

### 4. Create User Account

1. Open `http://localhost:5229` (landing for anonymous users)
2. Sign up with email + password
3. Prefer email `test@example.com` so re-running `seed_trees.sql` picks that owner automatically

### 5. Link User to Family Data

The seed script links the owner to **"Me" (56)** on tree **1**. To attach another account:

```sql
UPDATE "FamilyMembers"
SET "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "Email" = 'your-email@example.com')
WHERE "Id" = 56 AND "FamilyTreeId" = 1;
```

---

## Test Account Configuration

Prefer registering through the UI (Identity hashes passwords correctly). Do **not** invent columns like `AspNetUsers.FamilyMemberId` — the link is `FamilyMembers.UserId`.

UI tests use `TestAuth/SignIn` (Testing environment only) against the in-process host; they do not require the SQL user script below.

### Test Authentication Endpoint

```csharp
// Testing host only — signs in the fixture user
await page.GotoAsync(serverAddress + "/TestAuth/SignIn");
```

---

## MCP / Database Inspection

Use Cursor’s postgres MCP **`query`** tool (read-only) against the same DB as local `appsettings.json`, or `psql` for writes. Example:

```sql
SELECT COUNT(*) FROM "FamilyMembers" WHERE "FamilyTreeId" = 1;
```

---

## Test Data Validation

```sql
-- Parent edges (RelationshipType Parent = 0)
SELECT parent."Name" AS parent, child."Name" AS child
FROM "FamilyMemberRelationships" r
JOIN "FamilyMembers" parent ON parent."Id" = r."FromMemberId"
JOIN "FamilyMembers" child ON child."Id" = r."ToMemberId"
WHERE r."FamilyTreeId" = 1 AND r."RelationshipType" = 0
ORDER BY parent."Name", child."Name";

-- Couple edges (RelationshipType Couple = 2)
SELECT a."Name" AS member_a, b."Name" AS member_b
FROM "FamilyMemberRelationships" r
JOIN "FamilyMembers" a ON a."Id" = r."FromMemberId"
JOIN "FamilyMembers" b ON b."Id" = r."ToMemberId"
WHERE r."FamilyTreeId" = 1 AND r."RelationshipType" = 2
ORDER BY a."Name", b."Name";
```

Visual ranks are computed in app code (C# / JS), not stored as a `Generation` column. Validate ranks in the UI via `data-visual-rank` (see [`ui-testing-approach.md`](ui-testing-approach.md) and [`tree-layout-reference-tables.md`](tree-layout-reference-tables.md)).

---

## Test Environment Validation

### 1. Database Connectivity Test

```bash
# Test database connection (update with your connection details)
psql -h localhost -p 5432 -U family -d family -c "SELECT version();"

# Test basic query
psql -h localhost -p 5432 -U family -d family -c "SELECT COUNT(*) FROM \"FamilyMembers\";"
```

**Note**: Update connection parameters to match your local configuration.

### 2. Application Startup Test

```bash
# Start application
dotnet run --project src/GMO.FamilyTree.Web

# Test application health (dev URL from launchSettings.json)
curl -s http://localhost:5229/health
```

### 3. UI Test Validation

**Test hosting (not the dev port):** Integration tests use in-process **TestServer** (`WebAppFixture`) — no TCP port, no conflict with `dotnet run` on 5229 except when rebuilding locks `GMO.FamilyTree.Web.exe`. UI tests (`AppFixture`) bind Kestrel to a **random ephemeral port** for Playwright; they also do not use 5229.

```bash
# Run UI tests to validate environment
dotnet test tst/GMO.FamilyTree.Web.UiTests --logger "console;verbosity=detailed"

# Run specific test to verify test data
dotnet test tst/GMO.FamilyTree.Web.UiTests --filter "FullyQualifiedName~HorizontalLayout_Paternal_PositionsEveryNodeAndRank"
```

### 4. Data Validation

```sql
-- Check if seed data exists
SELECT COUNT(*) FROM "FamilyMembers" WHERE "FamilyTreeId" = 1;

-- Check if user is linked to family data
SELECT fm."Name", u."UserName" 
FROM "FamilyMembers" fm
JOIN "AspNetUsers" u ON fm."UserId" = u."Id"
WHERE fm."FamilyTreeId" = 1 AND fm."Id" = 56;

-- Verify relationships
SELECT COUNT(*) FROM "FamilyMemberRelationships" WHERE "FamilyTreeId" = 1;
```

---

## Troubleshooting Test Environment
```bash
# Check PostgreSQL service
sudo systemctl status postgresql

# Check database exists
sudo -u postgres psql -c "\l" | grep family

# Test user permissions
psql -h localhost -U family -d family -c "\dt"
```

#### Test Authentication Issues

```bash
# Check if user exists
psql -h localhost -U family -d family -c "SELECT \"Id\", \"UserName\" FROM \"AspNetUsers\" WHERE \"UserName\" = 'test@familytree.local';"

# Check user profile
psql -h localhost -U family -d family -c "SELECT * FROM \"UserProfiles\" WHERE \"UserId\" = (SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"UserName\" = 'test@familytree.local');"
```

#### UI Test Failures

```bash
# Check browser installation
dotnet tool install --global playwright
playwright install

# Run tests with detailed output
dotnet test tst/GMO.FamilyTree.Web.UiTests --logger "console;verbosity=detailed"

# Check test data state
psql -h localhost -U family -d family -c "SELECT \"Id\", \"Name\" FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 1 ORDER BY \"Id\";"
```

---

## Automation Scripts

Example scripts (can be placed in `scripts/`). Update connection details to match your appsettings.

### 1. Setup Script

Create `scripts/setup-test-environment.sh`:

```bash
#!/bin/bash

echo "Setting up Family Tree test environment..."

# IMPORTANT: Update these connection details to match your appsettings.json
DB_HOST="localhost"
DB_PORT="5432"
DB_USER="family"
DB_NAME="family"

# Run seed script
echo "Running seed script..."
psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -f tst/GMO.FamilyTree.Web.UiTests/Data/seed_trees.sql

# Verify setup
echo "Verifying setup..."
psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c "SELECT COUNT(*) as family_members FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 1;"

echo "Test environment setup complete!"
echo "NOTE: Update connection details in this script to match your appsettings.json"
```

### 2. Validation Script

Create `scripts/validate-test-environment.sh`:

```bash
#!/bin/bash

echo "Validating Family Tree test environment..."

# Check database connectivity
if psql -h localhost -p 5432 -U family -d family -c "SELECT 1;" > /dev/null 2>&1; then
    echo "Database connection: OK"
else
    echo "Database connection: FAILED"
    exit 1
fi

# Check test data
member_count=$(psql -h localhost -p 5432 -U family -d family -t -c "SELECT COUNT(*) FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 1;")
if [ "$member_count" -eq "16" ]; then
    echo "Family members data: OK ($member_count members)"
else
    echo "Family members data: FAILED (expected 16, got $member_count)"
    exit 1
fi

# Check relationships
relationship_count=$(psql -h localhost -p 5432 -U family -d family -t -c "SELECT COUNT(*) FROM \"FamilyMemberRelationships\" WHERE \"FamilyTreeId\" = 1;")
if [ "$relationship_count" -eq "15" ]; then
    echo "Family relationships: OK ($relationship_count relationships)"
else
    echo "Family relationships: FAILED (expected 15, got $relationship_count)"
    exit 1
fi

echo "Test environment validation: PASSED"
```

### 3. Quick Start Script

Create `scripts/quick-start.sh`:

```bash
#!/bin/bash

echo "Quick start for Family Tree testing..."

# 1. Start web server
echo "Starting web server..."
cd src/GMO.FamilyTree.Web
dotnet run &
SERVER_PID=$!
echo "Server PID: $SERVER_PID"

# Wait for server to start
sleep 5

# 2. Open browser for manual testing
echo "Opening browser..."
if command -v xdg-open > /dev/null 2>&1; then
    xdg-open http://localhost:5229
elif command -v open > /dev/null 2>&1; then
    open http://localhost:5229
else
    echo "Please open http://localhost:5229 in your browser"
fi

echo "Quick start complete!"
echo "Web server running with PID: $SERVER_PID"
echo "Press Ctrl+C to stop the server"
```

---

## Related Documentation

- [Database Setup](database-setup.md) - Complete database configuration and seeding
- [UI Testing Approach](ui-testing-approach.md) - How tests use the test environment
- [Tree Layout Orientation](tree-layout-orientation.md) - Visual rank system that uses the data

---

*This testing environment setup should be validated before running UI tests to ensure reliable test execution.*
