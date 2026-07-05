namespace GMO.FamilyTree.Web.Models;

public class FamilyMemberCardViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NickName { get; set; }
    public DateOnly? DOB { get; set; }
    public int? BirthOrder { get; set; }
    public bool IsMe { get; set; }
    public bool IsMale { get; set; }
    public IReadOnlyList<long> ParentIds { get; set; } = new List<long>();
    public IReadOnlyList<long> ChildIds { get; set; } = new List<long>();
    /// <summary>Sibling member ids in BirthOrder (1, 2, 3...).</summary>
    public IReadOnlyList<long> SiblingIds { get; set; } = new List<long>();
    public IReadOnlyList<long> PartnerIds { get; set; } = new List<long>();
}