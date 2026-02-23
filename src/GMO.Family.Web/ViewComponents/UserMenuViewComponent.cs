using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.ViewComponents;

public sealed class UserMenuViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;

    public UserMenuViewComponent(AppDbContext db, ICurrentFamilyTreeService currentFamilyTree)
    {
        _db = db;
        _currentFamilyTree = currentFamilyTree;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return View("Default", new UserMenuViewModel());

        var userId = UserClaimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.Identity.Name ?? string.Empty;

        var currentId = await _currentFamilyTree.GetCurrentFamilyTreeIdAsync();
        var familyTrees = string.IsNullOrEmpty(userId)
            ? new List<FamilyTree>()
            : await _db.FamilyTrees.Where(x => x.OwnerId == userId).OrderBy(x => x.Name).ToListAsync();
        var currentTree = currentId.HasValue ? familyTrees.FirstOrDefault(x => x.Id == currentId.Value) : null;

        var profile = userId != null ? await _db.UserProfiles.FindAsync(userId) : null;
        var photoUrl = profile?.PhotoUrl;

        var model = new UserMenuViewModel
        {
            Email = email,
            PhotoUrl = photoUrl,
            CurrentFamilyTree = currentTree,
            FamilyTrees = familyTrees
        };
        return View("Default", model);
    }
}