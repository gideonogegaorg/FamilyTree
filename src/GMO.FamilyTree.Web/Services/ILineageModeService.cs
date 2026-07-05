using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Services;

public interface ILineageModeService
{
    Task<LineageMode> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(LineageMode mode, CancellationToken cancellationToken = default);
}