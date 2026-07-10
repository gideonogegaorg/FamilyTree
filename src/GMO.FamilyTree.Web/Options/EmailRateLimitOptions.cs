namespace GMO.FamilyTree.Web.Options;

public sealed class EmailRateLimitOptions
{
    public const string SectionName = "Email:RateLimit";

    /// <summary>Minimum seconds between sends to the same recipient for the same operation.</summary>
    public int MinIntervalSeconds { get; set; } = 180;

    /// <summary>Maximum sends per recipient per operation within the hourly window.</summary>
    public int MaxPerRecipientPerHour { get; set; } = 3;

    /// <summary>Maximum outbound email attempts per client IP per hour (all operations).</summary>
    public int MaxPerIpPerHour { get; set; } = 15;
}