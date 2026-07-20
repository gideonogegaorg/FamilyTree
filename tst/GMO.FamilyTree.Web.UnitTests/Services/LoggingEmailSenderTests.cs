using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_completes_without_throwing()
    {
        var protector = new EmailLogProtector(new EphemeralDataProtectionProvider());
        var sut = new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance, protector);

        var task = sut.SendEmailAsync(
            "test@example.com",
            "Test Subject",
            "<p>Body</p>",
            "Body",
            EmailRateLimitOperations.Confirmation);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task SendEmailAsync_protects_recipient_in_logs()
    {
        var logger = new CollectingLogger<LoggingEmailSender>();
        var protector = new EmailLogProtector(new EphemeralDataProtectionProvider());
        var sut = new LoggingEmailSender(logger, protector);

        await sut.SendEmailAsync(
            "secret@example.com",
            "Invite Subject",
            "<p>x</p>",
            "x",
            EmailRateLimitOperations.ShareInvite);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("Operation=", message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret@example.com", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Invite Subject", message, StringComparison.Ordinal);
    }
}

public class EmailLogProtectorTests
{
    [Fact]
    public void Protect_round_trips_for_troubleshooting()
    {
        var protector = new EmailLogProtector(new EphemeralDataProtectionProvider());
        var ciphertext = protector.Protect("user@example.com");

        Assert.DoesNotContain("user@example.com", ciphertext, StringComparison.Ordinal);
        Assert.Equal("user@example.com", protector.Unprotect(ciphertext));
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