namespace GMO.FamilyTree.Web.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Email backend: Logging (dev/CI) or Ses (production).</summary>
    public string Provider { get; set; } = "Logging";

    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "GOOM Family Tree";
    /// <summary>Optional monitored reply address (Reply-To header). Empty = omit.</summary>
    public string ReplyToAddress { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
}