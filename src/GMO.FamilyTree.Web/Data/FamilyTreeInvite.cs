namespace GMO.FamilyTree.Web.Data;

/// <summary>Pending or historical share invite (link or email) for a family tree.</summary>
public class FamilyTreeInvite
{
    public long Id { get; set; }
    public long FamilyTreeId { get; set; }
    /// <summary>URL-safe opaque token used in /Share/Accept/{token}.</summary>
    public string Token { get; set; } = string.Empty;
    public TreeShareRole Role { get; set; }
    /// <summary>When set, only a signed-in user with this email may accept (email invite). Null = open link.</summary>
    public string? Email { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public string? AcceptedByUserId { get; set; }

    public FamilyTree FamilyTree { get; set; } = null!;

    public bool IsLinkInvite => string.IsNullOrEmpty(Email);
    public bool IsPending => RevokedAt == null && AcceptedAt == null
        && (ExpiresAt == null || ExpiresAt > DateTimeOffset.UtcNow);
}