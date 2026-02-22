using GMO.Family.Web;
using GMO.Family.Web.Configuration;
using GMO.Family.Web.Extensions;
using GMO.Family.Web.Options;
using GMO.OpenTelemetry;
using GMO.OpenTelemetry.Serilog;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Load env vars from Process, User, and Machine (Windows System) so Configuration["VAR"] picks them up everywhere.
builder.Configuration.Sources.Add(new AllScopesEnvironmentVariablesConfigurationSource());

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
builder.Services.AddMetrics(otelBuilder, telemetryOptions, _ => { });

// Add services to the container.
builder.Services.AddControllersWithViews();

var googleAuthEnabled = builder.Services.AddGoogleAuthentication(builder.Configuration);

var app = builder.Build();

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

app.UseHttpsRedirection();
app.UseRouting();

if (googleAuthEnabled)
    app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
