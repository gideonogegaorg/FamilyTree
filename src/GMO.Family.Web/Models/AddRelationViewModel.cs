using System.ComponentModel.DataAnnotations;
using GMO.Family.Web.Data;

namespace GMO.Family.Web.Models;

public class AddRelationViewModel
{
    public long ContextMemberId { get; set; }
    public long FamilyTreeId { get; set; }
    public RelationshipType RelationshipType { get; set; }
    /// <summary>When true with Parent type: new member is child of context (reverse direction).</summary>
    public bool IsChild { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? NickName { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? DOB { get; set; }

    /// <summary>For Sibling: birth order among full siblings (1 = first born, etc.).</summary>
    public int? BirthOrder { get; set; }

    public bool IsMale { get; set; }

    /// <summary>When true, link to current user (this member is "me").</summary>
    public bool SetAsMe { get; set; }
}
