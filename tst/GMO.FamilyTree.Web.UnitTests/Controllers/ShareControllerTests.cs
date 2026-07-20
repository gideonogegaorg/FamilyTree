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

    private static async Task<(ShareController Controller, AppDbContext Db, UserManager<IdentityUser> Users, Mock<IEmailSender> Email, Mock<ICurrentFamilyTreeService> Current)> CreateAsync(
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
        var current = new Mock<ICurrentFamilyTreeService>();
        current.Setup(c => c.SetCurrentFamilyTreeIdAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var email = new Mock<IEmailSender>();

        var controller = new ShareController(
            db, userManager, access, share, current.Object, email.Object,
            rateLimiter ?? AccountControllerFixture.CreateAllowAllRateLimiter(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShareController>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        if (authenticated)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId)], "test"));
        }
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/Share/Accept/token");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = url.Object;
        return (controller, db, userManager, email, current);
    }

    [Fact]
    public async Task Manage_returns_not_found_for_non_owner()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(Manage_returns_not_found_for_non_owner), "guest");
        await using (db)
        {
            var result = await controller.Manage(1, CancellationToken.None);
            Assert.IsType<NotFoundResult>(result);
        }
    }

    [Fact]
    public async Task Manage_returns_view_for_owner()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(Manage_returns_view_for_owner), "owner");
        await using (db)
        {
            var result = await controller.Manage(1, CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Equal("Shared Tree", model.TreeName);
        }
    }

    [Fact]
    public async Task CreateLinkInvite_returns_share_url_for_owner()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(CreateLinkInvite_returns_share_url_for_owner), "owner");
        await using (db)
        {
            var result = await controller.CreateLinkInvite(
                1,
                new CreateLinkInviteInput { Role = TreeShareRole.Editor, ExpiresInDays = 7 },
                CancellationToken.None);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Manage", view.ViewName);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.False(string.IsNullOrEmpty(model.CreatedLinkUrl));
            Assert.Contains("Share link created", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Single(await db.FamilyTreeInvites.ToListAsync());
        }
    }

    [Fact]
    public async Task CreateEmailInvite_sends_email_and_persists_invite()
    {
        var (controller, db, _, email, _) = await CreateAsync(nameof(CreateEmailInvite_sends_email_and_persists_invite), "owner");
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await using (db)
        {
            var result = await controller.CreateEmailInvite(
                1,
                new CreateEmailInviteInput { Email = "guest@example.com", Role = TreeShareRole.Readonly },
                CancellationToken.None);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Contains("Invite sent", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Single(await db.FamilyTreeInvites.Where(i => i.RevokedAt == null).ToListAsync());
            email.Verify(e => e.SendEmailAsync(
                "guest@example.com",
                It.Is<string>(s => s.Contains("owner@example.com", StringComparison.Ordinal)
                    && s.Contains("GOOM Family Tree", StringComparison.Ordinal)),
                It.Is<string>(b => b.Contains("owner@example.com", StringComparison.Ordinal)),
                It.Is<string>(t => t.Contains("owner@example.com", StringComparison.Ordinal)
                    && t.Contains("Accept the invite", StringComparison.Ordinal)),
                EmailRateLimitOperations.ShareInvite), Times.Once);
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

        var (controller, db, _, email, _) = await CreateAsync(
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
            email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    [Fact]
    public async Task CreateEmailInvite_revokes_invite_when_send_fails()
    {
        var (controller, db, _, email, _) = await CreateAsync(nameof(CreateEmailInvite_revokes_invite_when_send_fails), "owner");
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
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

    [Fact]
    public async Task RevokeInvite_marks_invite_revoked()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(RevokeInvite_marks_invite_revoked), "owner");
        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateLinkInviteAsync(1, "owner", TreeShareRole.Editor, null);

            var result = await controller.RevokeInvite(1, invite.Id, CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Contains("revoked", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull((await db.FamilyTreeInvites.SingleAsync()).RevokedAt);
        }
    }

    [Fact]
    public async Task ResendInvite_sends_email_for_pending_email_invite()
    {
        var (controller, db, _, email, _) = await CreateAsync(nameof(ResendInvite_sends_email_for_pending_email_invite), "owner");
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateEmailInviteAsync(1, "owner", "guest@example.com", TreeShareRole.Readonly, null);

            var result = await controller.ResendInvite(1, invite.Id, CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Contains("resent", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            email.Verify(e => e.SendEmailAsync(
                "guest@example.com",
                It.Is<string>(s => s.Contains("owner@example.com", StringComparison.Ordinal)
                    && s.Contains("GOOM Family Tree", StringComparison.Ordinal)),
                It.Is<string>(b => b.Contains("owner@example.com", StringComparison.Ordinal)),
                It.Is<string>(t => t.Contains("owner@example.com", StringComparison.Ordinal)
                    && t.Contains("Accept the invite", StringComparison.Ordinal)),
                EmailRateLimitOperations.ShareInvite), Times.Once);
        }
    }

    [Fact]
    public async Task RemoveCollaborator_and_ChangeRole_update_access()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(RemoveCollaborator_and_ChangeRole_update_access), "owner");
        await using (db)
        {
            db.FamilyTreeAccesses.Add(new FamilyTreeAccess
            {
                FamilyTreeId = 1,
                UserId = "guest",
                Role = TreeShareRole.Readonly,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByUserId = "owner"
            });
            await db.SaveChangesAsync();

            var change = await controller.ChangeRole(1, "guest", TreeShareRole.Editor, CancellationToken.None);
            var changeView = Assert.IsType<ViewResult>(change);
            Assert.Contains("Role updated", Assert.IsType<ShareManageViewModel>(changeView.Model).StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(TreeShareRole.Editor, (await db.FamilyTreeAccesses.SingleAsync()).Role);

            var remove = await controller.RemoveCollaborator(1, "guest", CancellationToken.None);
            var removeView = Assert.IsType<ViewResult>(remove);
            Assert.Contains("Access removed", Assert.IsType<ShareManageViewModel>(removeView.Model).StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await db.FamilyTreeAccesses.ToListAsync());
        }
    }

    [Fact]
    public async Task Accept_redirects_anonymous_to_login()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(Accept_redirects_anonymous_to_login), "owner", authenticated: false);
        await using (db)
        {
            var result = await controller.Accept("any-token", CancellationToken.None);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Login", redirect.ActionName);
            Assert.Equal("Account", redirect.ControllerName);
        }
    }

    [Fact]
    public async Task Accept_valid_link_sets_current_tree_and_redirects_home()
    {
        var (controller, db, _, _, current) = await CreateAsync(nameof(Accept_valid_link_sets_current_tree_and_redirects_home), "guest");
        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateLinkInviteAsync(1, "owner", TreeShareRole.Editor, null);

            var result = await controller.Accept(invite.Token, CancellationToken.None);
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(HomeController.Index), redirect.ActionName);
            Assert.Equal("Home", redirect.ControllerName);
            current.Verify(c => c.SetCurrentFamilyTreeIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task Accept_expired_invite_shows_error()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(Accept_expired_invite_shows_error), "guest");
        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateLinkInviteAsync(1, "owner", TreeShareRole.Readonly, DateTimeOffset.UtcNow.AddDays(-1));

            var result = await controller.Accept(invite.Token, CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("AcceptError", view.ViewName);
            Assert.Contains("expired", Assert.IsType<string>(view.Model), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Accept_blank_token_shows_error()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(Accept_blank_token_shows_error), "guest");
        await using (db)
        {
            var result = await controller.Accept(" ", CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("AcceptError", view.ViewName);
        }
    }

    [Fact]
    public async Task Accept_email_mismatch_and_revoked_show_errors()
    {
        var (controller, db, _, _, _) = await CreateAsync(nameof(Accept_email_mismatch_and_revoked_show_errors), "other");
        db.Users.Add(new IdentityUser { Id = "other", UserName = "other@example.com", Email = "other@example.com", EmailConfirmed = true });
        await db.SaveChangesAsync();

        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var emailInvite = await share.CreateEmailInviteAsync(1, "owner", "guest@example.com", TreeShareRole.Readonly, null);
            var mismatch = await controller.Accept(emailInvite.Token, CancellationToken.None);
            var mismatchView = Assert.IsType<ViewResult>(mismatch);
            Assert.Contains("email", Assert.IsType<string>(mismatchView.Model), StringComparison.OrdinalIgnoreCase);

            var link = await share.CreateLinkInviteAsync(1, "owner", TreeShareRole.Editor, null);
            await share.RevokeInviteAsync(link.Id, "owner");
            // Switch principal to guest for revoked check
            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "guest")], "test"));
            var revoked = await controller.Accept(link.Token, CancellationToken.None);
            var revokedView = Assert.IsType<ViewResult>(revoked);
            Assert.Contains("revoked", Assert.IsType<string>(revokedView.Model), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Accept_already_owner_redirects_home()
    {
        var (controller, db, _, _, current) = await CreateAsync(nameof(Accept_already_owner_redirects_home), "owner");
        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateLinkInviteAsync(1, "owner", TreeShareRole.Editor, null);
            var result = await controller.Accept(invite.Token, CancellationToken.None);
            Assert.IsType<RedirectToActionResult>(result);
            current.Verify(c => c.SetCurrentFamilyTreeIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task ResendInvite_rate_limited_does_not_send()
    {
        var rateLimiter = new Mock<IEmailRateLimiter>();
        rateLimiter.Setup(r => r.TryAcquire(EmailRateLimitOperations.ShareInvite, "guest@example.com", It.IsAny<string?>()))
            .Returns(false);
        var (controller, db, _, email, _) = await CreateAsync(
            nameof(ResendInvite_rate_limited_does_not_send), "owner", rateLimiter: rateLimiter.Object);
        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateEmailInviteAsync(1, "owner", "guest@example.com", TreeShareRole.Readonly, null);
            var result = await controller.ResendInvite(1, invite.Id, CancellationToken.None);
            var model = Assert.IsType<ShareManageViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Contains("Too many emails", model.StatusMessage, StringComparison.OrdinalIgnoreCase);
            email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }
}