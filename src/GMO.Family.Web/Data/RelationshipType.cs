namespace GMO.Family.Web.Data;

/// <summary>Type of relationship between two family members. Child is the reverse of Parent.</summary>
public enum RelationshipType
{
    Parent = 0,
    // Sibling = 1, // Removed: hierarchy is Parent->Child + Couple only
    Couple = 2,
}
