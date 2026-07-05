namespace GMO.Family.Web.Services;

public enum FamilyTreeDeleteResult
{
    NotFound,
    Deleted
}

public interface IFamilyTreeDeletionService
{
    /// <summary>
    /// Deletes a tree owned by <paramref name="ownerId"/> and reassigns the current tree when needed.
    /// When the last tree is deleted, creates a new empty "Default" tree and selects it.
    /// </summary>
    Task<FamilyTreeDeleteResult> DeleteAsync(string ownerId, long treeId, CancellationToken cancellationToken = default);
}