using System.Security.Claims;

using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.UnitTests.Fixtures;
using GMO.FamilyTree.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Controllers;

public class ShareControllerTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static async Task<(ShareController Controller, AppDbContext Db, UserManager<IdentityUser> Users, Mock<IEmailSender> Email)> CreateAsync(
        string dbName,
        string userId,
        bool authenticated = true,
        IEmailRateLimiter? rateLimiter = null)
    {
        var db = CreateDb(dbName);
        db.Users.AddRange(
            new IdentityUser { Id = "owner", UserName = "owner@example.com", Email = "owner@example.com", EmailConfirmed = true },
            new IdentityUser { Id = "guest", UserName = "guest@example.com", Email = "guest@example.com", EmailConfirmed = true });
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "Shared Tree", OwnerId = "owner", Uid = Guid.NewGuid() });
        await db.SaveChangesAsync();

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

        var access = new FamilyTreeAccessService(db);
        var share = new FamilyTreeShareService(db, access);
        var current = new CurrentFamilyTreeServiceMock();
        var email = new Mock<IEmailSender>();

        var controller = new ShareController(
            db, userManager, access, share, current.Object, email.Object,
            rateLimiter ?? AccountControllerFixture.CreateAllowAllRateLimiter());
        var http = new DefaultHttpContext();
        if (authenticated)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        }
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/Share/Accept/token");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = url.Object;
        return (controller, db, userManager, email);
    }

    [Fact]
    public async Task Manage_returns_not_found_for_non_owner()
    {
        var (controller, db, _, _) = await CreateAsync(nameof(Manage_returns_not_found_for_non_owner), "guest");
        await using (db)
        {
            var result = await controller.Manage(1, CancellationToken.None);
            Assert.IsType<NotFoundResult>(result);
        }
    }

    [Fact]
    public async Task Manage_returns_view_for_owner()
    {
        var (controller, db, _, _) = await CreateAsync(nameof(Manage_returns_view_for_owner), "owner");
        await using (db)
        {
            var result = await controller.Manage(1, CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            Assert.NotNull(view.Model);
        }
    }

    [Fact]
    public async Task Accept_redirects_anonymous_to_login()
    {
        var (controller, db, _, _) = await CreateAsync(nameof(Accept_redirects_anonymous_to_login), "owner", authenticated: false);
        await using (db)
        {
            var result = await controller.Accept("any-token", CancellationToken.None);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirect.ActionName);
            Assert.Equal("Account", redirect.ControllerName);
        }
    }

    [Fact]
    public async Task CreateEmailInvite_does_not_persist_invite_when_rate_limited()
    {
        var rateLimiter = new Mock<IEmailRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquire(
                EmailRateLimitOperations.ShareInvite,
                "guest@example.com",
                It.IsAny<string?>()))
            .Returns(false);

        var (controller, db, _, email) = await CreateAsync(
            nameof(CreateEmailInvite_does_not_persist_invite_when_rate_limited),
            "owner",
            rateLimiter: rateLimiter.Object);
        await using (db)
        {
            var result = await controller.CreateEmailInvite(
                1,
                new CreateEmailInviteInput { Email = "guest@example.com" },
                CancellationToken.None);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Contains("Too many emails", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await db.FamilyTreeInvites.ToListAsync());
            email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    [Fact]
    public async Task CreateEmailInvite_revokes_invite_when_send_fails()
    {
        var (controller, db, _, email) = await CreateAsync(nameof(CreateEmailInvite_revokes_invite_when_send_fails), "owner");
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("send failed"));

        await using (db)
        {
            var result = await controller.CreateEmailInvite(
                1,
                new CreateEmailInviteInput { Email = "guest@example.com" },
                CancellationToken.None);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Contains("Could not send", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            var invite = await db.FamilyTreeInvites.SingleAsync();
            Assert.NotNull(invite.RevokedAt);
        }
    }
}