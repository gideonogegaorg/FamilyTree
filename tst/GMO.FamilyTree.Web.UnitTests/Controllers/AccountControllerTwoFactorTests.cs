using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;
using GMO.FamilyTree.Web.UnitTests.Fixtures;
using GMO.FamilyTree.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class AccountControllerTwoFactorTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerTwoFactorTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task Login_redirects_to_2fa_when_required()
    {
        await using var db = _f.CreateDb(nameof(Login_redirects_to_2fa_when_required));
        var users = AccountControllerFixture.CreateIdentityManagers(db).Item2;
        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.PasswordSignInAsync("tfa@example.com", "TestPassword1!", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

        var controller = CreateAnonymousController(db, signIn.Object, users, new Mock<IEmailSender>().Object);
        var result = await controller.Login(new LoginViewModel
        {
            Email = "tfa@example.com",
            Password = "TestPassword1!",
            RememberMe = false
        }, returnUrl: "/Home/Index");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.LoginWith2fa), redirect.ActionName);
    }

    [Fact]
    public async Task Login_success_redirects_home_and_enables_email_2fa()
    {
        await using var db = _f.CreateDb(nameof(Login_success_redirects_home_and_enables_email_2fa));
        var (_, users) = AccountControllerFixture.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "setup@example.com", Email = "setup@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.PasswordSignInAsync("setup@example.com", "TestPassword1!", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = CreateAnonymousController(db, signIn.Object, users, new Mock<IEmailSender>().Object);
        var result = await controller.Login(new LoginViewModel
        {
            Email = "setup@example.com",
            Password = "TestPassword1!",
            RememberMe = false
        }, returnUrl: "/Home/Index");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Home/Index", redirect.Url);
        Assert.True(await users.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task LoginWith2fa_GET_sends_email_code()
    {
        await using var db = _f.CreateDb(nameof(LoginWith2fa_GET_sends_email_code));
        var (_, users) = AccountControllerFixture.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "2fa@example.com", Email = "2fa@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync(user);

        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendEmailAsync(user.Email!, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var controller = CreateAnonymousController(db, signIn.Object, users, email.Object);
        var result = await controller.LoginWith2fa(rememberMe: false, returnUrl: "/Home/Index");

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<LoginWith2FaViewModel>(view.Model);
        Assert.True((bool)controller.ViewBag.EmailCodeSent!);
        email.Verify(e => e.SendEmailAsync(
            user.Email!,
            "GOOM Family Tree: your sign-in code",
            It.Is<string>(body => body.Contains("sign-in code", StringComparison.Ordinal)),
            It.Is<string>(text => text.Contains("sign-in code", StringComparison.Ordinal)),
            EmailRateLimitOperations.TwoFactor), Times.Once);
    }

    [Fact]
    public async Task LoginWith2fa_POST_verifies_email_code()
    {
        await using var db = _f.CreateDb(nameof(LoginWith2fa_POST_verifies_email_code));
        var (_, users) = AccountControllerFixture.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "verify@example.com", Email = "verify@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);

        var code = await users.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync(user);
        signIn.Setup(s => s.TwoFactorSignInAsync(TokenOptions.DefaultEmailProvider, code, false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = CreateAnonymousController(db, signIn.Object, users, new Mock<IEmailSender>().Object);
        var result = await controller.LoginWith2fa(new LoginWith2FaViewModel
        {
            TwoFactorCode = code,
            RememberMe = false,
            ReturnUrl = "/Home/Index"
        });

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/Home/Index", redirect.Url);
        signIn.Verify(s => s.TwoFactorSignInAsync(TokenOptions.DefaultEmailProvider, code, false, false), Times.Once);
    }

    [Fact]
    public async Task LoginWith2fa_POST_rejects_invalid_code()
    {
        await using var db = _f.CreateDb(nameof(LoginWith2fa_POST_rejects_invalid_code));
        var (_, users) = AccountControllerFixture.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "badcode@example.com", Email = "badcode@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync(user);
        signIn.Setup(s => s.TwoFactorSignInAsync(TokenOptions.DefaultEmailProvider, "000000", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var controller = CreateAnonymousController(db, signIn.Object, users, new Mock<IEmailSender>().Object);
        var result = await controller.LoginWith2fa(new LoginWith2FaViewModel
        {
            TwoFactorCode = "000000",
            RememberMe = false,
            ReturnUrl = "/Home/Index"
        });

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task LoginWith2fa_GET_sets_rate_limited_flag_when_email_blocked()
    {
        await using var db = _f.CreateDb(nameof(LoginWith2fa_GET_sets_rate_limited_flag_when_email_blocked));
        var (_, users) = AccountControllerFixture.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "rl@example.com", Email = "rl@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);
        Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync(user);

        var rateLimiter = new Mock<IEmailRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquire(EmailRateLimitOperations.TwoFactor, user.Email!, It.IsAny<string?>()))
            .Returns(false);

        var controller = CreateAnonymousController(db, signIn.Object, users, new Mock<IEmailSender>().Object, rateLimiter.Object);
        var result = await controller.LoginWith2fa(rememberMe: false, returnUrl: "/Home/Index");
        Assert.IsType<ViewResult>(result);
        Assert.True((bool)controller.ViewBag.EmailRateLimited!);
    }

    [Fact]
    public async Task ExternalLoginCallback_bypasses_two_factor()
    {
        await using var db = _f.CreateDb(nameof(ExternalLoginCallback_bypasses_two_factor));
        var existingUser = new IdentityUser { UserName = "g@example.com", Email = "g@example.com", EmailConfirmed = true };
        var (_, userManager) = AccountControllerFixture.CreateIdentityManagers(db, existingUser);
        await userManager.AddLoginAsync(existingUser, AccountControllerFixture.CreateExternalLoginInfo("g@example.com"));
        Assert.True((await userManager.AddPasswordAsync(existingUser, "TestPassword1!")).Succeeded);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.ExternalLoginSignInAsync("Google", "provider-key", false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var controller = _f.CreateAccountController(
            signIn.Object, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("g@example.com"),
            AccountControllerFixture.CreateUrlHelper("/home"));

        var result = await controller.ExternalLoginCallback(returnUrl: "/home");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/home", redirect.Url);
        signIn.Verify(s => s.ExternalLoginSignInAsync("Google", "provider-key", false, true), Times.Once);
        signIn.Verify(s => s.ExternalLoginSignInAsync("Google", "provider-key", false, false), Times.Never);
    }

    private static AccountController CreateAnonymousController(
        AppDbContext db,
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        IEmailSender email,
        IEmailRateLimiter? rateLimiter = null)
    {
        var currentTree = new CurrentFamilyTreeServiceMock().Object;
        var treeViewOrientation = new Mock<ITreeViewOrientationService>().Object;
        var lineageMode = new Mock<ILineageModeService>().Object;
        var defaultTree = new DefaultFamilyTreeService(db);
        var familyTreeDeletion = new Mock<IFamilyTreeDeletionService>().Object;
        var photos = new Mock<IPhotoStorageService>().Object;
        var treeCardViewMode = new Mock<ITreeCardViewModeService>().Object;
        var access = new FamilyTreeAccessService(db);
        var googleAuth = new GoogleAuthOptionsMock().Object;
        var external = AccountControllerFixture.CreateExternalLoginInfoProvider("user@example.com");

        var controller = new AccountController(
            new AccountControllerDependencies(
                signIn, users, email, googleAuth, db, currentTree, treeViewOrientation, lineageMode,
                defaultTree, familyTreeDeletion, external, photos, treeCardViewMode, access,
                rateLimiter ?? AccountControllerFixture.CreateAllowAllRateLimiter()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Url = new UrlHelperMock().Object;
        return controller;
    }
}