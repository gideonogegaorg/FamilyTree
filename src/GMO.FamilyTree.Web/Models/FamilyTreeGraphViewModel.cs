using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Models;

public class FamilyTreeGraphViewModel
{
    public long TreeId { get; set; }
    public string TreeName { get; set; } = string.Empty;
    public string? CurrentUserId { get; set; }
    public long? FocusMemberId { get; set; }
    public TreeViewOrientation TreeViewOrientation { get; set; }
    public LineageMode LineageMode { get; set; }
    public TreeCardViewMode TreeCardViewMode { get; set; }
    public TreeAccessLevel AccessLevel { get; set; }
    public bool CanEdit => AccessLevel >= TreeAccessLevel.Editor;
    public IReadOnlyList<FamilyMemberCardViewModel> Members { get; set; } = new List<FamilyMemberCardViewModel>();

    /// <summary>JSON array of Cytoscape node objects.</summary>
    public string NodesJson { get; set; } = "[]";
    /// <summary>JSON array of Cytoscape edge objects.</summary>
    public string EdgesJson { get; set; } = "[]";
}