using System.Security.Claims;

using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using GMO.FamilyTree.Web.UnitTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class HomeControllerLandingTests
{
    private static string ResolveWebRootPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GMO.FamilyTree.sln")))
            dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "GMO.FamilyTree.Web", "wwwroot");
    }

    [Fact]
    public async Task Landing_anonymous_user_returns_view_with_demo_tree_data()
    {
        await using var db = CreateDb(nameof(Landing_anonymous_user_returns_view_with_demo_tree_data));
        var controller = CreateController(db, authenticated: false, webRootPath: ResolveWebRootPath());

        var result = await controller.Landing(CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LandingPageViewModel>(view.Model);
        Assert.Contains("James", model.DemoNodesJson);
        Assert.NotEqual("[]", model.DemoNodesJson);
    }

    [Fact]
    public async Task Landing_authenticated_user_redirects_to_Home_Index()
    {
        await using var db = CreateDb(nameof(Landing_authenticated_user_redirects_to_Home_Index));
        var controller = CreateController(db, authenticated: true, webRootPath: ResolveWebRootPath());

        var result = await controller.Landing(CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Home/Index", redirect.Url);
    }

    [Fact]
    public async Task Landing_without_demo_file_returns_empty_view_model()
    {
        await using var db = CreateDb(nameof(Landing_without_demo_file_returns_empty_view_model));
        var emptyWebRoot = Path.Combine(Path.GetTempPath(), "ft-empty-wwwroot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyWebRoot);
        try
        {
            var controller = CreateController(db, authenticated: false, webRootPath: emptyWebRoot);

            var result = await controller.Landing(CancellationToken.None);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<LandingPageViewModel>(view.Model);
            Assert.Equal("[]", model.DemoNodesJson);
            Assert.Equal("[]", model.DemoEdgesJson);
        }
        finally
        {
            Directory.Delete(emptyWebRoot, recursive: true);
        }
    }

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    private static HomeController CreateController(AppDbContext db, bool authenticated, string webRootPath)
    {
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(webRootPath);

        var googleAuth = new Mock<IOptionsMonitor<GoogleAuthOptions>>();
        googleAuth.Setup(g => g.CurrentValue).Returns(new GoogleAuthOptions());

        var controller = new HomeController(
            AccountControllerFixture.CreateHomeDependencies(
                db,
                new Mock<ICurrentFamilyTreeService>().Object,
                new Mock<ITreeViewOrientationService>().Object,
                new Mock<ILineageModeService>().Object,
                new Mock<ITreeCardViewModeService>().Object,
                new FamilyTreeAccessService(db),
                googleAuth.Object,
                env.Object));

        var identity = authenticated
            ? new ClaimsIdentity([new Claim(ClaimTypes.Name, "test@example.com")], "test")
            : new ClaimsIdentity();
        controller.ControllerContext = new()
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }
}