using System.Security.Claims;

using GMO.Family.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.Services;

public sealed class CurrentFamilyTreeService : ICurrentFamilyTreeService
{
    private const string SessionKey = "CurrentFamilyTreeId";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public CurrentFamilyTreeService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<long?> GetCurrentFamilyTreeIdAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var value = session?.GetString(SessionKey);
        if (long.TryParse(value, out var sessionId))
            return sessionId;

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return null;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        var id = profile?.CurrentFamilyTreeId;
        if (id.HasValue && session != null)
            session.SetString(SessionKey, id.Value.ToString());
        return id;
    }

    public async Task SetCurrentFamilyTreeIdAsync(long? id, CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        if (session != null)
        {
            if (id.HasValue)
                session.SetString(SessionKey, id.Value.ToString());
            else
                session.Remove(SessionKey);
        }

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId, CurrentFamilyTreeId = id };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.CurrentFamilyTreeId = id;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}