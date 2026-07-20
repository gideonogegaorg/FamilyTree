using GMO.FamilyTree.Web.Services;

using GMO.FamilyTree.Web.UnitTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class AccountControllerDeleteFamilyTreeTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public AccountControllerDeleteFamilyTreeTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task DeleteFamilyTree_returns_NotFound_when_user_not_authenticated()
    {
        await using var db = _f.CreateDb(nameof(DeleteFamilyTree_returns_NotFound_when_user_not_authenticated));
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db);
        var deletion = new Mock<IFamilyTreeDeletionService>();
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("user@example.com"),
            familyTreeDeletion: deletion.Object);

        var result = await controller.DeleteFamilyTree(1, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        deletion.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteFamilyTree_returns_NotFound_when_tree_not_found()
    {
        await using var db = _f.CreateDb(nameof(DeleteFamilyTree_returns_NotFound_when_tree_not_found));
        var user = new IdentityUser { Id = "user-1", UserName = "user@example.com", Email = "user@example.com" };
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db, user);
        var deletion = new Mock<IFamilyTreeDeletionService>();
        deletion.Setup(s => s.DeleteAsync("user-1", 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyTreeDeleteResult.NotFound);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("user@example.com"),
            familyTreeDeletion: deletion.Object,
            userId: "user-1");

        var result = await controller.DeleteFamilyTree(9, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteFamilyTree_redirects_Home_on_success()
    {
        await using var db = _f.CreateDb(nameof(DeleteFamilyTree_redirects_Home_on_success));
        var user = new IdentityUser { Id = "user-1", UserName = "user@example.com", Email = "user@example.com" };
        var (signInManager, userManager) = AccountControllerFixture.CreateIdentityManagers(db, user);
        var deletion = new Mock<IFamilyTreeDeletionService>();
        deletion.Setup(s => s.DeleteAsync("user-1", 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyTreeDeleteResult.Deleted);
        var controller = _f.CreateAccountController(
            signInManager, userManager, db,
            AccountControllerFixture.CreateExternalLoginInfoProvider("user@example.com"),
            familyTreeDeletion: deletion.Object,
            userId: "user-1");

        var result = await controller.DeleteFamilyTree(3, CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Home/Index", redirect.Url);
        deletion.Verify(s => s.DeleteAsync("user-1", 3, It.IsAny<CancellationToken>()), Times.Once);
    }
}