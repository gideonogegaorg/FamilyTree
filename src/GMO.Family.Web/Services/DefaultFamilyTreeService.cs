using GMO.Family.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.Services;

public sealed class DefaultFamilyTreeService : IDefaultFamilyTreeService
{
    private const string DefaultTreeName = "Default";

    private readonly AppDbContext _db;

    public DefaultFamilyTreeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<long?> EnsureDefaultFamilyTreeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var hasAny = await _db.FamilyTrees.AnyAsync(x => x.OwnerId == userId, cancellationToken);
        if (hasAny) return null;

        var defaultTree = new FamilyTree
        {
            Uid = Guid.NewGuid(),
            Name = DefaultTreeName,
            OwnerId = userId
        };
        _db.FamilyTrees.Add(defaultTree);
        await _db.SaveChangesAsync(cancellationToken);
        return defaultTree.Id;
    }
}