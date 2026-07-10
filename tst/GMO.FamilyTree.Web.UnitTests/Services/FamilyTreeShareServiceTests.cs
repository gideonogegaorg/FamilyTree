using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class FamilyTreeShareServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static async Task<(AppDbContext db, FamilyTreeShareService sut, long treeId)> SeedAsync(string name)
    {
        var db = CreateDb(name);
        db.Users.AddRange(
            new IdentityUser { Id = "owner", UserName = "owner@example.com", Email = "owner@example.com" },
            new IdentityUser { Id = "guest", UserName = "guest@example.com", Email = "guest@example.com" },
            new IdentityUser { Id = "other", UserName = "other@example.com", Email = "other@example.com" });
        db.FamilyTrees.Add(new FamilyTreeEntity { Id = 1, Name = "Tree", OwnerId = "owner", Uid = Guid.NewGuid() });
        await db.SaveChangesAsync();
        var sut = new FamilyTreeShareService(db, new FamilyTreeAccessService(db));
        return (db, sut, 1);
    }

    [Fact]
    public async Task Accept_link_invite_grants_access()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Accept_link_invite_grants_access));
        await using (db)
        {
            var invite = await sut.CreateLinkInviteAsync(treeId, "owner", TreeShareRole.Editor, null);
            var (result, acceptedTreeId) = await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com");

            Assert.Equal(InviteAcceptResult.Success, result);
            Assert.Equal(treeId, acceptedTreeId);
            Assert.True(await db.FamilyTreeAccesses.AnyAsync(a => a.UserId == "guest" && a.Role == TreeShareRole.Editor));
        }
    }

    [Fact]
    public async Task Accept_email_invite_requires_matching_email_and_is_single_use()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Accept_email_invite_requires_matching_email_and_is_single_use));
        await using (db)
        {
            var invite = await sut.CreateEmailInviteAsync(treeId, "owner", "guest@example.com", TreeShareRole.Readonly, null);

            var mismatch = await sut.AcceptInviteAsync(invite.Token, "other", "other@example.com");
            Assert.Equal(InviteAcceptResult.EmailMismatch, mismatch.Result);

            var ok = await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com");
            Assert.Equal(InviteAcceptResult.Success, ok.Result);

            var again = await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com");
            Assert.Equal(InviteAcceptResult.NotFound, again.Result);
        }
    }

    [Fact]
    public async Task Revoke_invite_blocks_accept()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Revoke_invite_blocks_accept));
        await using (db)
        {
            var invite = await sut.CreateLinkInviteAsync(treeId, "owner", TreeShareRole.Readonly, null);
            Assert.True(await sut.RevokeInviteAsync(invite.Id, "owner"));

            var result = await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com");
            Assert.Equal(InviteAcceptResult.Revoked, result.Result);
        }
    }

    [Fact]
    public async Task Expired_invite_is_rejected()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Expired_invite_is_rejected));
        await using (db)
        {
            var invite = await sut.CreateLinkInviteAsync(
                treeId, "owner", TreeShareRole.Readonly, DateTimeOffset.UtcNow.AddMinutes(-1));

            var result = await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com");
            Assert.Equal(InviteAcceptResult.Expired, result.Result);
        }
    }

    [Fact]
    public async Task Remove_collaborator_and_change_role()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Remove_collaborator_and_change_role));
        await using (db)
        {
            var invite = await sut.CreateLinkInviteAsync(treeId, "owner", TreeShareRole.Editor, null);
            await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com");

            Assert.True(await sut.ChangeCollaboratorRoleAsync(treeId, "guest", TreeShareRole.Readonly, "owner"));
            var access = await new FamilyTreeAccessService(db).GetAccessLevelAsync("guest", treeId);
            Assert.Equal(TreeAccessLevel.Readonly, access);

            Assert.True(await sut.RemoveCollaboratorAsync(treeId, "guest", "owner"));
            Assert.Equal(TreeAccessLevel.None, await new FamilyTreeAccessService(db).GetAccessLevelAsync("guest", treeId));
        }
    }

    [Fact]
    public async Task Non_owner_cannot_create_or_manage_shares()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Non_owner_cannot_create_or_manage_shares));
        await using (db)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.CreateLinkInviteAsync(treeId, "guest", TreeShareRole.Editor, null));

            db.FamilyTreeAccesses.Add(new FamilyTreeAccess
            {
                FamilyTreeId = treeId,
                UserId = "guest",
                Role = TreeShareRole.Editor,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByUserId = "owner"
            });
            await db.SaveChangesAsync();

            Assert.False(await sut.RemoveCollaboratorAsync(treeId, "guest", "other"));
            Assert.False(await sut.ChangeCollaboratorRoleAsync(treeId, "guest", TreeShareRole.Readonly, "other"));
        }
    }

    [Fact]
    public async Task Link_invite_remains_reusable_until_revoked()
    {
        var (db, sut, treeId) = await SeedAsync(nameof(Link_invite_remains_reusable_until_revoked));
        await using (db)
        {
            db.Users.Add(new IdentityUser { Id = "guest2", UserName = "g2@example.com", Email = "g2@example.com" });
            await db.SaveChangesAsync();

            var invite = await sut.CreateLinkInviteAsync(treeId, "owner", TreeShareRole.Readonly, null);
            Assert.Equal(InviteAcceptResult.Success, (await sut.AcceptInviteAsync(invite.Token, "guest", "guest@example.com")).Result);
            Assert.Equal(InviteAcceptResult.Success, (await sut.AcceptInviteAsync(invite.Token, "guest2", "g2@example.com")).Result);
            Assert.Equal(2, await db.FamilyTreeAccesses.CountAsync(a => a.FamilyTreeId == treeId));
        }
    }
}