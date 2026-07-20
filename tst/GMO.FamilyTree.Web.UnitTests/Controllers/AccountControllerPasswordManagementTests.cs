using System.Text;

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
using Microsoft.AspNetCore.WebUtilities;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class AccountControllerPasswordManagementTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerPasswordManagementTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task SetPassword_succeeds_for_signed_in_user_without_password()
    {
        await using var db = _f.CreateDb(nameof(SetPassword_succeeds_for_signed_in_user_without_password));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "g@example.com", Email = "g@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var email = new Mock<IEmailSender>(MockBehavior.Strict);
        var controller = CreateAuthenticated(db, signIn, users, user.Id!, email.Object);
        var result = await controller.SetPassword(new SetPasswordViewModel
        {
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.True(await users.HasPasswordAsync(user));
        Assert.True(await users.GetTwoFactorEnabledAsync(user));
        email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddPassword_with_valid_token_succeeds_and_redirects_home_when_signed_in()
    {
        await using var db = _f.CreateDb(nameof(AddPassword_with_valid_token_succeeds_and_redirects_home_when_signed_in));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "add@example.com", Email = "add@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, AccountController.AddPasswordTokenPurpose);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var controller = CreateAuthenticated(db, signIn, users, user.Id!, new Mock<IEmailSender>().Object);
        var result = await controller.AddPassword(new AddPasswordViewModel
        {
            UserId = user.Id!,
            Code = code,
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.True(await users.HasPasswordAsync(user));
        Assert.True(await users.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task AddPassword_when_anonymous_redirects_to_login()
    {
        await using var db = _f.CreateDb(nameof(AddPassword_when_anonymous_redirects_to_login));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "anon@example.com", Email = "anon@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, AccountController.AddPasswordTokenPurpose);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var controller = CreateAnonymous(db, signIn, users, new Mock<IEmailSender>().Object);
        var result = await controller.AddPassword(new AddPasswordViewModel
        {
            UserId = user.Id!,
            Code = code,
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.Login), redirect.ActionName);
        Assert.True(await users.HasPasswordAsync(user));
        Assert.True(await users.GetTwoFactorEnabledAsync(user));
    }

    [Fact]
    public async Task AddPassword_with_invalid_token_fails()
    {
        await using var db = _f.CreateDb(nameof(AddPassword_with_invalid_token_fails));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "bad@example.com", Email = "bad@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var controller = CreateAnonymous(db, signIn, users, new Mock<IEmailSender>().Object);
        var result = await controller.AddPassword(new AddPasswordViewModel
        {
            UserId = user.Id!,
            Code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes("not-a-valid-token")),
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("AddPasswordError", view.ViewName);
        Assert.False(await users.HasPasswordAsync(user));
    }

    [Fact]
    public async Task AddPassword_rejected_when_password_already_set()
    {
        await using var db = _f.CreateDb(nameof(AddPassword_rejected_when_password_already_set));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "has@example.com", Email = "has@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, AccountController.AddPasswordTokenPurpose);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var controller = CreateAnonymous(db, signIn, users, new Mock<IEmailSender>().Object);
        var result = await controller.AddPassword(new AddPasswordViewModel
        {
            UserId = user.Id!,
            Code = code,
            Password = "OtherPassword1!",
            ConfirmPassword = "OtherPassword1!"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("AddPasswordError", view.ViewName);
    }

    [Fact]
    public async Task ConfirmAddPassword_with_valid_token_shows_form()
    {
        await using var db = _f.CreateDb(nameof(ConfirmAddPassword_with_valid_token_shows_form));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "form@example.com", Email = "form@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var token = await users.GenerateUserTokenAsync(user, TokenOptions.DefaultProvider, AccountController.AddPasswordTokenPurpose);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var controller = CreateAnonymous(db, signIn, users, new Mock<IEmailSender>().Object);
        var result = await controller.ConfirmAddPassword(user.Id!, code);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("AddPassword", view.ViewName);
        var model = Assert.IsType<AddPasswordViewModel>(view.Model);
        Assert.Equal(user.Id, model.UserId);
        Assert.Equal(code, model.Code);
    }

    [Fact]
    public async Task ChangePassword_succeeds_for_password_user()
    {
        await using var db = _f.CreateDb(nameof(ChangePassword_succeeds_for_password_user));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "chg@example.com", Email = "chg@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var controller = CreateAuthenticated(db, signIn, users, user.Id!, new Mock<IEmailSender>().Object);
        var result = await controller.ChangePassword(new ChangePasswordViewModel
        {
            CurrentPassword = "TestPassword1!",
            NewPassword = "NewPassword1!",
            ConfirmPassword = "NewPassword1!"
        });

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ChangePasswordViewModel>(view.Model);
        Assert.Equal("Your password has been changed.", model.StatusMessage);
        Assert.True(await users.CheckPasswordAsync(user, "NewPassword1!"));
    }

    [Fact]
    public async Task ManagePassword_shows_change_when_has_password()
    {
        await using var db = _f.CreateDb(nameof(ManagePassword_shows_change_when_has_password));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "mp@example.com", Email = "mp@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var controller = CreateAuthenticated(db, signIn, users, user.Id!, new Mock<IEmailSender>().Object);
        var result = await controller.ManagePassword();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ChangePassword", view.ViewName);
    }

    [Fact]
    public async Task ManagePassword_shows_set_when_no_password()
    {
        await using var db = _f.CreateDb(nameof(ManagePassword_shows_set_when_no_password));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "np@example.com", Email = "np@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var controller = CreateAuthenticated(db, signIn, users, user.Id!, new Mock<IEmailSender>().Object);
        var result = await controller.ManagePassword();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("SetPassword", view.ViewName);
    }

    [Fact]
    public async Task Register_existing_google_only_email_shows_add_password_guidance()
    {
        await using var db = _f.CreateDb(nameof(Register_existing_google_only_email_shows_add_password_guidance));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "exists@example.com", Email = "exists@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);

        var controller = CreateAnonymous(db, signIn, users, new Mock<IEmailSender>().Object);
        var result = await controller.Register(new RegisterViewModel
        {
            Email = "exists@example.com",
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Contains(controller.ModelState[string.Empty]!.Errors,
            e => e.ErrorMessage.Contains("Set password", StringComparison.OrdinalIgnoreCase));
    }

    private AccountController CreateAuthenticated(
        AppDbContext db,
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        string userId,
        IEmailSender email)
    {
        var controller = CreateAnonymous(db, signIn, users, email);
        var identity = new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)],
            "test");
        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(identity);
        return controller;
    }

    private AccountController CreateAnonymous(
        AppDbContext db,
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        IEmailSender email)
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
        var external = _f.CreateExternalLoginInfoProvider("user@example.com");

        var controller = new AccountController(
            new AccountControllerDependencies(
                signIn, users, email, googleAuth, db, currentTree, treeViewOrientation, lineageMode,
                defaultTree, familyTreeDeletion, external, photos, treeCardViewMode, access,
                AccountControllerFixture.CreateAllowAllRateLimiter()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Url = new UrlHelperMock().Object;
        return controller;
    }
}