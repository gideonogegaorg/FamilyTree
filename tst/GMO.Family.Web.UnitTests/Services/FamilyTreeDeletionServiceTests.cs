using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using GMO.Family.Web.UnitTests.Mocks;

using Microsoft.EntityFrameworkCore;

using Moq;

using Xunit;

namespace GMO.Family.Web.UnitTests.Services;

public class FamilyTreeDeletionServiceTests
{
    private const string OwnerId = "owner-1";
    private const string OtherOwnerId = "owner-2";

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static FamilyTreeDeletionService CreateSut(AppDbContext db, CurrentFamilyTreeServiceMock? current = null) =>
        new(db, (current ?? new CurrentFamilyTreeServiceMock()).Object, new DefaultFamilyTreeService(db));

    private static async Task<FamilyTree> SeedTreeAsync(AppDbContext db, string ownerId, string name)
    {
        var tree = new FamilyTree { Uid = Guid.NewGuid(), Name = name, OwnerId = ownerId };
        db.FamilyTrees.Add(tree);
        await db.SaveChangesAsync();
        return tree;
    }

    [Fact]
    public async Task DeleteAsync_returns_NotFound_when_tree_missing()
    {
        await using var db = CreateDb(nameof(DeleteAsync_returns_NotFound_when_tree_missing));
        var sut = CreateSut(db);

        var result = await sut.DeleteAsync(OwnerId, 999);

        Assert.Equal(FamilyTreeDeleteResult.NotFound, result);
    }

    [Fact]
    public async Task DeleteAsync_returns_NotFound_when_wrong_owner()
    {
        await using var db = CreateDb(nameof(DeleteAsync_returns_NotFound_when_wrong_owner));
        var tree = await SeedTreeAsync(db, OwnerId, "Mine");
        var sut = CreateSut(db);

        var result = await sut.DeleteAsync(OtherOwnerId, tree.Id);

        Assert.Equal(FamilyTreeDeleteResult.NotFound, result);
        Assert.NotNull(await db.FamilyTrees.FindAsync(tree.Id));
    }

    [Fact]
    public async Task DeleteAsync_creates_default_tree_when_deleting_last_tree()
    {
        await using var db = CreateDb(nameof(DeleteAsync_creates_default_tree_when_deleting_last_tree));
        var tree = await SeedTreeAsync(db, OwnerId, "Only");
        var current = new CurrentFamilyTreeServiceMock();
        current.ReturnsCurrentTreeId(tree.Id);
        var sut = CreateSut(db, current);

        var result = await sut.DeleteAsync(OwnerId, tree.Id);

        Assert.Equal(FamilyTreeDeleteResult.Deleted, result);
        Assert.Null(await db.FamilyTrees.FindAsync(tree.Id));
        var replacement = await db.FamilyTrees.SingleAsync(x => x.OwnerId == OwnerId);
        Assert.Equal("Default", replacement.Name);
        Assert.Empty(await db.FamilyMembers.Where(m => m.FamilyTreeId == replacement.Id).ToListAsync());
        current.Verify(s => s.SetCurrentFamilyTreeIdAsync(replacement.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_reassigns_current_to_next_tree_by_name_when_was_current()
    {
        await using var db = CreateDb(nameof(DeleteAsync_reassigns_current_to_next_tree_by_name_when_was_current));
        var alpha = await SeedTreeAsync(db, OwnerId, "Alpha");
        var beta = await SeedTreeAsync(db, OwnerId, "Beta");
        var current = new CurrentFamilyTreeServiceMock();
        current.ReturnsCurrentTreeId(beta.Id);
        var sut = CreateSut(db, current);

        var result = await sut.DeleteAsync(OwnerId, beta.Id);

        Assert.Equal(FamilyTreeDeleteResult.Deleted, result);
        current.Verify(s => s.SetCurrentFamilyTreeIdAsync(alpha.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_does_not_change_current_when_deleting_non_current_tree()
    {
        await using var db = CreateDb(nameof(DeleteAsync_does_not_change_current_when_deleting_non_current_tree));
        var keep = await SeedTreeAsync(db, OwnerId, "Keep");
        var remove = await SeedTreeAsync(db, OwnerId, "Remove");
        var current = new CurrentFamilyTreeServiceMock();
        current.ReturnsCurrentTreeId(keep.Id);
        var sut = CreateSut(db, current);

        var result = await sut.DeleteAsync(OwnerId, remove.Id);

        Assert.Equal(FamilyTreeDeleteResult.Deleted, result);
        current.Verify(s => s.SetCurrentFamilyTreeIdAsync(It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
