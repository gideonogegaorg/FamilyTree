namespace GMO.Family.Web.Services;

/// <summary>
/// Ensures the user has at least one family tree (creates "Default" if none). Call after sign-in.
/// Returns the id of the newly created default tree, or null if the user already had trees.
/// </summary>
public interface IDefaultFamilyTreeService
{
    Task<long?> EnsureDefaultFamilyTreeAsync(string userId, CancellationToken cancellationToken = default);
}