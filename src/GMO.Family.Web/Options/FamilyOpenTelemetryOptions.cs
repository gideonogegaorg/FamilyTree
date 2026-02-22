using GMO.OpenTelemetry;

namespace GMO.Family.Web.Options;

/// <summary>
/// OpenTelemetry options for the Family web app. Bound from the "Telemetry" configuration section.
/// </summary>
public sealed class FamilyOpenTelemetryOptions : OpenTelemetryOptionsBase
{
    public override string ServiceName => "GMO.Family.Web";
    public override string Version => "1.0.0";
}