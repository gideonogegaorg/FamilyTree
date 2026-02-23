using GMO.Family.Web.Controllers;
using GMO.Family.Web.Services;

using GMO.Family.Web.UnitTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace GMO.Family.Web.UnitTests.Controllers;

/// <summary>
/// Unit tests for external-login flow to improve coverage and lower CRAP score.
/// Uses AutoFixture + AutoMoq via <see cref="AccountControllerFixture"/> for shared setup and less duplication.
/// </summary>
public class AccountControllerExternalLoginTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerExternalLoginTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task ExternalLoginCallback_redirects_to_Login_when_remoteError_is_set()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_redirects_to_Login_when_remoteError_is_set));
        var (signInManager, userManager) = _f.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            _f.CreateExternalLoginInfoProvider(null),
            _f.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/", remoteError: "access_denied");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ExternalLoginCallback_redirects_to_Login_when_external_info_is_null()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_redirects_to_Login_when_external_info_is_null));
        var (signInManager, userManager) = _f.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            _f.CreateExternalLoginInfoProvider(null),
            _f.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ExternalLoginCallback_redirects_to_Login_when_email_claim_missing()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_redirects_to_Login_when_email_claim_missing));
        var (signInManager, userManager) = _f.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            _f.CreateExternalLoginInfoProvider(""),
            _f.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ExternalLoginCallback_signs_in_existing_user_and_local_redirects()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_signs_in_existing_user_and_local_redirects));
        var existingUser = new IdentityUser { UserName = "u@example.com", Email = "u@example.com", EmailConfirmed = true };
        var (signInManager, userManager) = _f.CreateIdentityManagers(db, existingUser);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            _f.CreateExternalLoginInfoProvider("u@example.com"),
            _f.CreateUrlHelper("/home"));

        var result = await controller.ExternalLoginCallback(returnUrl: "/home");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/home", redirect.Url);
    }

    [Fact]
    public async Task ExternalLoginCallback_creates_user_when_not_found_then_signs_in_and_redirects()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_creates_user_when_not_found_then_signs_in_and_redirects));
        var (signInManager, userManager) = _f.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            _f.CreateExternalLoginInfoProvider("new@example.com"),
            _f.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
        var user = await userManager.FindByEmailAsync("new@example.com");
        Assert.NotNull(user);
    }
}