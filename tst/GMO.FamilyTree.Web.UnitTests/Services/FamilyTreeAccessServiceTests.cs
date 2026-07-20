using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class FamilyTreeAccessServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    [Fact]
    public async Task GetAccessLevel_returns_owner_for_tree_owner()
    {
        await using var db = CreateDb(nameof(GetAccessLevel_returns_owner_for_tree_owner));
        db.Users.Add(new IdentityUser { Id = "owner", UserName = "o@example.com", Email = "o@example.com" });
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = "owner", Uid = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var sut = new FamilyTreeAccessService(db);
        Assert.Equal(TreeAccessLevel.Owner, await sut.GetAccessLevelAsync("owner", 1));
        Assert.True(await sut.CanManageSharingAsync("owner", 1));
        Assert.True(await sut.CanEditAsync("owner", 1));
        Assert.True(await sut.CanViewAsync("owner", 1));
    }

    [Fact]
    public async Task GetAccessLevel_returns_editor_and_readonly_from_grants()
    {
        await using var db = CreateDb(nameof(GetAccessLevel_returns_editor_and_readonly_from_grants));
        db.Users.AddRange(
            new IdentityUser { Id = "owner", UserName = "o@example.com", Email = "o@example.com" },
            new IdentityUser { Id = "ed", UserName = "e@example.com", Email = "e@example.com" },
            new IdentityUser { Id = "ro", UserName = "r@example.com", Email = "r@example.com" });
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = "owner", Uid = Guid.NewGuid() });
        db.FamilyTreeAccesses.AddRange(
            new FamilyTreeAccess { FamilyTreeId = 1, UserId = "ed", Role = TreeShareRole.Editor, GrantedAt = DateTimeOffset.UtcNow, GrantedByUserId = "owner" },
            new FamilyTreeAccess { FamilyTreeId = 1, UserId = "ro", Role = TreeShareRole.Readonly, GrantedAt = DateTimeOffset.UtcNow, GrantedByUserId = "owner" });
        await db.SaveChangesAsync();

        var sut = new FamilyTreeAccessService(db);
        Assert.Equal(TreeAccessLevel.Editor, await sut.GetAccessLevelAsync("ed", 1));
        Assert.True(await sut.CanEditAsync("ed", 1));
        Assert.False(await sut.CanManageSharingAsync("ed", 1));

        Assert.Equal(TreeAccessLevel.Readonly, await sut.GetAccessLevelAsync("ro", 1));
        Assert.True(await sut.CanViewAsync("ro", 1));
        Assert.False(await sut.CanEditAsync("ro", 1));
    }

    [Fact]
    public async Task GetAccessLevel_returns_none_for_strangers()
    {
        await using var db = CreateDb(nameof(GetAccessLevel_returns_none_for_strangers));
        db.Users.Add(new IdentityUser { Id = "owner", UserName = "o@example.com", Email = "o@example.com" });
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = "owner", Uid = Guid.NewGuid() });
        await db.SaveChangesAsync();

        var sut = new FamilyTreeAccessService(db);
        Assert.Equal(TreeAccessLevel.None, await sut.GetAccessLevelAsync("other", 1));
        Assert.False(await sut.CanViewAsync("other", 1));
    }

    [Fact]
    public async Task GetAccessLevelForMember_returns_none_for_empty_user()
    {
        await using var db = CreateDb(nameof(GetAccessLevelForMember_returns_none_for_empty_user));
        var sut = new FamilyTreeAccessService(db);
        Assert.Equal(TreeAccessLevel.None, await sut.GetAccessLevelForMemberAsync(string.Empty, 1));
    }

    [Fact]
    public async Task GetAccessLevelForMember_returns_none_when_member_missing()
    {
        await using var db = CreateDb(nameof(GetAccessLevelForMember_returns_none_when_member_missing));
        db.Users.Add(new IdentityUser { Id = "owner", UserName = "o@example.com", Email = "o@example.com" });
        await db.SaveChangesAsync();

        var sut = new FamilyTreeAccessService(db);
        Assert.Equal(TreeAccessLevel.None, await sut.GetAccessLevelForMemberAsync("owner", 999));
    }

    [Fact]
    public async Task GetAccessLevelForMember_delegates_to_tree_access()
    {
        await using var db = CreateDb(nameof(GetAccessLevelForMember_delegates_to_tree_access));
        db.Users.Add(new IdentityUser { Id = "owner", UserName = "o@example.com", Email = "o@example.com" });
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "T", OwnerId = "owner", Uid = Guid.NewGuid() });
        db.FamilyMembers.Add(new FamilyMember { Id = 10, FamilyTreeId = 1, Name = "Self" });
        await db.SaveChangesAsync();

        var sut = new FamilyTreeAccessService(db);
        Assert.Equal(TreeAccessLevel.Owner, await sut.GetAccessLevelForMemberAsync("owner", 10));
        Assert.True(await sut.UserOwnsMemberAsync("owner", 10));
    }

    [Fact]
    public async Task GetAccessibleTrees_includes_owned_and_shared()
    {
        await using var db = CreateDb(nameof(GetAccessibleTrees_includes_owned_and_shared));
        db.Users.AddRange(
            new IdentityUser { Id = "u1", UserName = "u1@example.com", Email = "u1@example.com" },
            new IdentityUser { Id = "u2", UserName = "u2@example.com", Email = "u2@example.com" });
        db.FamilyTrees.AddRange(
            new FamilyTreeEntity { Id = 1, Name = "Mine", OwnerId = "u1", Uid = Guid.NewGuid() },
            new FamilyTreeEntity { Id = 2, Name = "Shared", OwnerId = "u2", Uid = Guid.NewGuid() },
            new FamilyTreeEntity { Id = 3, Name = "Other", OwnerId = "u2", Uid = Guid.NewGuid() });
        db.FamilyTreeAccesses.Add(new FamilyTreeAccess
        {
            FamilyTreeId = 2,
            UserId = "u1",
            Role = TreeShareRole.Readonly,
            GrantedAt = DateTimeOffset.UtcNow,
            GrantedByUserId = "u2"
        });
        await db.SaveChangesAsync();

        var sut = new FamilyTreeAccessService(db);
        var trees = await sut.GetAccessibleTreesAsync("u1");
        Assert.Equal(2, trees.Count);
        Assert.Contains(trees, t => t.Name == "Mine");
        Assert.Contains(trees, t => t.Name == "Shared");
    }
}