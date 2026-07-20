using System.ComponentModel.DataAnnotations;

namespace GMO.FamilyTree.Web.Models;

public class EditMemberRequest
{
    [Required]
    public long MemberId { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? NickName { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? Dob { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? Dod { get; set; }

    public int? BirthOrder { get; set; }

    [Required]
    public bool? IsMale { get; set; }

    [Required]
    public bool? SetAsMe { get; set; }
}