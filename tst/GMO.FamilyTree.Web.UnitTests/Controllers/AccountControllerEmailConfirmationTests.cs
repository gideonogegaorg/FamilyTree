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
using Microsoft.EntityFrameworkCore;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class AccountControllerEmailConfirmationTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerEmailConfirmationTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task Register_sends_confirmation_and_does_not_sign_in()
    {
        await using var db = _f.CreateDb(nameof(Register_sends_confirmation_and_does_not_sign_in));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var email = new Mock<IEmailSender>();
        string? sentTo = null;
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string, string, string, string>((to, _, _, _, _) => sentTo = to)
            .Returns(Task.CompletedTask);

        var controller = CreateWithEmail(db, signIn, users, email.Object);
        var result = await controller.Register(new RegisterViewModel
        {
            Email = "new@example.com",
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        }, returnUrl: "/Home/Index");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.RegisterConfirmation), redirect.ActionName);
        Assert.Equal("new@example.com", sentTo);
        var user = await users.FindByEmailAsync("new@example.com");
        Assert.NotNull(user);
        Assert.False(await users.IsEmailConfirmedAsync(user!));
        Assert.True(await db.FamilyTrees.AnyAsync(t => t.OwnerId == user!.Id));
    }

    [Fact]
    public async Task ConfirmEmail_marks_user_confirmed()
    {
        await using var db = _f.CreateDb(nameof(ConfirmEmail_marks_user_confirmed));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "c@example.com", Email = "c@example.com" };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);
        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var controller = CreateWithEmail(db, signIn, users, new Mock<IEmailSender>().Object);
        var result = await controller.ConfirmEmail(user.Id!, code);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Contains("confirming", Assert.IsType<string>(view.Model), StringComparison.OrdinalIgnoreCase);
        Assert.True(await users.IsEmailConfirmedAsync(user));
    }

    [Fact]
    public async Task Login_when_unconfirmed_shows_resend_hint()
    {
        await using var db = _f.CreateDb(nameof(Login_when_unconfirmed_shows_resend_hint));
        var users = CreateUserManager(db);
        var user = new IdentityUser { UserName = "u@example.com", Email = "u@example.com", EmailConfirmed = false };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var signIn = new Mock<SignInManager<IdentityUser>>(
            users,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IdentityUser>>(),
            null!, null!, null!, null!);
        signIn.Setup(s => s.PasswordSignInAsync("u@example.com", "TestPassword1!", false, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);

        var controller = CreateWithEmail(db, signIn.Object, users, new Mock<IEmailSender>().Object);
        var result = await controller.Login(new LoginViewModel
        {
            Email = "u@example.com",
            Password = "TestPassword1!"
        }, returnUrl: "/");

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ViewBag.ShowResendConfirmation == true);
    }

    [Fact]
    public async Task ForgotPassword_skips_email_when_unconfirmed()
    {
        await using var db = _f.CreateDb(nameof(ForgotPassword_skips_email_when_unconfirmed));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "u@example.com", Email = "u@example.com", EmailConfirmed = false };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);
        var email = new Mock<IEmailSender>(MockBehavior.Strict);

        var controller = CreateWithEmail(db, signIn, users, email.Object);
        var result = await controller.ForgotPassword(new ForgotPasswordViewModel { Email = "u@example.com" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.ForgotPasswordConfirmation), redirect.ActionName);
        email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private static UserManager<IdentityUser> CreateUserManager(AppDbContext db)
    {
        var (_, users) = new AccountControllerFixture().CreateIdentityManagers(db);
        return users;
    }

    private AccountController CreateWithEmail(
        AppDbContext db,
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        IEmailSender emailSender)
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
            signIn, users, emailSender, googleAuth, db, currentTree, treeViewOrientation, lineageMode,
            defaultTree, familyTreeDeletion, external, photos, treeCardViewMode, access,
            AccountControllerFixture.CreateAllowAllRateLimiter(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance);

        var url = new UrlHelperMock().Object;
        var http = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = url;
        return controller;
    }
}