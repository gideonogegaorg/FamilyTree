using System.Security.Claims;

using GMO.Family.Web.Data;

namespace GMO.Family.Web.Services;

public sealed class TreePathModeService : ITreePathModeService
{
    private const string SessionKey = "TreePathMode";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public TreePathModeService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<TreePathMode> GetAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var value = session?.GetInt32(SessionKey);
        if (value.HasValue && Enum.IsDefined(typeof(TreePathMode), value.Value))
            return (TreePathMode)value.Value;

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return TreePathMode.Paternal;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        var mode = profile?.TreePathMode;
        if (mode.HasValue)
        {
            if (session != null)
                session.SetInt32(SessionKey, (int)mode.Value);
            return mode.Value;
        }
        return TreePathMode.Paternal;
    }

    public async Task SetAsync(TreePathMode mode, CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
            session.SetInt32(SessionKey, (int)mode);

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId, TreePathMode = mode };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.TreePathMode = mode;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}