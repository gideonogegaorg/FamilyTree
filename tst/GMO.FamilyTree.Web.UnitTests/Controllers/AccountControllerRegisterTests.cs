using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.UnitTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

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
}
