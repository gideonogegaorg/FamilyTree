namespace GMO.FamilyTree.Web.Data;

/// <summary>Effective access a user has to a family tree.</summary>
public enum TreeAccessLevel
{
    None = 0,
    Readonly = 1,
    Editor = 2,
    Owner = 3,
}