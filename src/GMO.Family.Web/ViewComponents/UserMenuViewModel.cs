using GMO.Family.Web.Data;

namespace GMO.Family.Web.ViewComponents;

public class UserMenuViewModel
{
    public string Email { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public FamilyTree? CurrentFamilyTree { get; set; }
    public List<FamilyTree> FamilyTrees { get; set; } = new();
}