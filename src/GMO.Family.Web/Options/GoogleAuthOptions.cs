using GMO.Family.Web.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GMO.Family.Web.Options;

/// <summary>
/// Google authentication configuration. Resolved from config keys (Authentication:Google:ClientId/ClientSecret)
/// and env vars (GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET). Use <see cref="IOptionsMonitor{TOptions}"/> to pick up changes.
/// </summary>
public class GoogleAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    /// <summary>
    /// True when both ClientId and ClientSecret have non-empty values.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Configures <see cref="GoogleAuthOptions"/> using the same key resolution as the rest of the app (GetFirstNonEmpty).
/// </summary>
internal sealed class ConfigureGoogleAuthOptions : IConfigureOptions<GoogleAuthOptions>
{
    private readonly IConfiguration _configuration;

    public ConfigureGoogleAuthOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(GoogleAuthOptions options)
    {
        options.ClientId = _configuration.GetFirstNonEmpty("Authentication:Google:ClientId", "GOOGLE_CLIENT_ID");
        options.ClientSecret = _configuration.GetFirstNonEmpty("Authentication:Google:ClientSecret", "GOOGLE_CLIENT_SECRET");
    }
}