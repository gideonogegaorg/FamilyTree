using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;

using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GMO.FamilyTree.Web.Extensions;

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
    /// Registers ASP.NET Core Identity and optionally Google as an external provider.
    /// Login path is set to /Account/Login. UseAuthentication() must always be called when using this.
    /// When environment is Testing, the fallback "require authenticated user" policy is not applied so integration tests can hit endpoints without auth.
    /// </summary>
    /// <returns>True if Google auth was enabled (for optional UI hints).</returns>
    public static bool AddFamilyAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? hostEnvironment = null)
    {
        services.AddOptions<GoogleAuthOptions>()
            .Configure<IConfiguration>((options, config) => new ConfigureGoogleAuthOptions(config).Configure(options));

        services
            .AddIdentity<IdentityUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        var googleAuth = configuration.GetGoogleAuthOptions();
        if (googleAuth.Enabled)
        {
            services.AddAuthentication().AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                options.ClientId = googleAuth.ClientId!;
                options.ClientSecret = googleAuth.ClientSecret!;
            });
        }

        var authBuilder = services.AddAuthorizationBuilder();
        if (hostEnvironment?.IsEnvironment("Testing") != true)
            authBuilder.SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return googleAuth.Enabled;
    }
}