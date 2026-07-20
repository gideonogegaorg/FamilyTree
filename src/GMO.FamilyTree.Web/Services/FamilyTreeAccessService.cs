using GMO.FamilyTree.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Services;

public sealed class FamilyTreeAccessService : IFamilyTreeAccessService
{
    private readonly AppDbContext _db;

    public FamilyTreeAccessService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TreeAccessLevel> GetAccessLevelAsync(string userId, long treeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return TreeAccessLevel.None;

        var tree = await _db.FamilyTrees.AsNoTracking()
            .Where(t => t.Id == treeId)
            .Select(t => new { t.OwnerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (tree == null)
            return TreeAccessLevel.None;
        if (tree.OwnerId == userId)
            return TreeAccessLevel.Owner;

        var role = await _db.FamilyTreeAccesses.AsNoTracking()
            .Where(a => a.FamilyTreeId == treeId && a.UserId == userId)
            .Select(a => (TreeShareRole?)a.Role)
            .FirstOrDefaultAsync(cancellationToken);
        return role switch
        {
            TreeShareRole.Editor => TreeAccessLevel.Editor,
            TreeShareRole.Readonly => TreeAccessLevel.Readonly,
            _ => TreeAccessLevel.None
        };
    }

    public async Task<TreeAccessLevel> GetAccessLevelForMemberAsync(string userId, long memberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return TreeAccessLevel.None;

        var treeId = await _db.FamilyMembers.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => (long?)m.FamilyTreeId)
            .FirstOrDefaultAsync(cancellationToken);
        return treeId is not { } resolvedTreeId
            ? TreeAccessLevel.None
            : await GetAccessLevelAsync(userId, resolvedTreeId, cancellationToken);
    }

    public async Task<bool> CanViewAsync(string userId, long treeId, CancellationToken cancellationToken = default)
        => await GetAccessLevelAsync(userId, treeId, cancellationToken) >= TreeAccessLevel.Readonly;

    public async Task<bool> CanEditAsync(string userId, long treeId, CancellationToken cancellationToken = default)
        => await GetAccessLevelAsync(userId, treeId, cancellationToken) >= TreeAccessLevel.Editor;

    public async Task<bool> CanManageSharingAsync(string userId, long treeId, CancellationToken cancellationToken = default)
        => await GetAccessLevelAsync(userId, treeId, cancellationToken) == TreeAccessLevel.Owner;

    public async Task<bool> CanDeleteTreeAsync(string userId, long treeId, CancellationToken cancellationToken = default)
        => await GetAccessLevelAsync(userId, treeId, cancellationToken) == TreeAccessLevel.Owner;

    public async Task<IReadOnlyList<Data.FamilyTree>> GetAccessibleTreesAsync(string userId, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(userId)
            ? []
            : await _db.FamilyTrees.AsNoTracking()
            .Where(t => t.OwnerId == userId
                || _db.FamilyTreeAccesses.Any(a => a.FamilyTreeId == t.Id && a.UserId == userId))
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UserOwnsTreeAsync(string userId, long treeId, CancellationToken cancellationToken = default)
        => await GetAccessLevelAsync(userId, treeId, cancellationToken) == TreeAccessLevel.Owner;

    public async Task<bool> UserOwnsMemberAsync(string userId, long memberId, CancellationToken cancellationToken = default)
        => await GetAccessLevelForMemberAsync(userId, memberId, cancellationToken) == TreeAccessLevel.Owner;
}