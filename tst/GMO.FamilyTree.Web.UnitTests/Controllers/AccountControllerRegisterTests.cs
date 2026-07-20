using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;
using GMO.FamilyTree.Web.UnitTests.Fixtures;
using GMO.FamilyTree.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class AccountControllerRegisterTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerRegisterTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task Register_deletes_user_when_default_tree_creation_fails()
    {
        await using var db = _f.CreateDb(nameof(Register_deletes_user_when_default_tree_creation_fails));
        var (signIn, users) = _f.CreateIdentityManagers(db);

        var defaultTree = new Mock<IDefaultFamilyTreeService>();
        defaultTree.Setup(s => s.EnsureDefaultFamilyTreeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("tree creation failed"));

        var controller = _f.CreateAccountController(
            signIn,
            users,
            db,
            _f.CreateExternalLoginInfoProvider("user@example.com"),
            defaultFamilyTreeService: defaultTree.Object);
        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "newuser@example.com",
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        var view = Assert.IsType<ViewResult>(result);
        Assert.NotNull(view.Model);
        Assert.Null(await users.FindByEmailAsync("newuser@example.com"));
    }

    [Fact]
    public async Task Register_creates_default_tree_profile_and_enables_email_2fa()
    {
        await using var db = _f.CreateDb(nameof(Register_creates_default_tree_profile_and_enables_email_2fa));
        var (signIn, users) = _f.CreateIdentityManagers(db);
        var email = new Mock<IEmailSender>();
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var controller = new AccountController(
            new AccountControllerDependencies(
                signIn,
                users,
                email.Object,
                new GoogleAuthOptionsMock().Object,
                db,
                new CurrentFamilyTreeServiceMock().Object,
                Mock.Of<ITreeViewOrientationService>(),
                Mock.Of<ILineageModeService>(),
                new DefaultFamilyTreeService(db),
                Mock.Of<IFamilyTreeDeletionService>(),
                _f.CreateExternalLoginInfoProvider("user@example.com"),
                Mock.Of<IPhotoStorageService>(),
                Mock.Of<ITreeCardViewModeService>(),
                new FamilyTreeAccessService(db),
                AccountControllerFixture.CreateAllowAllRateLimiter()),
            NullLogger<AccountController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        controller.Url = _f.CreateUrlHelper("/Home/Index");
        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext,
            Mock.Of<ITempDataProvider>());

        var result = await controller.Register(new RegisterViewModel
        {
            Email = "fresh@example.com",
            Password = "TestPassword1!",
            ConfirmPassword = "TestPassword1!"
        });

        Assert.IsType<RedirectToActionResult>(result);
        var user = await users.FindByEmailAsync("fresh@example.com");
        Assert.NotNull(user);
        Assert.True(await users.GetTwoFactorEnabledAsync(user!));
        Assert.True(await db.FamilyTrees.AnyAsync(t => t.OwnerId == user.Id));
        Assert.NotNull(await db.UserProfiles.FindAsync(user.Id));
    }
}