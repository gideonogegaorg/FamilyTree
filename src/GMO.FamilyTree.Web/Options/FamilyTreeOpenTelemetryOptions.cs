using GMO.OpenTelemetry;

namespace GMO.FamilyTree.Web.Options;

/// <summary>
/// OpenTelemetry options for the Family web app. Bound from the "Telemetry" configuration section.
/// </summary>
public sealed class FamilyTreeOpenTelemetryOptions : OpenTelemetryOptionsBase
{
    public override string ApplicationName => "GMO.FamilyTree.Web";
    public override string Version => "1.0.0";
}