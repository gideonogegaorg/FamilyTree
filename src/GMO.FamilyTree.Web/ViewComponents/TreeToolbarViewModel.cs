using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.ViewComponents;

public sealed class TreeToolbarViewModel
{
    public long CurrentTreeId { get; set; }
    public string CurrentTreeName { get; set; } = string.Empty;
    public IReadOnlyList<TreeToolbarTreeItem> FamilyTrees { get; set; } = Array.Empty<TreeToolbarTreeItem>();
    public TreeViewOrientation TreeViewOrientation { get; set; }
    public LineageMode LineageMode { get; set; }
    public TreeCardViewMode TreeCardViewMode { get; set; }
    public long? FocusMemberId { get; set; }
    public bool HasMembers { get; set; }
    public TreeAccessLevel CurrentAccessLevel { get; set; }
    public bool CanEdit => CurrentAccessLevel >= TreeAccessLevel.Editor;
    public bool CanManageSharing => CurrentAccessLevel == TreeAccessLevel.Owner;
}

public sealed class TreeToolbarTreeItem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TreeAccessLevel AccessLevel { get; set; }
    public bool IsOwner => AccessLevel == TreeAccessLevel.Owner;
}