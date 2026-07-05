namespace GMO.FamilyTree.Web.Data;

public class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    /// <summary>Legacy local profile photo URL; superseded by PhotoKey.</summary>
    public string? PhotoUrl { get; set; }
    /// <summary>Storage key for profile photo (private bucket; served via authenticated endpoint).</summary>
    public string? PhotoKey { get; set; }
    /// <summary>Persisted current family tree selection (cross-browser, cross-device).</summary>
    public long? CurrentFamilyTreeId { get; set; }

    /// <summary>Tree layout orientation (default Horizontal).</summary>
    public TreeViewOrientation? TreeViewOrientation { get; set; }

    /// <summary>Tree lineage mode (default Paternal).</summary>
    public LineageMode? LineageMode { get; set; }

    /// <summary>Tree card display density (default Standard).</summary>
    public TreeCardViewMode? TreeCardViewMode { get; set; }
}