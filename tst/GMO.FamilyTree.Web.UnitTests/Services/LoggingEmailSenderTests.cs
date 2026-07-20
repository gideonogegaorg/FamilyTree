using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_completes_without_throwing()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<LoggingEmailSender>();
        var protector = new EmailLogProtector(new EphemeralDataProtectionProvider());
        var sut = new LoggingEmailSender(logger, protector);

        await sut.SendEmailAsync("test@example.com", "Test Subject", "<p>Body</p>", "Body");
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