namespace GMO.FamilyTree.Web.ViewComponents;

public class UserMenuViewModel
{
    public string Email { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool HasPassword { get; set; }
}