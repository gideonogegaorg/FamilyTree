namespace GMO.Family.Web.Options;

/// <summary>
/// File system paths (relative to deploy/content root, like logs). Used so uploads live outside the app folder (e.g. DeployPath/uploads).
/// </summary>
public class PathsOptions
{
    /// <summary>
    /// Base directory for user uploads (e.g. profile photos). Relative path is resolved from content root; absolute path used as-is.
    /// When empty, the app falls back to WebRootPath/uploads.
    /// </summary>
    public string? Uploads { get; set; }
}