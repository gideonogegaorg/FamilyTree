using System.Security.Cryptography;
using System.Text;

using GMO.FamilyTree.Web.Extensions;
using GMO.FamilyTree.Web.Options;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GMO.FamilyTree.Web.Services;

public sealed class EmailRateLimiter : IEmailRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly EmailRateLimitOptions _options;
    private readonly ILogger<EmailRateLimiter> _logger;

    public EmailRateLimiter(
        IMemoryCache cache,
        IOptions<EmailRateLimitOptions> options,
        ILogger<EmailRateLimiter> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public bool TryAcquire(string operation, string recipientEmail, string? clientIp)
    {
        if (string.IsNullOrWhiteSpace(operation) || string.IsNullOrWhiteSpace(recipientEmail))
            return false;

        var ip = string.IsNullOrWhiteSpace(clientIp)
            ? HttpContextClientIpExtensions.UnknownClientIp
            : clientIp.Trim();

        if (!TryAcquireIp(ip))
        {
            _logger.LogWarning("Email rate limit exceeded for client IP {ClientIp}", HashForKey(ip));
            return false;
        }

        var email = recipientEmail.Trim().ToLowerInvariant();
        var recipientKey = $"email-rate:recipient:{HashForKey(operation)}:{HashForKey(email)}";
        if (!TryAcquireRecipient(recipientKey))
        {
            _logger.LogWarning("Email rate limit exceeded for a recipient bucket");
            return false;
        }

        return true;
    }

    private bool TryAcquireRecipient(string key)
    {
        var minInterval = _options.MinIntervalSeconds <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(_options.MinIntervalSeconds);
        var window = TimeSpan.FromHours(1);
        var maxPerHour = Math.Max(1, _options.MaxPerRecipientPerHour);

        return TryAcquireBucket(key, minInterval, window, maxPerHour);
    }

    private bool TryAcquireIp(string clientIp)
    {
        var key = $"email-rate:ip:{HashForKey(clientIp)}";
        return TryAcquireBucket(key, TimeSpan.Zero, TimeSpan.FromHours(1), Math.Max(1, _options.MaxPerIpPerHour));
    }

    private bool TryAcquireBucket(string key, TimeSpan minInterval, TimeSpan window, int maxInWindow)
    {
        var bucket = _cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = window;
            return new RateLimitBucket();
        })!;

        lock (bucket.Sync)
        {
            var now = DateTimeOffset.UtcNow;
            Prune(bucket, now, window);

            if (minInterval > TimeSpan.Zero
                && bucket.LastSentAt.HasValue
                && now - bucket.LastSentAt.Value < minInterval)
            {
                return false;
            }

            if (bucket.RecentSends.Count >= maxInWindow)
                return false;

            bucket.RecentSends.Enqueue(now);
            bucket.LastSentAt = now;
            _cache.Set(key, bucket, new MemoryCacheEntryOptions { SlidingExpiration = window });
            return true;
        }
    }

    private static void Prune(RateLimitBucket bucket, DateTimeOffset now, TimeSpan window)
    {
        while (bucket.RecentSends.Count > 0 && now - bucket.RecentSends.Peek() > window)
            bucket.RecentSends.Dequeue();
    }

    private static string HashForKey(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private sealed class RateLimitBucket
    {
        public object Sync { get; } = new();
        public DateTimeOffset? LastSentAt { get; set; }
        public Queue<DateTimeOffset> RecentSends { get; } = new();
    }
}