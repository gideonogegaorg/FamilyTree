using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.UnitTests.Fixtures;
using GMO.FamilyTree.Web.ViewComponents;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.ViewComponents;

public class UserMenuViewComponentTests : IClassFixture<AccountControllerFixture>
{
    private readonly AccountControllerFixture _f;

    public UserMenuViewComponentTests(AccountControllerFixture fixture) => _f = fixture;

    [Fact]
    public async Task InvokeAsync_returns_empty_model_when_not_authenticated()
    {
        await using var db = _f.CreateDb(nameof(InvokeAsync_returns_empty_model_when_not_authenticated));
        var users = AccountControllerFixture.CreateIdentityManagers(db).Item2;
        var component = new UserMenuViewComponent(db, users);
        ViewComponentTestHelper.AttachContext(component);

        var result = await component.InvokeAsync();

        var model = GetModel(result);
        Assert.Equal(string.Empty, model.Email);
        Assert.Null(model.PhotoUrl);
        Assert.False(model.HasPassword);
    }

    [Fact]
    public async Task InvokeAsync_returns_email_and_photo_from_profile()
    {
        await using var db = _f.CreateDb(nameof(InvokeAsync_returns_email_and_photo_from_profile));
        var users = AccountControllerFixture.CreateIdentityManagers(db).Item2;
        db.UserProfiles.Add(new UserProfile
        {
            UserId = "user-1",
            PhotoKey = "profiles/user-1.jpg"
        });
        await db.SaveChangesAsync();

        var component = new UserMenuViewComponent(db, users);
        ViewComponentTestHelper.AttachContext(
            component,
            ViewComponentTestHelper.AuthenticatedUser("user-1", "test@example.com"));

        var model = GetModel(await component.InvokeAsync());

        Assert.Equal("test@example.com", model.Email);
        Assert.Equal("/photos/profiles/me", model.PhotoUrl);
    }

    [Fact]
    public async Task InvokeAsync_returns_email_without_photo_when_profile_missing()
    {
        await using var db = _f.CreateDb(nameof(InvokeAsync_returns_email_without_photo_when_profile_missing));
        var users = AccountControllerFixture.CreateIdentityManagers(db).Item2;
        var component = new UserMenuViewComponent(db, users);
        ViewComponentTestHelper.AttachContext(
            component,
            ViewComponentTestHelper.AuthenticatedUser("user-1", "solo@example.com"));

        var model = GetModel(await component.InvokeAsync());

        Assert.Equal("solo@example.com", model.Email);
        Assert.Null(model.PhotoUrl);
    }

    [Fact]
    public async Task InvokeAsync_sets_HasPassword_for_password_user()
    {
        await using var db = _f.CreateDb(nameof(InvokeAsync_sets_HasPassword_for_password_user));
        var users = AccountControllerFixture.CreateIdentityManagers(db).Item2;
        var user = new IdentityUser { UserName = "pwd@example.com", Email = "pwd@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "TestPassword1!")).Succeeded);

        var component = new UserMenuViewComponent(db, users);
        ViewComponentTestHelper.AttachContext(
            component,
            ViewComponentTestHelper.AuthenticatedUser(user.Id!, "pwd@example.com"));

        var model = GetModel(await component.InvokeAsync());

        Assert.True(model.HasPassword);
    }

    private static UserMenuViewModel GetModel(IViewComponentResult result)
    {
        var view = Assert.IsType<ViewViewComponentResult>(result);
        return Assert.IsType<UserMenuViewModel>(view.ViewData!.Model);
    }
}