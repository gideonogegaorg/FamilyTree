using System.ComponentModel.DataAnnotations;

namespace GMO.FamilyTree.Web.Models;

public class ForgotPasswordViewModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}