using GMO.Family.Web.Data;

namespace GMO.Family.Web.Services;

public interface ILineageModeService
{
    Task<LineageMode> GetAsync(CancellationToken cancellationToken = default);
    Task SetAsync(LineageMode mode, CancellationToken cancellationToken = default);
}