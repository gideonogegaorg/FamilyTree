using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Services;

using GMO.FamilyTree.Web.UnitTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

/// <summary>
/// Unit tests for external-login flow to improve coverage and lower CRAP score.
/// Uses AutoFixture + AutoMoq via <see cref="AccountControllerFixture"/> for shared setup and less duplication.
/// </summary>
public class AccountControllerExternalLoginTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerExternalLoginTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public void SignIn_returns_google_challenge()
    {
        using var db = _f.CreateDb(nameof(SignIn_returns_google_challenge));
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider(null));

        var result = controller.SignIn(returnUrl: "/Home/Index");

        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public async Task ExternalLoginCallback_redirects_to_Login_when_remoteError_is_set()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_redirects_to_Login_when_remoteError_is_set));
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider(null),
            AccountControllerFixture.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/", remoteError: "access_denied");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ExternalLoginCallback_redirects_to_Login_when_external_info_is_null()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_redirects_to_Login_when_external_info_is_null));
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider(null),
            AccountControllerFixture.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ExternalLoginCallback_redirects_to_Login_when_email_claim_missing()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_redirects_to_Login_when_email_claim_missing));
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider(""),
            AccountControllerFixture.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
    }

    [Fact]
    public async Task ExternalLoginCallback_signs_in_existing_user_and_local_redirects()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_signs_in_existing_user_and_local_redirects));
        var existingUser = new IdentityUser { UserName = "u@example.com", Email = "u@example.com", EmailConfirmed = true };
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db, existingUser);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("u@example.com"),
            AccountControllerFixture.CreateUrlHelper("/home"));

        var result = await controller.ExternalLoginCallback(returnUrl: "/home");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/home", redirect.Url);
    }

    [Fact]
    public async Task ExternalLoginCallback_creates_user_when_not_found_then_signs_in_and_redirects()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_creates_user_when_not_found_then_signs_in_and_redirects));
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("new@example.com"),
            AccountControllerFixture.CreateUrlHelper());

        var result = await controller.ExternalLoginCallback(returnUrl: "/");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
        var user = await userManager.FindByEmailAsync("new@example.com");
        Assert.NotNull(user);
    }

    [Fact]
    public async Task ExternalLoginCallback_confirms_unconfirmed_email_for_existing_user()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_confirms_unconfirmed_email_for_existing_user));
        var existingUser = new IdentityUser { UserName = "squat@example.com", Email = "squat@example.com", EmailConfirmed = false };
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db, existingUser);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("squat@example.com"),
            AccountControllerFixture.CreateUrlHelper("/home"));

        var result = await controller.ExternalLoginCallback(returnUrl: "/home");

        Assert.IsType<LocalRedirectResult>(result);
        var user = await userManager.FindByEmailAsync("squat@example.com");
        Assert.NotNull(user);
        Assert.True(user!.EmailConfirmed);
    }
}