using System.Security.Claims;
using System.Text.Json;

using GMO.FamilyTree.Web.Controllers;
using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Models;
using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;
using GMO.FamilyTree.Web.Services.Photos;
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

public class ControllerModelStateValidationTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _fixture;

    public ControllerModelStateValidationTests(AccountControllerFixture fixture) => _fixture = fixture;

    private static void Invalidate(Controller controller) =>
        controller.ModelState.AddModelError(string.Empty, "invalid");

    [Fact]
    public async Task AccountController_UploadPhoto_returns_error_when_model_state_invalid()
    {
        await using var db = _fixture.CreateDb(nameof(AccountController_UploadPhoto_returns_error_when_model_state_invalid));
        var (signInManager, userManager) = _fixture.CreateIdentityManagers(db);
        var controller = _fixture.CreateAccountController(
            signInManager, userManager, db,
            _fixture.CreateExternalLoginInfoProvider("user@example.com"),
            userId: "user-1");
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        Invalidate(controller);

        var result = await controller.UploadPhoto(null, CancellationToken.None);

        var json = Assert.IsType<JsonResult>(result);
        Assert.Contains("Invalid input", JsonSerializer.Serialize(json.Value));
    }

    [Theory]
    [InlineData(nameof(AccountController.SetTreeCardViewMode))]
    [InlineData(nameof(AccountController.SwitchFamilyTree))]
    [InlineData(nameof(AccountController.SetTreeViewOrientation))]
    [InlineData(nameof(AccountController.DeleteFamilyTree))]
    [InlineData(nameof(AccountController.SetLineageMode))]
    public async Task AccountController_post_actions_return_bad_request_when_model_state_invalid(string _)
    {
        await using var db = _fixture.CreateDb(nameof(AccountController_post_actions_return_bad_request_when_model_state_invalid) + _);
        var user = new IdentityUser { Id = "user-1", UserName = "user@example.com", Email = "user@example.com" };
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "Tree", OwnerId = "user-1" });
        await db.SaveChangesAsync();

        var (signInManager, userManager) = _fixture.CreateIdentityManagers(db, user);
        var controller = _fixture.CreateAccountController(
            signInManager, userManager, db,
            _fixture.CreateExternalLoginInfoProvider("user@example.com"),
            userId: "user-1");
        Invalidate(controller);

        IActionResult result = _ switch
        {
            nameof(AccountController.SetTreeCardViewMode) => await controller.SetTreeCardViewMode(0, CancellationToken.None),
            nameof(AccountController.SwitchFamilyTree) => await controller.SwitchFamilyTree(1, CancellationToken.None),
            nameof(AccountController.SetTreeViewOrientation) => await controller.SetTreeViewOrientation(0, CancellationToken.None),
            nameof(AccountController.DeleteFamilyTree) => await controller.DeleteFamilyTree(1, CancellationToken.None),
            nameof(AccountController.SetLineageMode) => await controller.SetLineageMode(0, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(_))
        };

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task AccountController_LoginWith2fa_returns_bad_request_when_model_state_invalid()
    {
        await using var db = _fixture.CreateDb(nameof(AccountController_LoginWith2fa_returns_bad_request_when_model_state_invalid));
        var (signInManager, userManager) = _fixture.CreateIdentityManagers(db);
        var controller = _fixture.CreateAccountController(
            signInManager, userManager, db,
            _fixture.CreateExternalLoginInfoProvider("user@example.com"));
        Invalidate(controller);

        var result = await controller.LoginWith2fa(false, null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task ShareController_Manage_returns_bad_request_when_model_state_invalid()
    {
        var (controller, db, _, _, _) = await CreateShareControllerAsync(nameof(ShareController_Manage_returns_bad_request_when_model_state_invalid));
        await using (db)
        {
            Invalidate(controller);
            Assert.IsType<BadRequestResult>(await controller.Manage(1, CancellationToken.None));
        }
    }

    [Fact]
    public async Task ShareController_CreateLinkInvite_returns_manage_view_when_model_state_invalid()
    {
        var (controller, db, _, _, _) = await CreateShareControllerAsync(nameof(ShareController_CreateLinkInvite_returns_manage_view_when_model_state_invalid));
        await using (db)
        {
            Invalidate(controller);
            var input = new CreateLinkInviteInput { Role = TreeShareRole.Editor };
            var result = await controller.CreateLinkInvite(1, input, CancellationToken.None);
            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Manage", view.ViewName);
            var model = Assert.IsType<ShareManageViewModel>(view.Model);
            Assert.Equal(TreeShareRole.Editor, model.CreateLink.Role);
        }
    }

    [Fact]
    public async Task ShareController_CreateEmailInvite_returns_manage_view_when_model_state_invalid()
    {
        var (controller, db, _, email, _) = await CreateShareControllerAsync(nameof(ShareController_CreateEmailInvite_returns_manage_view_when_model_state_invalid));
        await using (db)
        {
            Invalidate(controller);
            var input = new CreateEmailInviteInput { Email = "guest@example.com", Role = TreeShareRole.Readonly };
            var result = await controller.CreateEmailInvite(1, input, CancellationToken.None);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Equal("Manage", view.ViewName);
            Assert.Equal("guest@example.com", Assert.IsType<ShareManageViewModel>(view.Model).CreateEmail.Email);
            email.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    [Fact]
    public async Task ShareController_post_actions_return_bad_request_when_model_state_invalid()
    {
        var (controller, db, _, _, _) = await CreateShareControllerAsync(nameof(ShareController_post_actions_return_bad_request_when_model_state_invalid));
        await using (db)
        {
            var share = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
            var invite = await share.CreateEmailInviteAsync(1, "owner", "guest@example.com", TreeShareRole.Readonly, null);
            db.FamilyTreeAccesses.Add(new FamilyTreeAccess
            {
                FamilyTreeId = 1,
                UserId = "guest",
                Role = TreeShareRole.Readonly,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByUserId = "owner"
            });
            await db.SaveChangesAsync();

            Invalidate(controller);
            Assert.IsType<BadRequestResult>(await controller.RevokeInvite(1, invite.Id, CancellationToken.None));

            Invalidate(controller);
            Assert.IsType<BadRequestResult>(await controller.ResendInvite(1, invite.Id, CancellationToken.None));

            Invalidate(controller);
            Assert.IsType<BadRequestResult>(await controller.RemoveCollaborator(1, "guest", CancellationToken.None));

            Invalidate(controller);
            Assert.IsType<BadRequestResult>(await controller.ChangeRole(1, "guest", TreeShareRole.Editor, CancellationToken.None));
        }
    }

    [Fact]
    public async Task FamilyMemberController_edit_and_upload_return_error_when_model_state_invalid()
    {
        await using var db = CreateDb(nameof(FamilyMemberController_edit_and_upload_return_error_when_model_state_invalid));
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = "owner-1" });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice" });
        await db.SaveChangesAsync();

        var controller = CreateFamilyMemberController(db);
        Invalidate(controller);

        var edit = await controller.EditMember(10, "Alice", null, null, null, null, true, false);
        var editJson = Assert.IsType<JsonResult>(edit);
        Assert.Contains("\"success\":false", JsonSerializer.Serialize(editJson.Value));

        Invalidate(controller);
        var upload = await controller.UploadMemberPhoto(10, null);
        var uploadJson = Assert.IsType<JsonResult>(upload);
        Assert.Contains("Invalid input", JsonSerializer.Serialize(uploadJson.Value));
    }

    [Fact]
    public async Task FamilyMemberController_LinkExisting_returns_view_when_model_state_invalid()
    {
        await using var db = CreateDb(nameof(FamilyMemberController_LinkExisting_returns_view_when_model_state_invalid));
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = "owner-1" });
        db.FamilyMembers.AddRange(
            new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Alice" },
            new FamilyMember { Id = 11, FamilyTreeId = 1, Name = "Bob" });
        await db.SaveChangesAsync();

        var controller = CreateFamilyMemberController(db);
        var model = new LinkExistingViewModel
        {
            ContextMemberId = 10,
            FamilyTreeId = 1,
            RelationshipType = RelationshipType.Couple
        };
        Invalidate(controller);

        var result = await controller.LinkExisting(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.NotEmpty(model.Candidates);
    }

    [Fact]
    public async Task PhotosController_MemberPhoto_returns_bad_request_when_model_state_invalid()
    {
        await using var db = CreateDb(nameof(PhotosController_MemberPhoto_returns_bad_request_when_model_state_invalid));
        var photos = new Mock<IPhotoStorageService>();
        var controller = new PhotosController(
            db,
            photos.Object,
            new FamilyTreeAccessService(db),
            new WebHostEnvironmentMock().Object,
            Microsoft.Extensions.Options.Options.Create(new PathsOptions()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner-1")], "test"))
            }
        };
        Invalidate(controller);

        Assert.IsType<BadRequestResult>(await controller.MemberPhoto(1, CancellationToken.None));
    }

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static FamilyMemberController CreateFamilyMemberController(AppDbContext db)
    {
        var currentTree = new CurrentFamilyTreeServiceMock();
        currentTree.ReturnsCurrentTreeId(1);
        var controller = new FamilyMemberController(
            db,
            currentTree.Object,
            new Mock<IPhotoStorageService>().Object,
            new FamilyTreeAccessService(db));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner-1")], "test"))
            }
        };
        return controller;
    }

    private static async Task<(ShareController Controller, AppDbContext Db, UserManager<IdentityUser> Users, Mock<IEmailSender> Email, Mock<ICurrentFamilyTreeService> Current)> CreateShareControllerAsync(string dbName)
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
        var email = new Mock<IEmailSender>();
        var controller = new ShareController(
            db, userManager, access, share, current.Object, email.Object,
            AccountControllerFixture.CreateAllowAllRateLimiter(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ShareController>.Instance);
        var http = new DefaultHttpContext { Request = { Scheme = "https" } };
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "owner")], "test"));
        var url = new Mock<IUrlHelper>();
        url.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/Share/Accept/token");
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.Url = url.Object;
        return (controller, db, userManager, email, current);
    }
}