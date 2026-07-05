using GMO.Family.Web.Data;
using GMO.Family.Web.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GMO.Family.Web.ViewComponents;

public sealed class TreeToolbarViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly ITreeViewOrientationService _treeViewOrientation;
    private readonly ILineageModeService _lineageMode;

    public TreeToolbarViewComponent(
        AppDbContext db,
        ICurrentFamilyTreeService currentFamilyTree,
        ITreeViewOrientationService treeViewOrientation,
        ILineageModeService lineageMode)
    {
        _db = db;
        _currentFamilyTree = currentFamilyTree;
        _treeViewOrientation = treeViewOrientation;
        _lineageMode = lineageMode;
    }

    public async Task<IViewComponentResult> InvokeAsync(long? focusMemberId = null, bool hasMembers = true)
    {
        if (User?.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var userId = UserClaimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var currentId = await _currentFamilyTree.GetCurrentFamilyTreeIdAsync();
        var familyTrees = string.IsNullOrEmpty(userId)
            ? new List<FamilyTree>()
            : await _db.FamilyTrees.Where(x => x.OwnerId == userId).OrderBy(x => x.Name).ToListAsync();
        var currentTree = currentId.HasValue
            ? familyTrees.FirstOrDefault(x => x.Id == currentId.Value)
            : null;

        var model = new TreeToolbarViewModel
        {
            CurrentTreeId = currentTree?.Id ?? 0,
            CurrentTreeName = currentTree?.Name ?? "Family Tree",
            FamilyTrees = familyTrees,
            TreeViewOrientation = await _treeViewOrientation.GetOrientationAsync(),
            LineageMode = await _lineageMode.GetAsync(),
            FocusMemberId = focusMemberId,
            HasMembers = hasMembers
        };
        return View("Default", model);
    }
}
