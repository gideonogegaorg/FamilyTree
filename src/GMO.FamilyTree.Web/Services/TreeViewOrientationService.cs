using System.Security.Claims;

using GMO.FamilyTree.Web.Data;

using Microsoft.EntityFrameworkCore;

namespace GMO.FamilyTree.Web.Services;

public sealed class TreeViewOrientationService : ITreeViewOrientationService
{
    private const string SessionKey = "TreeViewOrientation";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    public TreeViewOrientationService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public async Task<TreeViewOrientation> GetOrientationAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        var value = session?.GetInt32(SessionKey);
        if (value.HasValue && Enum.IsDefined(typeof(TreeViewOrientation), value.Value))
            return (TreeViewOrientation)value.Value;

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return TreeViewOrientation.Horizontal;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        var orientation = profile?.TreeViewOrientation;
        if (orientation.HasValue)
        {
            session?.SetInt32(SessionKey, (int)orientation.Value);
            return orientation.Value;
        }
        return TreeViewOrientation.Horizontal;
    }

    public async Task SetOrientationAsync(TreeViewOrientation orientation, CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;
        session?.SetInt32(SessionKey, (int)orientation);

        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return;

        var profile = await _db.UserProfiles.FindAsync(new object[] { userId }, cancellationToken);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId, TreeViewOrientation = orientation };
            _db.UserProfiles.Add(profile);
        }
        else
        {
            profile.TreeViewOrientation = orientation;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}