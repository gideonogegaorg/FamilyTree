using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.ViewComponents;

public sealed class TreeToolbarViewModel
{
    public long CurrentTreeId { get; set; }
    public string CurrentTreeName { get; set; } = string.Empty;
    public IReadOnlyList<Data.FamilyTree> FamilyTrees { get; set; } = Array.Empty<Data.FamilyTree>();
    public TreeViewOrientation TreeViewOrientation { get; set; }
    public LineageMode LineageMode { get; set; }
    public TreeCardViewMode TreeCardViewMode { get; set; }
    public long? FocusMemberId { get; set; }
    public bool HasMembers { get; set; }
}