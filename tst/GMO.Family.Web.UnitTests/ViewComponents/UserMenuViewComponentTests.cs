using GMO.Family.Web.Data;
using GMO.Family.Web.ViewComponents;

using GMO.Family.Web.UnitTests.Fixtures;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.Family.Web.UnitTests.ViewComponents;

public class UserMenuViewComponentTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task InvokeAsync_returns_empty_model_when_not_authenticated()
    {
        await using var db = CreateDb(nameof(InvokeAsync_returns_empty_model_when_not_authenticated));
        var component = new UserMenuViewComponent(db);
        ViewComponentTestHelper.AttachContext(component);

        var result = await component.InvokeAsync();

        var model = GetModel(result);
        Assert.Equal(string.Empty, model.Email);
        Assert.Null(model.PhotoUrl);
    }

    [Fact]
    public async Task InvokeAsync_returns_email_and_photo_from_profile()
    {
        await using var db = CreateDb(nameof(InvokeAsync_returns_email_and_photo_from_profile));
        db.UserProfiles.Add(new UserProfile
        {
            UserId = "user-1",
            PhotoUrl = "/photos/me.jpg"
        });
        await db.SaveChangesAsync();

        var component = new UserMenuViewComponent(db);
        ViewComponentTestHelper.AttachContext(
            component,
            ViewComponentTestHelper.AuthenticatedUser("user-1", "test@example.com"));

        var model = GetModel(await component.InvokeAsync());

        Assert.Equal("test@example.com", model.Email);
        Assert.Equal("/photos/me.jpg", model.PhotoUrl);
    }

    [Fact]
    public async Task InvokeAsync_returns_email_without_photo_when_profile_missing()
    {
        await using var db = CreateDb(nameof(InvokeAsync_returns_email_without_photo_when_profile_missing));
        var component = new UserMenuViewComponent(db);
        ViewComponentTestHelper.AttachContext(
            component,
            ViewComponentTestHelper.AuthenticatedUser("user-1", "solo@example.com"));

        var model = GetModel(await component.InvokeAsync());

        Assert.Equal("solo@example.com", model.Email);
        Assert.Null(model.PhotoUrl);
    }

    private static UserMenuViewModel GetModel(IViewComponentResult result)
    {
        var view = Assert.IsType<ViewViewComponentResult>(result);
        return Assert.IsType<UserMenuViewModel>(view.ViewData!.Model);
    }
}
