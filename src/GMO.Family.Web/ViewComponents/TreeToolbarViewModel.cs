using GMO.Family.Web.Data;

namespace GMO.Family.Web.ViewComponents;

public sealed class TreeToolbarViewModel
{
    public long CurrentTreeId { get; set; }
    public string CurrentTreeName { get; set; } = string.Empty;
    public IReadOnlyList<FamilyTree> FamilyTrees { get; set; } = Array.Empty<FamilyTree>();
    public TreeViewOrientation TreeViewOrientation { get; set; }
    public LineageMode LineageMode { get; set; }
    public long? FocusMemberId { get; set; }
    public bool HasMembers { get; set; }
}