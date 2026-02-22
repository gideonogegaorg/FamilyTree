using Microsoft.AspNetCore.Identity;

namespace GMO.Family.Web.Services;

/// <summary>
/// Production implementation: reads external login info from SignInManager (OAuth callback).
/// </summary>
public sealed class SignInManagerExternalLoginInfoProvider : IExternalLoginInfoProvider
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public SignInManagerExternalLoginInfoProvider(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(CancellationToken cancellationToken = default) =>
        _signInManager.GetExternalLoginInfoAsync();
}