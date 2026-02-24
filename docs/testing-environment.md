# Testing Environment Setup

This document covers the complete testing environment setup for the Family Tree application, including database seeding, test accounts, and MCP server configuration.

---

## Overview

The testing environment requires:
1. **Database setup** with seeded family data
2. **Test account configuration** for authentication
3. **MCP server setup** for database operations
4. **Test data validation** for reliable test execution

---

## Prerequisites

### Software Requirements

- **.NET 10.0** or later
- **PostgreSQL** 15 or later
- **Node.js** (for MCP server if applicable)
- **Playwright** browsers (Chrome, Firefox, Safari)

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
- **⚠️ IMPORTANT**: Seed data must be loaded for meaningful UI validation

**⚠️ CRITICAL**: The UI validation requires the complete 16-person family tree from the seed script. Without the seeded data, you'll only see "Me" (1 person) and cannot validate:
- Visual rank positioning (0, 0.5, 1, 1.5, 2)
- Half-rank spouse positioning
- Multi-generation layout
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
cat src/GMO.Family.Web/appsettings.json

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

**Current Limitations:**
- MCP server is not configured by default
- Direct psql requires database credentials (appsettings.json is git ignored)
- Need an alternative approach for seed data

**Recommended Approach:**
1. **Check if seed data already exists** in the database
2. **Use application endpoints** if available for data management
3. **Configure MCP server** for database operations (advanced)

**Check Current Database State:**
```bash
# Try to connect with common defaults (may fail without credentials)
psql -h localhost -p 5432 -U family -d family -c "SELECT COUNT(*) FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 9;" 2>/dev/null || echo "Database connection failed - check credentials"
```

**Alternative: Manual Data Entry**
If seed script cannot be executed, you can:
1. Create family members manually through the UI
2. Use the application's data management features
3. Configure database access for seed script execution

### 3. Verify Seed Data

```sql
-- Verify family members were created (should be 16)
SELECT COUNT(*) as family_members FROM "FamilyMembers" WHERE "FamilyTreeId" = 9;

-- Verify relationships were created (should be 15)
SELECT COUNT(*) as relationships FROM "FamilyMemberRelationships" WHERE "FamilyTreeId" = 9;

-- Verify family tree was created
SELECT * FROM "FamilyTrees" WHERE "Id" = 9;
```

### 4. Create User Account

Since there's no way to recover passwords for existing users, create a new account:

1. **Open application**: Navigate to `http://localhost:5229`
2. **Register account**: Click "Create a free account"
3. **Fill registration form**:
   - Email: `test@familytree.local`
   - Password: `Test123!`
   - Confirm password: `Test123!`
   - Check "This is me (link to my account)"
4. **Submit registration**

### 5. Link User to Family Data

The seed script automatically links the first user to "Me" (ID: 56). If you created a new account, link it manually:

```sql
-- Link your user to "Me" family member
UPDATE "FamilyMembers" 
SET "UserId" = (SELECT "Id" FROM "AspNetUsers" WHERE "UserName" = 'test@familytree.local')
WHERE "Id" = 56 AND "FamilyTreeId" = 9;
```

---

## Test Account Configuration

### 1. Create Test User Script

Create `scripts/create-test-user.sql`:

```sql
-- Test user for automated testing
INSERT INTO "AspNetUsers" (
    "Id", 
    "UserName", 
    "NormalizedUserName", 
    "Email", 
    "NormalizedEmail",
    "EmailConfirmed", 
    "PasswordHash", 
    "SecurityStamp", 
    "ConcurrencyStamp",
    "LockoutEnabled",
    "AccessFailedCount",
    "TwoFactorEnabled",
    "PhoneNumberConfirmed",
    "FamilyMemberId"
) VALUES (
    'test-user-12345',
    'test@familytree.local',
    'TEST@FAMILYTREE.LOCAL',
    'test@familytree.local',
    'TEST@FAMILYTREE.LOCAL',
    true,
    'AQAAAAIAAYagAAAAENa1r5EGgAAAAEAh...',  -- Hashed password for "Test123!"
    'test-security-stamp',
    'test-concurrency-stamp',
    false,
    0,
    false,
    false,
    13  -- Associated with "Me" family member
) ON CONFLICT ("Id") DO NOTHING;

-- Create user profile
INSERT INTO "UserProfiles" (
    "UserId", 
    "TreeViewOrientation", 
    "LineageMode"
) VALUES (
    'test-user-12345',
    0,  -- Horizontal
    0   -- Paternal
) ON CONFLICT ("UserId") DO NOTHING;
```

### 2. Execute Test User Creation

```bash
# Run the script
psql -h localhost -U test_user -d FamilyTree_Test -f scripts/create-test-user.sql

# Verify user creation
psql -h localhost -U test_user -d FamilyTree_Test -c "SELECT \"UserName\", \"FamilyMemberId\" FROM \"AspNetUsers\" WHERE \"Id\" = 'test-user-12345';"
```

### 3. Test Authentication Endpoint

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

**Note**: The test authentication endpoint signs in the first available user account. For consistent testing, create a dedicated test user and link it to the seeded data.

---

## MCP Server Setup for Testing

### 1. PostgreSQL MCP Server Configuration

The MCP server provides database operations for testing:

```javascript
// mcp-config.json
{
  "mcpServers": {
    "postgres": {
      "command": "node",
      "args": ["mcp-postgres-server.js"],
      "env": {
        "POSTGRES_HOST": "localhost",
        "POSTGRES_PORT": "5432",
        "POSTGRES_DB": "FamilyTree_Test",
        "POSTGRES_USER": "test_user",
        "POSTGRES_PASSWORD": "test_password"
      }
    }
  }
}
```

### 2. Available MCP Operations

| Operation | Use Case | Example |
|---|---|---|
| `mcp2_query` | Read-only SQL queries | Verify test data state |
| `mcp0_get_schema` | Schema inspection | Validate table structure |
| `mcp0_get_table_data` | Data retrieval | Get specific test data |
| `mcp0_describe_table` | Table details | Check column types |

### 3. MCP Usage in Testing

```javascript
// Verify test data exists
const familyMembers = await mcp2_query({
  sql: "SELECT COUNT(*) as count FROM \"FamilyMembers\""
});

// Get specific test data
const testData = await mcp0_get_table_data({
  tableName: "FamilyMembers",
  whereClause: "\"Name\" IN ('Paternal Grandpa', 'Maternal Grandma')",
  limit: 10
});
```

---

## Test Data Validation

### 1. Verify Seeded Data Structure

```sql
-- Check family member hierarchy
SELECT 
    fm."Id",
    fm."Name", 
    fm."Generation",
    fm."IsMale",
    COUNT(fr."ChildId") as ChildCount
FROM "FamilyMembers" fm
LEFT JOIN "FamilyRelationships" fr ON fm."Id" = fr."ParentId"
GROUP BY fm."Id", fm."Name", fm."Generation", fm."IsMale"
ORDER BY fm."Generation", fm."Name";
```

**Expected Results:**
```
Id | Name              | Generation | IsMale | ChildCount
---|-------------------|------------|--------|-----------
2  | Paternal Grandpa  | 1          | true   | 2
1  | Paternal Grandma  | 1          | false  | 0
3  | Maternal Grandma  | 1          | false  | 3
4  | Maternal Grandpa 1| 1          | true   | 0
5  | Maternal Grandpa 2| 1          | true   | 0
6  | Father            | 2          | true   | 1
7  | Mother            | 2          | false  | 1
8  | Fathers Brother   | 2          | true   | 2
11 | Mothers HalfSib   | 2          | false  | 1
13 | Me                | 3          | true   | 0
```

### 2. Verify Relationship Structure

```sql
-- Check parent-child relationships
SELECT 
    parent."Name" as Parent,
    child."Name" as Child,
    fr."RelationshipType"
FROM "FamilyRelationships" fr
JOIN "FamilyMembers" parent ON fr."ParentId" = parent."Id"
JOIN "FamilyMembers" child ON fr."ChildId" = child."Id"
ORDER BY parent."Generation", parent."Name", child."Name";
```

### 3. Verify Visual Rank Assignments

```sql
-- Check visual rank calculations (Paternal mode)
WITH PaternalRanks AS (
    SELECT 
        fm."Id",
        fm."Name",
        fm."Generation",
        CASE 
            WHEN fm."Id" = 2 THEN 0.0  -- Paternal Grandpa (primary male)
            WHEN fm."Id" = 1 THEN 0.5  -- Paternal Grandma (secondary partner)
            WHEN fm."Id" IN (6, 8) THEN 1.0  -- Father, Fathers Brother (primary males)
            WHEN fm."Id" IN (7, 9, 10, 11, 12) THEN 1.5  -- Secondary partners
            WHEN fm."Id" IN (13, 14, 15, 16) THEN 2.0  -- Children generation
        END as PaternalRank
    FROM "FamilyMembers" fm
)
SELECT * FROM PaternalRanks ORDER BY PaternalRank, "Name";
```

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
dotnet run --project src/GMO.Family.Web

# Test application health
curl http://localhost:5000/health

# Test test authentication
curl -X POST http://localhost:5000/TestAuth/SignIn
```

### 3. UI Test Validation

```bash
# Run UI tests to validate environment
dotnet test tst/GMO.Family.Web.UiTests --logger "console;verbosity=detailed"

# Run specific test to verify test data
dotnet test tst/GMO.Family.Web.UiTests --filter "FullyQualifiedName~HorizontalLayout_Paternal_PositionsEveryNodeAndRank"
```

### 4. Data Validation

```sql
-- Check if seed data exists
SELECT COUNT(*) FROM "FamilyMembers" WHERE "FamilyTreeId" = 9;

-- Check if user is linked to family data
SELECT fm."Name", u."UserName" 
FROM "FamilyMembers" fm
JOIN "AspNetUsers" u ON fm."UserId" = u."Id"
WHERE fm."FamilyTreeId" = 9 AND fm."Id" = 56;

-- Verify relationships
SELECT COUNT(*) FROM "FamilyRelationships" WHERE "FamilyTreeId" = 9;
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
dotnet test tst/GMO.Family.Web.UiTests --logger "console;verbosity=detailed"

# Check test data state
psql -h localhost -U family -d family -c "SELECT \"Name\", \"Generation\" FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 9 ORDER BY \"Generation\", \"Name\";"
```

---

## Automation Scripts

### 1. Complete Setup Script

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
psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -f tst/GMO.Family.Web.UiTests/Data/seed_3gen.sql

# Verify setup
echo "Verifying setup..."
psql -h $DB_HOST -p $DB_PORT -U $DB_USER -d $DB_NAME -c "SELECT COUNT(*) as family_members FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 9;"

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
    echo "✅ Database connection: OK"
else
    echo "❌ Database connection: FAILED"
    exit 1
fi

# Check test data
member_count=$(psql -h localhost -p 5432 -U family -d family -t -c "SELECT COUNT(*) FROM \"FamilyMembers\" WHERE \"FamilyTreeId\" = 9;")
if [ "$member_count" -eq "16" ]; then
    echo "✅ Family members data: OK ($member_count members)"
else
    echo "❌ Family members data: FAILED (expected 16, got $member_count)"
    exit 1
fi

# Check relationships
relationship_count=$(psql -h localhost -p 5432 -U family -d family -t -c "SELECT COUNT(*) FROM \"FamilyRelationships\" WHERE \"FamilyTreeId\" = 9;")
if [ "$relationship_count" -eq "15" ]; then
    echo "✅ Family relationships: OK ($relationship_count relationships)"
else
    echo "❌ Family relationships: FAILED (expected 15, got $relationship_count)"
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
cd src/GMO.Family.Web
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
