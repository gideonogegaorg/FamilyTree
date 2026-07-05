using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Services;

public interface ITreeViewOrientationService
{
    Task<TreeViewOrientation> GetOrientationAsync(CancellationToken cancellationToken = default);
    Task SetOrientationAsync(TreeViewOrientation orientation, CancellationToken cancellationToken = default);
}