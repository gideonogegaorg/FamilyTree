using GMO.Family.Web.Data;

namespace GMO.Family.Web.Services;

public interface ITreeViewOrientationService
{
    Task<TreeViewOrientation> GetOrientationAsync(CancellationToken cancellationToken = default);
    Task SetOrientationAsync(TreeViewOrientation orientation, CancellationToken cancellationToken = default);
}