using GMO.Family.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.Services;

public sealed class FamilyTreeDeletionService : IFamilyTreeDeletionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly IDefaultFamilyTreeService _defaultFamilyTree;

    public FamilyTreeDeletionService(
        AppDbContext db,
        ICurrentFamilyTreeService currentFamilyTree,
        IDefaultFamilyTreeService defaultFamilyTree)
    {
        _db = db;
        _currentFamilyTree = currentFamilyTree;
        _defaultFamilyTree = defaultFamilyTree;
    }

    public async Task<FamilyTreeDeleteResult> DeleteAsync(string ownerId, long treeId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FamilyTrees.FindAsync(new object[] { treeId }, cancellationToken);
        if (entity == null || entity.OwnerId != ownerId)
            return FamilyTreeDeleteResult.NotFound;

        var currentId = await _currentFamilyTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        var wasCurrent = currentId == treeId;

        _db.FamilyTrees.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var hasRemaining = await _db.FamilyTrees.AnyAsync(x => x.OwnerId == ownerId, cancellationToken);
        if (!hasRemaining)
        {
            var defaultId = await _defaultFamilyTree.EnsureDefaultFamilyTreeAsync(ownerId, cancellationToken);
            await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(defaultId, cancellationToken);
        }
        else if (wasCurrent)
        {
            var nextId = await _db.FamilyTrees
                .Where(x => x.OwnerId == ownerId)
                .OrderBy(x => x.Name)
                .Select(x => x.Id)
                .FirstAsync(cancellationToken);
            await _currentFamilyTree.SetCurrentFamilyTreeIdAsync(nextId, cancellationToken);
        }

        return FamilyTreeDeleteResult.Deleted;
    }
}
