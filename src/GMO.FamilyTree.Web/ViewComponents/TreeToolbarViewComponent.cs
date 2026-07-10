using GMO.FamilyTree.Web.Data;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.Mvc;

namespace GMO.FamilyTree.Web.ViewComponents;

public sealed class TreeToolbarViewComponent : ViewComponent
{
    private readonly ICurrentFamilyTreeService _currentFamilyTree;
    private readonly ITreeViewOrientationService _treeViewOrientation;
    private readonly ILineageModeService _lineageMode;
    private readonly ITreeCardViewModeService _treeCardViewMode;
    private readonly IFamilyTreeAccessService _access;

    public TreeToolbarViewComponent(
        ICurrentFamilyTreeService currentFamilyTree,
        ITreeViewOrientationService treeViewOrientation,
        ILineageModeService lineageMode,
        ITreeCardViewModeService treeCardViewMode,
        IFamilyTreeAccessService access)
    {
        _currentFamilyTree = currentFamilyTree;
        _treeViewOrientation = treeViewOrientation;
        _lineageMode = lineageMode;
        _treeCardViewMode = treeCardViewMode;
        _access = access;
    }

    public async Task<IViewComponentResult> InvokeAsync(long? focusMemberId = null, bool hasMembers = true)
    {
        if (User?.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var userId = UserClaimsPrincipal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var currentId = await _currentFamilyTree.GetCurrentFamilyTreeIdAsync();
        var trees = string.IsNullOrEmpty(userId)
            ? Array.Empty<Data.FamilyTree>()
            : await _access.GetAccessibleTreesAsync(userId);

        var items = new List<TreeToolbarTreeItem>(trees.Count);
        foreach (var tree in trees)
        {
            var level = string.IsNullOrEmpty(userId)
                ? TreeAccessLevel.None
                : await _access.GetAccessLevelAsync(userId, tree.Id);
            items.Add(new TreeToolbarTreeItem
            {
                Id = tree.Id,
                Name = tree.Name,
                AccessLevel = level
            });
        }

        var currentTree = currentId.HasValue
            ? items.FirstOrDefault(x => x.Id == currentId.Value)
            : null;

        var model = new TreeToolbarViewModel
        {
            CurrentTreeId = currentTree?.Id ?? 0,
            CurrentTreeName = currentTree?.Name ?? "Family Tree",
            FamilyTrees = items,
            TreeViewOrientation = await _treeViewOrientation.GetOrientationAsync(),
            LineageMode = await _lineageMode.GetAsync(),
            TreeCardViewMode = await _treeCardViewMode.GetAsync(),
            FocusMemberId = focusMemberId,
            HasMembers = hasMembers,
            CurrentAccessLevel = currentTree?.AccessLevel ?? TreeAccessLevel.None
        };
        return View("Default", model);
    }
}