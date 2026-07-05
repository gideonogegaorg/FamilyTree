using GMO.FamilyTree.Web.Services;

using Microsoft.Extensions.Logging;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_completes_without_throwing()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<LoggingEmailSender>();
        var sut = new LoggingEmailSender(logger);

        // Act
        await sut.SendEmailAsync("test@example.com", "Test Subject", "<p>Body</p>");

        // Assert
        // No throw; completes. Optional: assert logger was called via a test logger sink.
    }
}