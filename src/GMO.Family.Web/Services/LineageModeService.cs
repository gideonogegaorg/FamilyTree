using System.Security.Claims;

using GMO.Family.Web.Data;

namespace GMO.Family.Web.Services;

public sealed class LineageModeService : ILineageModeService
{
    private const string SessionKey = "LineageMode";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public LineageModeService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<LineageMode> GetAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var value = session?.GetInt32(SessionKey);
        if (value.HasValue && Enum.IsDefined(typeof(LineageMode), value.Value))
            return (LineageMode)value.Value;

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return LineageMode.Paternal;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        var mode = profile?.LineageMode;
        if (mode.HasValue)
        {
            if (session != null)
                session.SetInt32(SessionKey, (int)mode.Value);
            return mode.Value;
        }
        return LineageMode.Paternal;
    }

    public async Task SetAsync(LineageMode mode, CancellationToken cancellationToken = default)
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
            profile = new UserProfile { UserId = userId, LineageMode = mode };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.LineageMode = mode;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}