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
using Microsoft.AspNetCore.Mvc.ViewFeatures;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class AccountControllerEmailRateLimitTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerEmailRateLimitTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task ForgotPassword_does_not_send_when_rate_limited()
    {
        await using var db = _f.CreateDb(nameof(ForgotPassword_does_not_send_when_rate_limited));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "victim@example.com", Email = "victim@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var email = new Mock<IEmailSender>(MockBehavior.Strict);
        var rateLimiter = new Mock<IEmailRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquire(
                EmailRateLimitOperations.ResetRequest,
                "victim@example.com",
                It.IsAny<string?>()))
            .Returns(false);

        var controller = CreateController(db, signIn, users, email.Object, rateLimiter.Object);
        var result = await controller.ForgotPassword(new ForgotPasswordViewModel { Email = "victim@example.com" });

        Assert.IsType<RedirectToActionResult>(result);
        email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_does_not_send_for_google_only_user()
    {
        await using var db = _f.CreateDb(nameof(ForgotPassword_does_not_send_for_google_only_user));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "google@example.com", Email = "google@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        await users.AddLoginAsync(user, new UserLoginInfo("Google", "key", "Google"));

        var email = new Mock<IEmailSender>(MockBehavior.Strict);
        var controller = CreateController(db, signIn, users, email.Object, AccountControllerFixture.CreateAllowAllRateLimiter());
        var result = await controller.ForgotPassword(new ForgotPasswordViewModel { Email = "google@example.com" });

        Assert.IsType<RedirectToActionResult>(result);
        email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ResendConfirmationEmail_sets_temp_data_when_rate_limited()
    {
        await using var db = _f.CreateDb(nameof(ResendConfirmationEmail_sets_temp_data_when_rate_limited));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var user = new IdentityUser { UserName = "new@example.com", Email = "new@example.com", EmailConfirmed = false };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var email = new Mock<IEmailSender>(MockBehavior.Strict);
        var rateLimiter = new Mock<IEmailRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquire(
                EmailRateLimitOperations.Confirmation,
                "new@example.com",
                It.IsAny<string?>()))
            .Returns(false);

        var controller = CreateController(db, signIn, users, email.Object, rateLimiter.Object);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        var result = await controller.ResendConfirmationEmail(new ResendConfirmationViewModel { Email = "new@example.com" });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.RegisterConfirmation), redirect.ActionName);
        Assert.True(controller.TempData.ContainsKey("EmailRateLimited"));
        email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    private static AccountController CreateController(
        AppDbContext db,
        SignInManager<IdentityUser> signIn,
        UserManager<IdentityUser> users,
        IEmailSender emailSender,
        IEmailRateLimiter rateLimiter)
    {
        var controller = new AccountController(
            new AccountControllerDependencies(
                signIn,
                users,
                emailSender,
                new GoogleAuthOptionsMock().Object,
                db,
                new CurrentFamilyTreeServiceMock().Object,
                new Mock<ITreeViewOrientationService>().Object,
                new Mock<ILineageModeService>().Object,
                new DefaultFamilyTreeService(db),
                new Mock<IFamilyTreeDeletionService>().Object,
                new AccountControllerFixture().CreateExternalLoginInfoProvider("user@example.com"),
                new Mock<IPhotoStorageService>().Object,
                new Mock<ITreeCardViewModeService>().Object,
                new FamilyTreeAccessService(db),
                rateLimiter),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountController>.Instance);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.Url = new UrlHelperMock().Object;
        return controller;
    }
}