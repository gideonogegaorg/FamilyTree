using System.Security.Claims;

using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class HomeControllerIndexTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task Index_without_current_tree_redirects_to_family_tree_list()
    {
        await using var db = CreateDb(nameof(Index_without_current_tree_redirects_to_family_tree_list));
        var currentTree = new Mock<ICurrentFamilyTreeService>();
        currentTree.Setup(c => c.GetCurrentFamilyTreeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((long?)null);

        var controller = CreateController(db, currentTree.Object, userId: "user-1");
        var result = await controller.Index(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FamilyTreeController.Index), redirect.ActionName);
        Assert.Equal("FamilyTree", redirect.ControllerName);
    }

    [Fact]
    public async Task Index_when_tree_record_missing_redirects_to_family_tree_list()
    {
        await using var db = CreateDb(nameof(Index_when_tree_record_missing_redirects_to_family_tree_list));
        var currentTree = new Mock<ICurrentFamilyTreeService>();
        currentTree.Setup(c => c.GetCurrentFamilyTreeIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(99L);

        var controller = CreateController(db, currentTree.Object, userId: "user-1");
        var result = await controller.Index(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(FamilyTreeController.Index), redirect.ActionName);
    }

    private static HomeController CreateController(AppDbContext db, ICurrentFamilyTreeService currentTree, string userId)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());

        var googleAuth = new Mock<IOptionsMonitor<GoogleAuthOptions>>();
        googleAuth.Setup(g => g.CurrentValue).Returns(new GoogleAuthOptions());

        var controller = new HomeController(
            new HomeControllerDependencies(
                db,
                currentTree,
                new Mock<ITreeViewOrientationService>().Object,
                new Mock<ILineageModeService>().Object,
                new Mock<ITreeCardViewModeService>().Object,
                new FamilyTreeAccessService(db),
                googleAuth.Object,
                env.Object));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId)],
                    "test"))
            }
        };
        return controller;
    }
}