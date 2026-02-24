using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Npgsql;

namespace GMO.Family.Web.UiTests;

public sealed class AppFixture : WebApplicationFactory<WebAppEntry>, IDisposable
{
    private const string BaseConnectionString = "Host=localhost;Port=5432;Username=family;Password=family";
    private string _testDatabaseName = null!;
    private string _testConnectionString = null!;
    private bool _dbDropped;
    private bool _initialized;
    private readonly object _initLock = new();

    private IHost? _host;
    public string ServerAddress { get; private set; }

    public AppFixture()
    {
        EnsureDatabaseCreated();

        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        ServerAddress = $"http://127.0.0.1:{port}";

        // Force WebApplicationFactory to initialize the host immediately
        _ = this.Services;
    }

    private void EnsureDatabaseCreated()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            CreateDatabaseAsync().GetAwaiter().GetResult();
            _initialized = true;
        }
    }

    private async Task CreateDatabaseAsync()
    {
        _testDatabaseName = "family_uitest_" + Guid.NewGuid().ToString("N")[..12];
        _testConnectionString = $"{BaseConnectionString};Database={_testDatabaseName}";

        await using (var conn = new NpgsqlConnection(BaseConnectionString + ";Database=postgres"))
        {
            await conn.OpenAsync();
            await using (var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_testDatabaseName}\"", conn))
                await cmd.ExecuteNonQueryAsync();
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // 1. Let the base create the TestServer for migrations to run via EF Core automatically
        var dummyHost = base.CreateHost(builder);

        // 2. Now spin up the Kestrel server on a random port for Playwright
        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseStaticWebAssets();
            webHostBuilder.UseKestrel();
            webHostBuilder.UseUrls(ServerAddress);
        });

        _host = builder.Build();
        _host.Start();

        // 3. Create test user and seed data
        using (var scope = dummyHost.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Microsoft.AspNetCore.Identity.IdentityUser>>();
            var user = new Microsoft.AspNetCore.Identity.IdentityUser
            {
                UserName = "test@example.com",
                Email = "test@example.com",
                EmailConfirmed = true
            };
            userManager.CreateAsync(user, "TestPassword1!").GetAwaiter().GetResult();
        }

        SeedDatabaseAsync().GetAwaiter().GetResult();

        return dummyHost; // return the original dummyHost to satisfy WebApplicationFactory requirements
    }

    private async Task SeedDatabaseAsync()
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Data", "seed_3gen.sql");

        var sql = await File.ReadAllTextAsync(seedPath);

        await using var conn = new NpgsqlConnection(_testConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _testConnectionString);
        builder.UseSetting("Telemetry:Otlp:Endpoint", "http://localhost:4317");
        
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
        _host?.Dispose();
        if (disposing && !_dbDropped)
            DropTestDatabaseAsync().GetAwaiter().GetResult();
        base.Dispose(disposing);
    }

    private async Task DropTestDatabaseAsync()
    {
        if (_dbDropped) return;
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