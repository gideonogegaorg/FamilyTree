using System.Diagnostics;
using System.Net;

using Amazon;
using Amazon.S3;
using Amazon.SimpleEmail;

using GMO.FamilyTree.Web;
using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Extensions;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;
using GMO.OpenTelemetry;
using GMO.OpenTelemetry.Serilog;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL;

using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

ConfigureCoreServices(builder);
ConfigurePhotoStorage(builder);
var telemetryOptions = ConfigureTelemetry(builder);
ConfigureDatabase(builder);
ConfigureEmail(builder);

var app = builder.Build();

await ApplyMigrationsAsync(app);
ConfigureTelemetryStartupLogging(app, telemetryOptions);
ConfigureMiddleware(app);

await app.RunAsync();

static void ConfigureCoreServices(WebApplicationBuilder builder)
{
    builder.Services.Configure<PathsOptions>(builder.Configuration.GetSection("Paths"));
    builder.Services.Configure<PhotosOptions>(builder.Configuration.GetSection("Photos"));
    builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
    builder.Services.Configure<EmailRateLimitOptions>(builder.Configuration.GetSection(EmailRateLimitOptions.SectionName));
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<IEmailRateLimiter, EmailRateLimiter>();
    builder.Services.AddControllersWithViews();
    builder.Services.AddDataProtection();
    builder.Services.AddSingleton<IEmailLogProtector, EmailLogProtector>();
    builder.Services.AddScoped<ICurrentFamilyTreeService, CurrentFamilyTreeService>();
    builder.Services.AddScoped<IFamilyTreeDeletionService, FamilyTreeDeletionService>();
    builder.Services.AddScoped<ITreeViewOrientationService, TreeViewOrientationService>();
    builder.Services.AddScoped<ILineageModeService, LineageModeService>();
    builder.Services.AddScoped<ITreeCardViewModeService, TreeCardViewModeService>();
    builder.Services.AddScoped<IFamilyTreeAccessService, FamilyTreeAccessService>();
    builder.Services.AddScoped<IFamilyTreeShareService, FamilyTreeShareService>();
    builder.Services.AddScoped<IDefaultFamilyTreeService, DefaultFamilyTreeService>();
    builder.Services.AddScoped<IExternalLoginInfoProvider, SignInManagerExternalLoginInfoProvider>();
    builder.Services.AddScoped<AccountControllerDependencies>();
    builder.Services.AddScoped<HomeControllerDependencies>();
    builder.Services.AddScoped<ShareControllerDependencies>();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSession(options => options.IdleTimeout = TimeSpan.FromMinutes(20));
    builder.Services.AddFamilyAuthentication(builder.Configuration, builder.Environment);
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
        options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("::1"), 128));
    });
}

static void ConfigurePhotoStorage(WebApplicationBuilder builder)
{
    var photosProvider = builder.Configuration["Photos:Provider"] ?? "Local";
    if (photosProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
    {
        builder.Services.AddSingleton<IAmazonS3>(sp =>
        {
            var photos = sp.GetRequiredService<IOptions<PhotosOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(photos.S3ServiceUrl))
            {
                var config = new AmazonS3Config
                {
                    ServiceURL = photos.S3ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = photos.S3Region
                };
                var accessKey = photos.S3AccessKey ?? "minioadmin";
                var secretKey = photos.S3SecretKey ?? "minioadmin";
                return new AmazonS3Client(accessKey, secretKey, config);
            }
            return new AmazonS3Client();
        });
        builder.Services.AddSingleton<IPhotoStorageService, S3PhotoStorageService>();
        return;
    }

    builder.Services.AddSingleton<IPhotoStorageService, LocalPhotoStorageService>();
}

static FamilyTreeOpenTelemetryOptions ConfigureTelemetry(WebApplicationBuilder builder)
{
    var telemetryOptions = new FamilyTreeOpenTelemetryOptions();
    builder.Configuration.GetSection("Telemetry").Bind(telemetryOptions);
    builder.Services.AddSingleton<IOpenTelemetryOptions>(telemetryOptions);
    builder.Services.AddSingleton<ICorrelationIdService, CorrelationIdService>();
    builder.Services.AddSingleton<IAttributeEnricher, AttributeEnricher>();

    using (var bootstrapLog = new LoggerConfiguration().WriteTo.Console().CreateLogger())
    {
        TelemetryConfigLogger.LogConfig(telemetryOptions, bootstrapLog, "configuration (before)");
    }

    builder.Host.UseSerilog((context, services, loggerConfig) =>
    {
        loggerConfig.ReadFrom.Configuration(context.Configuration);
        var options = services.GetRequiredService<IOpenTelemetryOptions>();
        loggerConfig.AddOpenTelemetry(options, context.Configuration, services);
    });

    var otelBuilder = builder.Services.AddOpenTelemetry();
    var otlpEndpoint = builder.Configuration["Telemetry:Otlp:Endpoint"];
    if (telemetryOptions.Enabled && !string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        builder.Services.AddSingleton<TruncatingSpanProcessor>();
        otelBuilder.ConfigureResource(res => res
            .AddService(telemetryOptions.ServiceName, serviceVersion: telemetryOptions.Version)
            .AddEnvironmentVariableDetector()
            .AddTelemetrySdk()
            .AddAttributes(telemetryOptions.Attributes));

        builder.Services.Configure<OtlpExporterOptions>(builder.Configuration.GetSection("Telemetry:Otlp"));

        if (telemetryOptions.TraceSourceNames.Any())
        {
            otelBuilder.WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(telemetryOptions.TraceSourceNames.ToArray())
                    .AddAspNetCoreInstrumentation()
                    .AddNpgsql()
                    .AddOtlpExporter(opt =>
                    {
                        opt.BatchExportProcessorOptions.MaxQueueSize = telemetryOptions.TracesMaxQueueSize;
                        opt.BatchExportProcessorOptions.ScheduledDelayMilliseconds = telemetryOptions.TracesExportDelayMs;
                        opt.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = telemetryOptions.TracesExportTimeoutMs;
                        opt.BatchExportProcessorOptions.MaxExportBatchSize = telemetryOptions.TracesMaxBatchSize;
                    })
                    .AddProcessor(sp => sp.GetRequiredService<TruncatingSpanProcessor>());

                if (telemetryOptions.EnableHttpInstrumentation)
                    tracing.AddHttpClientInstrumentation(x => x.RecordException = true);
            });
        }
    }

    builder.Services.AddMetrics(otelBuilder, telemetryOptions, _ => { });
    return telemetryOptions;
}

static void ConfigureDatabase(WebApplicationBuilder builder)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Host=localhost;Port=5432;Database=familytree;Username=familytree;Password=familytree";
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.ConfigureTracing(o => o.ConfigureCommandEnrichmentCallback((activity, cmd) =>
    {
        activity?.SetTag("db.statement", cmd.CommandText);
    }));
    var npgsqlDataSource = dataSourceBuilder.Build();
    builder.Services.AddSingleton(npgsqlDataSource);
    builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(npgsqlDataSource));
    builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
}

static void ConfigureEmail(WebApplicationBuilder builder)
{
    var emailProvider = builder.Configuration["Email:Provider"] ?? "Logging";
    if (emailProvider.Equals("Ses", StringComparison.OrdinalIgnoreCase)
        && !builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddSingleton<IAmazonSimpleEmailService>(sp =>
        {
            var email = sp.GetRequiredService<IOptions<EmailOptions>>().Value;
            var region = RegionEndpoint.GetBySystemName(
                string.IsNullOrWhiteSpace(email.Region) ? "us-east-1" : email.Region);
            return new AmazonSimpleEmailServiceClient(region);
        });
        builder.Services.AddScoped<IEmailSender, SesEmailSender>();
        return;
    }

    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

static void ConfigureTelemetryStartupLogging(WebApplication app, FamilyTreeOpenTelemetryOptions telemetryOptions)
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var options = app.Services.GetRequiredService<IOpenTelemetryOptions>();
        TelemetryConfigLogger.LogConfig(options, Log.Logger, "configured (after)");
    });
}

static void ConfigureMiddleware(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();

    var pathsOptions = app.Services.GetRequiredService<IOptions<PathsOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(pathsOptions.Uploads))
    {
        var uploadsFullPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, pathsOptions.Uploads));
        Directory.CreateDirectory(uploadsFullPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsFullPath),
            RequestPath = "/uploads"
        });
    }

    app.MapStaticAssets().AllowAnonymous();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapControllerRoute(
        name: "landing",
        pattern: "",
        defaults: new { controller = "Home", action = "Landing" })
        .WithStaticAssets();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
        .WithStaticAssets();
}