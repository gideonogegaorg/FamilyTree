using System.Threading.Tasks;

using GMO.Family.Web;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Npgsql;

namespace GMO.Family.Web.IntegrationTests;

/// <summary>
/// Creates a unique PostgreSQL database per test run. Injects the test connection string
/// so the app runs migrations on startup against the test DB. Drops the DB when disposed.
/// </summary>
public sealed class WebAppFixture : WebApplicationFactory<WebAppEntry>, IDisposable
{
    private const string BaseConnectionString = "Host=localhost;Port=5432;Username=family;Password=family";
    private string _testDatabaseName = null!;
    private string _testConnectionString = null!;
    private bool _dbDropped;
    private bool _initialized;
    private readonly object _initLock = new();

    public new HttpClient CreateClient()
    {
        EnsureDatabaseCreated();
        var client = base.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        // Sign in as test user so FamilyTree actions (which require OwnerId) succeed
        client.GetAsync("/TestAuth/SignIn").GetAwaiter().GetResult();
        return client;
    }

    private void EnsureDatabaseCreated()
    {
        if (_initialized)
            return;
        lock (_initLock)
        {
            if (_initialized)
                return;
            CreateDatabaseAsync().GetAwaiter().GetResult();
            _initialized = true;
        }
    }

    private async Task CreateDatabaseAsync()
    {
        _testDatabaseName = "family_test_" + Guid.NewGuid().ToString("N")[..12];
        _testConnectionString = $"{BaseConnectionString};Database={_testDatabaseName}";

        await using (var conn = new NpgsqlConnection(BaseConnectionString + ";Database=postgres"))
        {
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_testDatabaseName}\"", conn))
                await cmd.ExecuteNonQueryAsync();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _testConnectionString);
        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_dbDropped)
            DropTestDatabaseAsync().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }

    private async Task DropTestDatabaseAsync()
    {
        if (_dbDropped)
            return;
        _dbDropped = true;
        try
        {
            await using var conn = new NpgsqlConnection(BaseConnectionString + ";Database=postgres");
            await conn.OpenAsync();
            await TerminateConnectionsAsync(conn, _testDatabaseName);
            await using (var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_testDatabaseName}\"", conn))
                await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    private static async Task TerminateConnectionsAsync(NpgsqlConnection adminConn, string databaseName)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT pg_terminate_backend(pg_stat_activity.pid)
            FROM pg_stat_activity
            WHERE pg_stat_activity.datname = @db AND pid <> pg_backend_pid()
            """, adminConn);
        cmd.Parameters.AddWithValue("db", databaseName);
        await cmd.ExecuteNonQueryAsync();
    }
}
