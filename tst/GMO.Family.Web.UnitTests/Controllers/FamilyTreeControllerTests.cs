using GMO.Family.Web.Controllers;
using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using GMO.Family.Web.UnitTests.Fixtures;
using GMO.Family.Web.UnitTests.Mocks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Moq;

using Xunit;

namespace GMO.Family.Web.UnitTests.Controllers;

public class FamilyTreeControllerTests
{
    private readonly FamilyTreeControllerFixture _fixture = new();

    [Fact]
    public async Task Create_POST_valid_sets_current_tree_and_redirects_to_Home()
    {
        await using var db = _fixture.CreateDb(nameof(Create_POST_valid_sets_current_tree_and_redirects_to_Home));
        var currentTree = new CurrentFamilyTreeServiceMock();
        var (controller, _, _) = _fixture.CreateController(db, currentTree: currentTree);
        var model = new FamilyTree { Name = "New Tree", Uid = Guid.NewGuid() };

        var result = await controller.Create(model, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
        var saved = await db.FamilyTrees.SingleAsync();
        Assert.Equal("New Tree", saved.Name);
        currentTree.Verify(s => s.SetCurrentFamilyTreeIdAsync(saved.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_POST_empty_name_returns_view_with_validation_error()
    {
        await using var db = _fixture.CreateDb(nameof(Create_POST_empty_name_returns_view_with_validation_error));
        var (controller, _, _) = _fixture.CreateController(db);
        var model = new FamilyTree { Name = "   ", Uid = Guid.NewGuid() };

        var result = await controller.Create(model, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(await db.FamilyTrees.ToListAsync());
    }

    [Fact]
    public async Task Edit_POST_valid_redirects_to_Home_and_updates_name()
    {
        await using var db = _fixture.CreateDb(nameof(Edit_POST_valid_redirects_to_Home_and_updates_name));
        db.FamilyTrees.Add(new FamilyTree { Uid = Guid.NewGuid(), Name = "Old", OwnerId = "owner-1" });
        await db.SaveChangesAsync();
        var entity = await db.FamilyTrees.SingleAsync();
        var (controller, _, _) = _fixture.CreateController(db);

        var result = await controller.Edit(entity.Id, new FamilyTree { Id = entity.Id, Uid = entity.Uid, Name = "New" }, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("New", (await db.FamilyTrees.FindAsync(entity.Id))!.Name);
    }

    [Fact]
    public async Task Edit_POST_id_mismatch_returns_NotFound()
    {
        await using var db = _fixture.CreateDb(nameof(Edit_POST_id_mismatch_returns_NotFound));
        var (controller, _, _) = _fixture.CreateController(db);

        var result = await controller.Edit(1, new FamilyTree { Id = 2, Name = "X", Uid = Guid.NewGuid() }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteConfirmed_delegates_to_service_and_redirects_Home()
    {
        await using var db = _fixture.CreateDb(nameof(DeleteConfirmed_delegates_to_service_and_redirects_Home));
        var deletion = new Mock<IFamilyTreeDeletionService>();
        deletion.Setup(s => s.DeleteAsync("owner-1", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyTreeDeleteResult.Deleted);
        var (controller, _, _) = _fixture.CreateController(db, deletion: deletion);

        var result = await controller.DeleteConfirmed(5, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
        deletion.Verify(s => s.DeleteAsync("owner-1", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteConfirmed_returns_NotFound_when_service_reports_not_found()
    {
        await using var db = _fixture.CreateDb(nameof(DeleteConfirmed_returns_NotFound_when_service_reports_not_found));
        var deletion = new Mock<IFamilyTreeDeletionService>();
        deletion.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FamilyTreeDeleteResult.NotFound);
        var (controller, _, _) = _fixture.CreateController(db, deletion: deletion);

        var result = await controller.DeleteConfirmed(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
