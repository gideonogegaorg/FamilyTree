using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class EmailRateLimiterTests
{
    private static EmailRateLimiter CreateLimiter(int minIntervalSeconds = 60, int maxPerRecipientPerHour = 2, int maxPerIpPerHour = 5)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new EmailRateLimitOptions
        {
            MinIntervalSeconds = minIntervalSeconds,
            MaxPerRecipientPerHour = maxPerRecipientPerHour,
            MaxPerIpPerHour = maxPerIpPerHour
        });
        return new EmailRateLimiter(cache, options, NullLogger<EmailRateLimiter>.Instance);
    }

    [Fact]
    public void TryAcquire_allows_first_send()
    {
        var limiter = CreateLimiter();
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_blocks_repeat_within_min_interval()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 300);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_allows_different_operations_independently()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 300);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_blocks_after_max_per_recipient_per_hour()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 2);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_blocks_after_max_per_ip_per_hour()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 100, maxPerIpPerHour: 2);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "a@example.com", "9.9.9.9"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "b@example.com", "9.9.9.9"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ShareInvite, "c@example.com", "9.9.9.9"));
    }

    [Fact]
    public void TryAcquire_does_not_consume_recipient_when_ip_limited()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 10, maxPerIpPerHour: 1);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "a@example.com", "9.9.9.9"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "b@example.com", "9.9.9.9"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "b@example.com", "8.8.8.8"));
    }

    [Fact]
    public void TryAcquire_null_ip_uses_shared_unknown_bucket()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 100, maxPerIpPerHour: 2);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ForgotPassword, "a@example.com", null));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "b@example.com", null));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ShareInvite, "c@example.com", null));
    }
}
