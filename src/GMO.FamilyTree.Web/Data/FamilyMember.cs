using Microsoft.AspNetCore.Identity;

namespace GMO.FamilyTree.Web.Data;

public class FamilyMember
{
    public long Id { get; set; }
    public long FamilyTreeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public DateOnly? DOB { get; set; }
    /// <summary>Birth order among full siblings (1 = first born, 2 = second, etc.).</summary>
    public int? BirthOrder { get; set; }
    public bool IsMale { get; set; }
    /// <summary>When set and equal to current user's id, this member is "me" in the tree.</summary>
    public string? UserId { get; set; }
    /// <summary>Storage key for member photo (private bucket; served via authenticated endpoint).</summary>
    public string? PhotoKey { get; set; }

    public FamilyTree FamilyTree { get; set; } = null!;
    public IdentityUser? User { get; set; }
    public ICollection<FamilyMemberRelationship> OutgoingRelationships { get; set; } = new List<FamilyMemberRelationship>();
    public ICollection<FamilyMemberRelationship> IncomingRelationships { get; set; } = new List<FamilyMemberRelationship>();
}