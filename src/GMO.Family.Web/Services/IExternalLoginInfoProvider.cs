using Microsoft.AspNetCore.Identity;

namespace GMO.Family.Web.Services;

/// <summary>
/// Provides external login info (e.g. from OAuth callback). Allows tests to supply fake info without mocking SignInManager.
/// </summary>
public interface IExternalLoginInfoProvider
{
    Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(CancellationToken cancellationToken = default);
}
