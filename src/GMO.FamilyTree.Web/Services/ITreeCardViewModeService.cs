using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Services;

public interface ITreeCardViewModeService
{
    Task<TreeCardViewMode> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(TreeCardViewMode mode, CancellationToken cancellationToken = default);
}