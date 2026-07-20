namespace GMO.FamilyTree.Web.Data;

/// <summary>Role granted to a collaborator on a shared family tree (owner is not stored here).</summary>
public enum TreeShareRole
{
    Readonly = 0,
    Editor = 1,
}