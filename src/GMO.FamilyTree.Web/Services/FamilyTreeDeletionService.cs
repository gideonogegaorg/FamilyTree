using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services.Photos;

using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Services;

public sealed class FamilyTreeDeletionService : IFamilyTreeDeletionService
{
    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly IDefaultFamilyTreeService _defaultFamilyTree;
    private readonly IPhotoStorageService _photos;

    public FamilyTreeDeletionService(
        AppDbContext db,
        ICurrentFamilyTreeService currentFamilyTree,
        IDefaultFamilyTreeService defaultFamilyTree,
        IPhotoStorageService photos)
    {
        _db = db;
        _currentFamilyTree = currentFamilyTree;
        _defaultFamilyTree = defaultFamilyTree;
        _photos = photos;
    }

    public async Task<FamilyTreeDeleteResult> DeleteAsync(string ownerId, long treeId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.FamilyTrees.FindAsync(new object[] { treeId }, cancellationToken);
        if (entity == null || entity.OwnerId != ownerId)
            return FamilyTreeDeleteResult.NotFound;

        var currentId = await _currentFamilyTree.GetCurrentFamilyTreeIdAsync(cancellationToken);
        var wasCurrent = currentId == treeId;

        var memberPhotoKeys = await _db.FamilyMembers
            .Where(m => m.FamilyTreeId == treeId && m.PhotoKey != null)
            .Select(m => m.PhotoKey!)
            .ToListAsync(cancellationToken);

        _db.FamilyTrees.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await PhotoStorageHelper.DeleteManyAsync(_photos, memberPhotoKeys, cancellationToken);

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