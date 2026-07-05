using GMO.Family.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.Services;

public sealed class FamilyTreeAccessService : IFamilyTreeAccessService
{
    private readonly AppDbContext _db;

    public FamilyTreeAccessService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<bool> UserOwnsTreeAsync(string userId, long treeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return false;
        return await _db.FamilyTrees.AnyAsync(t => t.Id == treeId && t.OwnerId == userId, cancellationToken);
    }

    public async Task<bool> UserOwnsMemberAsync(string userId, long memberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
            return false;
        return await _db.FamilyMembers
            .AsNoTracking()
            .AnyAsync(m => m.Id == memberId && m.FamilyTree.OwnerId == userId, cancellationToken);
    }
}