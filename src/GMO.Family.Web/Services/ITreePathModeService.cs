using GMO.Family.Web.Data;

namespace GMO.Family.Web.Services;

public interface ITreePathModeService
{
    Task<TreePathMode> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(TreePathMode mode, CancellationToken cancellationToken = default);
}
