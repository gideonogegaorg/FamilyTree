using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

using GMO.FamilyTree.Web.Options;
using GMO.FamilyTree.Web.Services;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace GMO.FamilyTree.Web.UnitTests.Services;

public class SesEmailSenderTests
{
    private static EmailLogProtector CreateProtector() =>
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public async Task SendEmailAsync_calls_ses_with_html_and_plain_text()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        SendEmailRequest? captured = null;
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-1" });

        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            FromAddress = "noreply@example.com",
            FromDisplayName = "Family Tree",
            Region = "us-east-1"
        });
        var sut = new SesEmailSender(ses.Object, options, CreateProtector(), NullLogger<SesEmailSender>.Instance);

        await sut.SendEmailAsync("to@example.com", "Hello", "<p>Hi</p>", "Hi", EmailRateLimitOperations.Confirmation);

        Assert.NotNull(captured);
        Assert.Contains("noreply@example.com", captured!.Source);
        Assert.Equal("to@example.com", Assert.Single(captured.Destination.ToAddresses));
        Assert.Equal("Hello", captured.Message.Subject.Data);
        Assert.Equal("<p>Hi</p>", captured.Message.Body.Html.Data);
        Assert.Equal("Hi", captured.Message.Body.Text.Data);
        Assert.Empty(captured.ReplyToAddresses);
    }

    [Fact]
    public async Task SendEmailAsync_sets_reply_to_when_configured()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        SendEmailRequest? captured = null;
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-2" });

        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            FromAddress = "noreply@example.com",
            ReplyToAddress = "support@example.com"
        });
        var sut = new SesEmailSender(ses.Object, options, CreateProtector(), NullLogger<SesEmailSender>.Instance);

        await sut.SendEmailAsync("to@example.com", "Hello", "<p>Hi</p>", "Hi", EmailRateLimitOperations.ResetRequest);

        Assert.Equal("support@example.com", Assert.Single(captured!.ReplyToAddresses));
    }

    [Fact]
    public async Task SendEmailAsync_throws_when_from_address_missing()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        var sut = new SesEmailSender(
            ses.Object,
            Microsoft.Extensions.Options.Options.Create(new EmailOptions()),
            CreateProtector(),
            NullLogger<SesEmailSender>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.SendEmailAsync("to@example.com", "S", "<p>x</p>", "x", EmailRateLimitOperations.Confirmation));
    }

    [Fact]
    public async Task SendEmailAsync_throws_when_plain_text_missing()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            FromAddress = "noreply@example.com"
        });
        var sut = new SesEmailSender(ses.Object, options, CreateProtector(), NullLogger<SesEmailSender>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.SendEmailAsync("to@example.com", "S", "<p>x</p>", "  ", EmailRateLimitOperations.Confirmation));
    }

    [Fact]
    public async Task SendEmailAsync_logs_and_rethrows_when_ses_fails()
    {
        var ses = new Mock<IAmazonSimpleEmailService>();
        ses.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSimpleEmailServiceException("boom"));

        var logger = new CollectingLogger<SesEmailSender>();
        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions { FromAddress = "noreply@example.com" });
        var sut = new SesEmailSender(ses.Object, options, CreateProtector(), logger);

        await Assert.ThrowsAsync<AmazonSimpleEmailServiceException>(() =>
            sut.SendEmailAsync("to@example.com", "S", "<p>x</p>", "x", EmailRateLimitOperations.Confirmation));

        var message = Assert.Single(logger.Messages);
        Assert.Contains("Operation=", message, StringComparison.Ordinal);
        Assert.Contains("ToProtected=", message, StringComparison.Ordinal);
        Assert.DoesNotContain("to@example.com", message, StringComparison.Ordinal);
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