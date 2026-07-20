using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class EmailRateLimiterTests
{
    private static EmailRateLimiter CreateLimiter(
        int minIntervalSeconds = 60,
        int maxPerRecipientPerHour = 2,
        int maxPerIpPerHour = 5,
        ILogger<EmailRateLimiter>? logger = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new EmailRateLimitOptions
        {
            MinIntervalSeconds = minIntervalSeconds,
            MaxPerRecipientPerHour = maxPerRecipientPerHour,
            MaxPerIpPerHour = maxPerIpPerHour
        });
        return new EmailRateLimiter(cache, options, logger ?? NullLogger<EmailRateLimiter>.Instance);
    }

    [Fact]
    public void TryAcquire_allows_first_send()
    {
        var limiter = CreateLimiter();
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_blocks_repeat_within_min_interval()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 300);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_allows_different_operations_independently()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 300);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_blocks_after_max_per_recipient_per_hour()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 2);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "user@example.com", "1.2.3.4"));
    }

    [Fact]
    public void TryAcquire_blocks_after_max_per_ip_per_hour()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 100, maxPerIpPerHour: 2);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "a@example.com", "9.9.9.9"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "b@example.com", "9.9.9.9"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ShareInvite, "c@example.com", "9.9.9.9"));
    }

    [Fact]
    public void TryAcquire_does_not_consume_recipient_when_ip_limited()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 10, maxPerIpPerHour: 1);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "a@example.com", "9.9.9.9"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "b@example.com", "9.9.9.9"));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "b@example.com", "8.8.8.8"));
    }

    [Fact]
    public void TryAcquire_null_ip_uses_shared_unknown_bucket()
    {
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 100, maxPerIpPerHour: 2);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.ResetRequest, "a@example.com", null));
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "b@example.com", null));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.ShareInvite, "c@example.com", null));
    }

    [Fact]
    public void TryAcquire_logs_operation_and_recipient_hash_when_recipient_limited()
    {
        var logger = new CollectingLogger<EmailRateLimiter>();
        var limiter = CreateLimiter(minIntervalSeconds: 0, maxPerRecipientPerHour: 1, logger: logger);
        Assert.True(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "user@example.com", "1.2.3.4"));
        Assert.False(limiter.TryAcquire(EmailRateLimitOperations.Confirmation, "user@example.com", "1.2.3.4"));

        var message = Assert.Single(logger.Messages);
        Assert.Contains("Operation=confirmation", message, StringComparison.Ordinal);
        Assert.Contains("RecipientHash=", message, StringComparison.Ordinal);
        Assert.DoesNotContain("user@example.com", message, StringComparison.Ordinal);
    }
}

file sealed class CollectingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
    }
}