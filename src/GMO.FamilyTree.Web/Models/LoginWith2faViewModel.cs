using System.ComponentModel.DataAnnotations;



namespace GMO.FamilyTree.Web.Models;



public class LoginWith2faViewModel

{

    [Required]

    [StringLength(7, MinimumLength = 6, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.")]

    [DataType(DataType.Text)]

    [Display(Name = "Verification code")]

    public string TwoFactorCode { get; set; } = string.Empty;



    [Display(Name = "Remember this machine")]
    [Required]
    public bool RememberMachine { get; set; }

    [Required]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

}