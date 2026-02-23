namespace GMO.Family.Web.Services;

/// <summary>
/// Tracks the current FamilyTree selection for the user. Persisted in database (cross-browser, cross-device); session used as cache.
/// </summary>
public interface ICurrentFamilyTreeService
{
    Task<long?> GetCurrentFamilyTreeIdAsync(CancellationToken cancellationToken = default);
    Task SetCurrentFamilyTreeIdAsync(long? id, CancellationToken cancellationToken = default);
}