using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.ViewComponents;

public sealed class UserMenuViewComponent : ViewComponent
{
    private readonly AppDbContext _db;

    public UserMenuViewComponent(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return View("Default", new UserMenuViewModel());

        var userId = UserClaimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.Identity.Name ?? string.Empty;

        var profile = userId != null ? await _db.UserProfiles.FindAsync(userId) : null;
        var photoUrl = profile?.PhotoUrl;

        var model = new UserMenuViewModel
        {
            Email = email,
            PhotoUrl = photoUrl
        };
        return View("Default", model);
    }
}
