namespace GMO.Family.Web.Models;

public class MemberActionMenuViewModel
{
    public long ContextMemberId { get; set; }
    public long FamilyTreeId { get; set; }
    public string ContextMemberName { get; set; } = string.Empty;
    public bool CanAddParent { get; set; }

    // Edit panel fields
    public string Name { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public DateOnly? DOB { get; set; }
    public int? BirthOrder { get; set; }
    public bool IsMe { get; set; }
    public bool IsMale { get; set; }

    public IReadOnlyList<ExistingRelationshipViewModel> ExistingRelationships { get; set; } = new List<ExistingRelationshipViewModel>();
    public IReadOnlyList<LinkExistingCandidateViewModel> ParentCandidates { get; set; } = new List<LinkExistingCandidateViewModel>();
    public IReadOnlyList<LinkExistingCandidateViewModel> ChildCandidates { get; set; } = new List<LinkExistingCandidateViewModel>();
    public IReadOnlyList<LinkExistingCandidateViewModel> PartnerCandidates { get; set; } = new List<LinkExistingCandidateViewModel>();
}

public class ExistingRelationshipViewModel
{
    public long RelationshipId { get; set; }
    public string Label { get; set; } = string.Empty;
}