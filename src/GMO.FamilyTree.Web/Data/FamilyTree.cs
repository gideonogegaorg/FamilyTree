namespace GMO.FamilyTree.Web.Data;

public class FamilyTree
{
    public long Id { get; set; }
    public Guid Uid { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Id of the user who created/owns this family tree (AspNetUsers.Id). Required.</summary>
    public string OwnerId { get; set; } = string.Empty;

    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
}