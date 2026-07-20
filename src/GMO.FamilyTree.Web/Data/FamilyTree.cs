namespace GMO.FamilyTree.Web.Data;

using System.Text.Json.Serialization;

public class FamilyTree
{
    [JsonRequired]
    public long Id { get; set; }

    [JsonRequired]
    public Guid Uid { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Id of the user who created/owns this family tree (AspNetUsers.Id). Required.</summary>
    public string OwnerId { get; set; } = string.Empty;

    public ICollection<FamilyMember> Members { get; set; } = new List<FamilyMember>();
    public ICollection<FamilyTreeAccess> AccessGrants { get; set; } = new List<FamilyTreeAccess>();
    public ICollection<FamilyTreeInvite> Invites { get; set; } = new List<FamilyTreeInvite>();
}