namespace GMO.Family.Web.Data;

public class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    /// <summary>Persisted current family tree selection (cross-browser, cross-device).</summary>
    public long? CurrentFamilyTreeId { get; set; }

    /// <summary>Tree layout orientation (default Horizontal).</summary>
    public TreeViewOrientation? TreeViewOrientation { get; set; }

    /// <summary>Tree lineage path (default Paternal).</summary>
    public TreePathMode? TreePathMode { get; set; }
}