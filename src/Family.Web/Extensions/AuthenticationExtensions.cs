using Family.Web.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Family.Web.Extensions;

/// <summary>
/// Static helpers for authentication setup.
/// </summary>
public static class AuthenticationExtensions
{
    /// <summary>
    /// Creates and configures <see cref="GoogleAuthOptions"/> from configuration (same key resolution as DI).
    /// </summary>
    public static GoogleAuthOptions GetGoogleAuthOptions(this IConfiguration configuration)
    {
        var options = new GoogleAuthOptions();
        new ConfigureGoogleAuthOptions(configuration).Configure(options);
        return options;
    }

    /// <summary>
    /// Registers Google authentication when ClientId and ClientSecret are configured.
    /// Use IOptionsMonitor&lt;GoogleAuthOptions&gt; to pick up config changes at runtime.
    /// </summary>
    /// <returns>True if Google auth was enabled (so the app should call UseAuthentication()).</returns>
    public static bool AddGoogleAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GoogleAuthOptions>()
            .Configure<IConfiguration>((options, config) => new ConfigureGoogleAuthOptions(config).Configure(options));

        var googleAuth = configuration.GetGoogleAuthOptions();
        if (!googleAuth.Enabled)
            return false;

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/";
                options.AccessDeniedPath = "/";
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = googleAuth.ClientId!;
                options.ClientSecret = googleAuth.ClientSecret!;
            });
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return true;
    }
}
