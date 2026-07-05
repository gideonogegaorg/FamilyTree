using GMO.Family.Web.Data;

namespace GMO.Family.Web.Services;

public interface ITreeCardViewModeService
{
    Task<TreeCardViewMode> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(TreeCardViewMode mode, CancellationToken cancellationToken = default);
}