using System.Security.Claims;

using GMO.Family.Web.Services;

using Microsoft.AspNetCore.Identity;

using Moq;

namespace GMO.Family.Web.UnitTests.Mocks;

/// <summary>
/// Mock of <see cref="IExternalLoginInfoProvider"/>; default: returns valid external login info (with email).
/// Override in tests for exception paths: no info (null), or missing/empty email.
/// </summary>
public class ExternalLoginInfoProviderMock : Mock<IExternalLoginInfoProvider>
{
    public const string DefaultEmail = "user@example.com";

    public ExternalLoginInfoProviderMock(string? email = DefaultEmail)
        : base(MockBehavior.Loose)
    {
        var info = email != null ? CreateInfo(email) : null;
        Setup(p => p.GetExternalLoginInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(info);
    }

    /// <summary>Override for "external info is null" (e.g. provider failed or no cookie).</summary>
    public void ReturnsNull()
    {
        Setup(p => p.GetExternalLoginInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ExternalLoginInfo?)null);
    }

    /// <summary>Override for "email claim missing or empty" path.</summary>
    public void ReturnsEmptyEmail()
    {
        Setup(p => p.GetExternalLoginInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateInfo(""));
    }

    /// <summary>Override to return info with a specific email (e.g. new user or existing user).</summary>
    public void ReturnsEmail(string email)
    {
        Setup(p => p.GetExternalLoginInfoAsync(It.IsAny<CancellationToken>())).ReturnsAsync(CreateInfo(email));
    }

    private static ExternalLoginInfo CreateInfo(string email)
    {
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(ClaimTypes.Email, email));
        var principal = new ClaimsPrincipal(identity);
        return new ExternalLoginInfo(principal, "Google", "provider-key", "Google");
    }
}
