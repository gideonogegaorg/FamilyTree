using GMO.OpenTelemetry;

namespace GMO.Family.Web;

/// <summary>
/// Logs OpenTelemetry configuration for diagnostics (non-secret values only).
/// </summary>
internal static class TelemetryConfigLogger
{
    /// <summary>
    /// Logs the telemetry options we picked up (traces, metrics, logs). Does not log endpoints, headers, or API keys.
    /// </summary>
    public static void LogConfig(IOpenTelemetryOptions options, Serilog.ILogger logger, string phase)
    {
        if (logger == null!) return;

        var otlpSet = options.Otlp?.Endpoint != null;
        var metricsEndpointSet = options.MetricsEndpoint != null;
        var loggingEndpointSet = !string.IsNullOrEmpty(options.LoggingEndpoint);

        logger.Information(
            "OpenTelemetry {Phase}: Enabled={Enabled}, EnvironmentName={EnvironmentName}, ServiceName={ServiceName}, Version={Version}. " +
            "Traces: OtlpEndpoint={OtlpSet}, SourceNames={TraceSourceNames}. " +
            "Metrics: MetricsEndpoint={MetricsEndpointSet}, SourceNames={MeterSourceNames}. " +
            "Logs: EnableLogging={EnableLogging}, LoggingEndpoint={LoggingEndpointSet}.",
            phase,
            options.Enabled,
            options.EnvironmentName,
            options.ServiceName,
            options.Version,
            otlpSet ? "(set)" : "(not set)",
            options.TraceSourceNames ?? Array.Empty<string>(),
            metricsEndpointSet ? "(set)" : "(not set)",
            options.MeterSourceNames ?? Array.Empty<string>(),
            options.EnableLogging,
            loggingEndpointSet ? "(set)" : "(not set)");
    }
}
