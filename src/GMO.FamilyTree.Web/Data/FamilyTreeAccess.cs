using Microsoft.AspNetCore.Identity;

namespace GMO.FamilyTree.Web.Data;

/// <summary>Accepted collaborator grant on a family tree (not including the owner).</summary>
public class FamilyTreeAccess
{
    public long Id { get; set; }
    public long FamilyTreeId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public TreeShareRole Role { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    public string GrantedByUserId { get; set; } = string.Empty;

    public FamilyTree FamilyTree { get; set; } = null!;
    public IdentityUser User { get; set; } = null!;
}