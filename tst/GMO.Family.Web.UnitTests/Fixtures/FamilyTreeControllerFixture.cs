using System.Security.Claims;

using GMO.Family.Web.Controllers;
using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using GMO.Family.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;
namespace GMO.Family.Web.UnitTests.Fixtures;

public sealed class FamilyTreeControllerFixture
{
    public AppDbContext CreateDb(string? name = null)
    {
        var dbName = name ?? "FamilyTreeCtrl_" + Guid.NewGuid().ToString("N")[..12];
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    public (FamilyTreeController Controller, CurrentFamilyTreeServiceMock CurrentTree, Mock<IFamilyTreeDeletionService> Deletion) CreateController(
        AppDbContext db,
        string userId = "owner-1",
        CurrentFamilyTreeServiceMock? currentTree = null,
        Mock<IFamilyTreeDeletionService>? deletion = null)
    {
        var userStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<IdentityUser>(db);
        var userManager = new UserManager<IdentityUser>(
            userStore,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<IdentityUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            null!);

        var currentTreeMock = currentTree ?? new CurrentFamilyTreeServiceMock();
        var deletionMock = deletion ?? new Mock<IFamilyTreeDeletionService>(MockBehavior.Loose);

        var controller = new FamilyTreeController(db, userManager, currentTreeMock.Object, deletionMock.Object);
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return (controller, currentTreeMock, deletionMock);
    }
}
