using GMO.FamilyTree.Web.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GMO.FamilyTree.Web.ViewComponents;

public sealed class UserMenuViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public UserMenuViewComponent(AppDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return View("Default", new UserMenuViewModel());

        var userId = UserClaimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.Identity.Name ?? string.Empty;

        var profile = userId != null ? await _db.UserProfiles.FindAsync(userId) : null;
        var hasPhoto = !string.IsNullOrEmpty(profile?.PhotoKey) || !string.IsNullOrEmpty(profile?.PhotoUrl);

        var hasPassword = false;
        if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
                hasPassword = await _userManager.HasPasswordAsync(user);
        }

        var model = new UserMenuViewModel
        {
            Email = email,
            PhotoUrl = hasPhoto ? "/photos/profiles/me" : null,
            HasPassword = hasPassword
        };
        return View("Default", model);
    }
}
