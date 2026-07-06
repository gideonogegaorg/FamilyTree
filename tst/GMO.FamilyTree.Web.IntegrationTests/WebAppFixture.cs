using System.Threading.Tasks;

using GMO.FamilyTree.Web;
using GMO.FamilyTree.Web.Data;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace GMO.FamilyTree.Web.IntegrationTests;

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

    public HttpClient CreateClient(bool signIn = true)
    {
        EnsureDatabaseCreated();

        // We set HandleCookies = true so the client maintains its own CookieContainer.
        // However, we MUST use a distinct sign-in state.
        var client = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        if (signIn)
        {
            client.GetAsync("/TestAuth/SignIn").GetAwaiter().GetResult();
        }

        return client;
    }

    /// <summary>
    /// Creates a scope and returns the test app's <see cref="AppDbContext"/> for DB assertions (e.g. after Register/Login).
    /// </summary>
    public IServiceScope CreateScope() => Services.CreateScope();

    public AppDbContext GetDbContext(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
        builder.UseSetting("Telemetry:Otlp:Endpoint", "http://localhost:4317"); // avoid null Endpoint when binding OtlpExporterOptions
        if (!string.IsNullOrEmpty(_testDatabaseName))
        {
            builder.UseSetting("Photos:LocalBasePath", Path.Combine("uploads", "photos-test", _testDatabaseName));
            builder.UseSetting("Photos:StoragePrefix", $"familytree/test/{_testDatabaseName}");
        }

        // Suppress verbose EF Core and ASP.NET Core Information logs during tests
        builder.UseSetting("Serilog:MinimumLevel:Default", "Error");
        builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft", "Error");
        builder.UseSetting("Serilog:MinimumLevel:Override:System", "Error");
        builder.UseSetting("Serilog:MinimumLevel:Override:Microsoft.Hosting.Lifetime", "Error");

        builder.UseEnvironment("Testing");
        builder.ConfigureServices((_, services) =>
        {
            var testAssembly = typeof(Controllers.TestAuthController).Assembly;
            services.AddControllersWithViews().AddApplicationPart(testAssembly);
        });
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