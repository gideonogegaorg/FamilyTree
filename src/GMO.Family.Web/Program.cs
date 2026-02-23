using System.Diagnostics;
using System.Net;

using GMO.Family.Web;
using GMO.Family.Web.Data;
using GMO.Family.Web.Extensions;
using GMO.Family.Web.Options;
using GMO.Family.Web.Services;
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

builder.Services.Configure<PathsOptions>(builder.Configuration.GetSection("Paths"));

var telemetryOptions = new FamilyOpenTelemetryOptions();
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
var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(otlpEndpoint);

if (telemetryOptions.Enabled && hasOtlpEndpoint)
{
    builder.Services.AddSingleton<TruncatingSpanProcessor>();

    otelBuilder.ConfigureResource(res => res
        .AddService(telemetryOptions.ServiceName, serviceVersion: telemetryOptions.Version)
        .AddEnvironmentVariableDetector()
        .AddTelemetrySdk()
        .AddAttributes(telemetryOptions.Attributes)
    );

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
            {
                tracing.AddHttpClientInstrumentation(x => x.RecordException = true);
            }
        });
    }
}

builder.Services.AddMetrics(otelBuilder, telemetryOptions, _ => { });

// PostgreSQL data source with OpenTelemetry enrichment (statements + parameters).
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=family;Username=family;Password=family";
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.ConfigureTracing(o => o.ConfigureCommandEnrichmentCallback((activity, cmd) =>
{
    activity?.SetTag("db.statement", cmd.CommandText);
    foreach (NpgsqlParameter p in cmd.Parameters)
        activity?.SetTag($"db.query.parameter.{p.ParameterName}", p.Value?.ToString() ?? "(null)");
}));
var npgsqlDataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(npgsqlDataSource);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(npgsqlDataSource));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
builder.Services.AddScoped<ICurrentFamilyTreeService, CurrentFamilyTreeService>();
builder.Services.AddScoped<IDefaultFamilyTreeService, DefaultFamilyTreeService>();
builder.Services.AddScoped<IExternalLoginInfoProvider, SignInManagerExternalLoginInfoProvider>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => options.IdleTimeout = TimeSpan.FromMinutes(20));

builder.Services.AddFamilyAuthentication(builder.Configuration, builder.Environment);

// Trust X-Forwarded-Proto/Host from Nginx (or other reverse proxy) so redirect URIs use the client's scheme (https).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("127.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("::1"), 128));
});

var app = builder.Build();

// Apply EF Core migrations on startup (uses built-in database lock for multi-instance safety).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    var options = app.Services.GetRequiredService<IOpenTelemetryOptions>();
    TelemetryConfigLogger.LogConfig(options, Log.Logger, "configured (after)");
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();