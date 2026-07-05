using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Models;

public class LinkExistingViewModel
{
    public long ContextMemberId { get; set; }
    public string ContextMemberName { get; set; } = string.Empty;
    public long FamilyTreeId { get; set; }
    public RelationshipType RelationshipType { get; set; }
    public bool IsChild { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public IReadOnlyList<LinkExistingCandidateViewModel> Candidates { get; set; } = new List<LinkExistingCandidateViewModel>();
    public long? ExistingMemberId { get; set; }
}

public class LinkExistingCandidateViewModel
{
    public long Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}