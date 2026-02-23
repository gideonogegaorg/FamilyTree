namespace GMO.Family.Web.Data;

public class FamilyMemberRelationship
{
    public long Id { get; set; }
    public long FamilyTreeId { get; set; }
    public long FromMemberId { get; set; }
    public long ToMemberId { get; set; }
    public RelationshipType RelationshipType { get; set; }

    public FamilyTree FamilyTree { get; set; } = null!;
    public FamilyMember FromMember { get; set; } = null!;
    public FamilyMember ToMember { get; set; } = null!;
}
