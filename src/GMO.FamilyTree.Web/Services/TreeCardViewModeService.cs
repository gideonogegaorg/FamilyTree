using System.Security.Claims;

using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Services;

public sealed class TreeCardViewModeService : ITreeCardViewModeService
{
    private const string SessionKey = "TreeCardViewMode";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public TreeCardViewModeService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<TreeCardViewMode> GetAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var value = session?.GetInt32(SessionKey);
        if (value.HasValue && Enum.IsDefined(typeof(TreeCardViewMode), value.Value))
            return (TreeCardViewMode)value.Value;

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return TreeCardViewMode.Standard;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        var mode = profile?.TreeCardViewMode;
        if (mode.HasValue)
        {
            session?.SetInt32(SessionKey, (int)mode.Value);
            return mode.Value;
        }
        return TreeCardViewMode.Standard;
    }

    public async Task SetAsync(TreeCardViewMode mode, CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.SetInt32(SessionKey, (int)mode);

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId, TreeCardViewMode = mode };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.TreeCardViewMode = mode;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}