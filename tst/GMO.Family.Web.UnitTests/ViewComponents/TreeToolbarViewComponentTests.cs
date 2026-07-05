using GMO.Family.Web.Data;
using GMO.Family.Web.Services;
using GMO.Family.Web.ViewComponents;

using GMO.Family.Web.UnitTests.Fixtures;
using GMO.Family.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.EntityFrameworkCore;

using Moq;

using Xunit;

namespace GMO.Family.Web.UnitTests.ViewComponents;

public class TreeToolbarViewComponentTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task InvokeAsync_returns_empty_content_when_not_authenticated()
    {
        await using var db = CreateDb(nameof(InvokeAsync_returns_empty_content_when_not_authenticated));
        var component = CreateComponent(db, authenticated: false);

        var result = await component.InvokeAsync();

        var content = Assert.IsType<ContentViewComponentResult>(result);
        Assert.Equal(string.Empty, content.Content);
    }

    [Fact]
    public async Task InvokeAsync_lists_user_trees_and_passes_focus_member()
    {
        await using var db = CreateDb(nameof(InvokeAsync_lists_user_trees_and_passes_focus_member));
        db.FamilyTrees.Add(new FamilyTree { Uid = Guid.NewGuid(), Name = "Only", OwnerId = "user-1" });
        await db.SaveChangesAsync();
        var component = CreateComponent(db, currentTreeId: null);

        var model = GetModel(await component.InvokeAsync(focusMemberId: 42, hasMembers: true));

        Assert.Equal(42, model.FocusMemberId);
        Assert.True(model.HasMembers);
        Assert.Single(model.FamilyTrees);
    }

    [Fact]
    public async Task InvokeAsync_lists_multiple_trees_for_picker()
    {
        await using var db = CreateDb(nameof(InvokeAsync_lists_multiple_trees_for_picker));
        db.FamilyTrees.AddRange(
            new FamilyTree { Uid = Guid.NewGuid(), Name = "A", OwnerId = "user-1" },
            new FamilyTree { Uid = Guid.NewGuid(), Name = "B", OwnerId = "user-1" });
        await db.SaveChangesAsync();
        var component = CreateComponent(db);

        var model = GetModel(await component.InvokeAsync());

        Assert.Equal(2, model.FamilyTrees.Count);
    }

    [Fact]
    public async Task InvokeAsync_uses_current_tree_name_and_orientation_settings()
    {
        await using var db = CreateDb(nameof(InvokeAsync_uses_current_tree_name_and_orientation_settings));
        var tree = new FamilyTree { Uid = Guid.NewGuid(), Name = "My Tree", OwnerId = "user-1" };
        db.FamilyTrees.Add(tree);
        await db.SaveChangesAsync();

        var current = new CurrentFamilyTreeServiceMock();
        current.ReturnsCurrentTreeId(tree.Id);
        var orientation = new Mock<ITreeViewOrientationService>();
        orientation.Setup(s => s.GetOrientationAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TreeViewOrientation.Vertical);
        var lineage = new Mock<ILineageModeService>();
        lineage.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(LineageMode.Maternal);

        var component = new TreeToolbarViewComponent(db, current.Object, orientation.Object, lineage.Object);
        ViewComponentTestHelper.AttachContext(component, ViewComponentTestHelper.AuthenticatedUser("user-1"));

        var model = GetModel(await component.InvokeAsync(hasMembers: false));

        Assert.Equal(tree.Id, model.CurrentTreeId);
        Assert.Equal("My Tree", model.CurrentTreeName);
        Assert.Equal(TreeViewOrientation.Vertical, model.TreeViewOrientation);
        Assert.Equal(LineageMode.Maternal, model.LineageMode);
        Assert.False(model.HasMembers);
    }

    private static TreeToolbarViewComponent CreateComponent(AppDbContext db, long? currentTreeId = null, bool authenticated = true)
    {
        var current = new CurrentFamilyTreeServiceMock();
        if (currentTreeId.HasValue)
            current.ReturnsCurrentTreeId(currentTreeId.Value);

        var orientation = new Mock<ITreeViewOrientationService>();
        orientation.Setup(s => s.GetOrientationAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TreeViewOrientation.Horizontal);
        var lineage = new Mock<ILineageModeService>();
        lineage.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(LineageMode.Paternal);

        var component = new TreeToolbarViewComponent(db, current.Object, orientation.Object, lineage.Object);
        ViewComponentTestHelper.AttachContext(
            component,
            authenticated ? ViewComponentTestHelper.AuthenticatedUser("user-1") : null);
        return component;
    }

    private static TreeToolbarViewModel GetModel(IViewComponentResult result)
    {
        var view = Assert.IsType<ViewViewComponentResult>(result);
        return Assert.IsType<TreeToolbarViewModel>(view.ViewData!.Model);
    }
}
