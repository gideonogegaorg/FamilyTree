namespace GMO.FamilyTree.Web.Services;

/// <summary>
/// Limits how often transactional email can be sent to a recipient or from a client IP.
/// </summary>
public interface IEmailRateLimiter
{
    /// <summary>
    /// Returns true when a send is allowed and records the attempt.
    /// </summary>
    bool TryAcquire(string operation, string recipientEmail, string? clientIp);
}
