using GMO.FamilyTree.Web.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Xunit;

namespace GMO.FamilyTree.Web.IntegrationTests;

/// <summary>
/// Integration tests for EF Core migration Down scripts. Each test uses an isolated
/// PostgreSQL database: apply all migrations, then migrate down to zero, and assert
/// nothing fails and the database is left empty (no application tables).
/// </summary>
public sealed class MigrationDownTests
{
    private const string BaseConnectionString = "Host=localhost;Port=5432;Username=familytree;Password=familytree";

    [Fact]
    public async Task MigrateDown_to_zero_succeeds_and_leaves_db_empty()
    {
        // Arrange: create an isolated database for this test
        var dbName = "family_migdown_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = $"{BaseConnectionString};Database={dbName}";

        await using (var adminConn = new NpgsqlConnection(BaseConnectionString + ";Database=postgres"))
        {
            await adminConn.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{dbName}\"", adminConn))
                await cmd.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            await using (var context = new AppDbContext(options))
            {
                var migrator = context.Database.GetInfrastructure().GetRequiredService<IMigrator>();

                // Act: apply all migrations (Up), then migrate down to zero (all Down)
                migrator.Migrate();
                migrator.Migrate("0");
            }

            // Assert: no application tables remain (DB is empty of our schema)
            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("""
                    SELECT table_name
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                    ORDER BY table_name
                    """, conn);
                await using var reader = await cmd.ExecuteReaderAsync();
                var tables = new List<string>();
                while (await reader.ReadAsync())
                    tables.Add(reader.GetString(0));

                // After Migrate("0") we expect at most __EFMigrationsHistory (EF may leave it)
                var appTables = tables.Where(t =>
                    t != "__EFMigrationsHistory").ToList();
                Assert.Empty(appTables);
            }
        }
        finally
        {
            await TerminateAndDropDatabaseAsync(dbName);
        }
    }

    private static async Task TerminateAndDropDatabaseAsync(string databaseName)
    {
        try
        {
            await using var conn = new NpgsqlConnection(BaseConnectionString + ";Database=postgres");
            await conn.OpenAsync();
            await using (var term = new NpgsqlCommand("""
                SELECT pg_terminate_backend(pg_stat_activity.pid)
                FROM pg_stat_activity
                WHERE pg_stat_activity.datname = @db AND pid <> pg_backend_pid()
                """, conn))
            {
                term.Parameters.AddWithValue("db", databaseName);
                await term.ExecuteNonQueryAsync();
            }
            await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\"", conn))
                await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}