using System.ComponentModel.DataAnnotations;

namespace GMO.FamilyTree.Web.Models;

public class ResendConfirmationViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}