using System.ComponentModel.DataAnnotations;

using GMO.FamilyTree.Web.Data;

namespace GMO.FamilyTree.Web.Models;

public class LinkExistingViewModel
{
    public required long ContextMemberId { get; set; }
    public string ContextMemberName { get; set; } = string.Empty;
    public required long FamilyTreeId { get; set; }

    [Required]
    public RelationshipType? RelationshipType { get; set; }

    [Required]
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