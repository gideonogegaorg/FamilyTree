using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.Family.Web.UnitTests.Services;

public class DefaultFamilyTreeServiceTests
{
    private static AppDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task EnsureDefaultFamilyTreeAsync_returns_null_when_userId_is_null()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(EnsureDefaultFamilyTreeAsync_returns_null_when_userId_is_null));
        var sut = new DefaultFamilyTreeService(db);

        // Act
        var result = await sut.EnsureDefaultFamilyTreeAsync(null!);

        // Assert
        Assert.Null(result);
        Assert.Empty(await db.FamilyTrees.ToListAsync());
    }

    [Fact]
    public async Task EnsureDefaultFamilyTreeAsync_returns_null_when_userId_is_empty()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(EnsureDefaultFamilyTreeAsync_returns_null_when_userId_is_empty));
        var sut = new DefaultFamilyTreeService(db);

        // Act
        var result = await sut.EnsureDefaultFamilyTreeAsync(string.Empty);

        // Assert
        Assert.Null(result);
        Assert.Empty(await db.FamilyTrees.ToListAsync());
    }

    [Fact]
    public async Task EnsureDefaultFamilyTreeAsync_creates_default_tree_and_returns_id_when_user_has_no_trees()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(EnsureDefaultFamilyTreeAsync_creates_default_tree_and_returns_id_when_user_has_no_trees));
        var sut = new DefaultFamilyTreeService(db);
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];

        // Act
        var result = await sut.EnsureDefaultFamilyTreeAsync(userId);

        // Assert
        Assert.NotNull(result);
        var tree = await db.FamilyTrees.SingleOrDefaultAsync(x => x.OwnerId == userId);
        Assert.NotNull(tree);
        Assert.Equal("Default", tree.Name);
        Assert.Equal(userId, tree.OwnerId);
        Assert.Equal(tree.Id, result.Value);
    }

    [Fact]
    public async Task EnsureDefaultFamilyTreeAsync_returns_null_when_user_already_has_trees()
    {
        // Arrange
        await using var db = CreateDbContext(nameof(EnsureDefaultFamilyTreeAsync_returns_null_when_user_already_has_trees));
        var userId = "user-" + Guid.NewGuid().ToString("N")[..8];
        db.FamilyTrees.Add(new FamilyTree { Uid = Guid.NewGuid(), Name = "Existing", OwnerId = userId });
        await db.SaveChangesAsync();
        var countBefore = await db.FamilyTrees.CountAsync(x => x.OwnerId == userId);
        var sut = new DefaultFamilyTreeService(db);

        // Act
        var result = await sut.EnsureDefaultFamilyTreeAsync(userId);

        // Assert
        Assert.Null(result);
        var countAfter = await db.FamilyTrees.CountAsync(x => x.OwnerId == userId);
        Assert.Equal(countBefore, countAfter);
    }
}